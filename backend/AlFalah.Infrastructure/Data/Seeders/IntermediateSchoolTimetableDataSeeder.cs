using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlFalah.Infrastructure.Data.Seeders;

/// <summary>
/// Development-only, repeatable fixture for manually exercising every intelligent
/// timetable screen against the Arabic sample school used by secretary_1.
/// </summary>
public sealed class IntermediateSchoolTimetableDataSeeder
{
    private const string TargetSchoolName = "مدرسة الفلاح النموذجية";
    private const string AcademicYearCode = "2026-2027";
    private const string SetupName = "إعداد الاختبار الشامل - المرحلة المتوسطة";
    private const string TimingName = "توقيت الاختبار الشامل - المرحلة المتوسطة";
    private const string TimetableTitle = "الجدول التجريبي الشامل - المرحلة المتوسطة";
    private const string TeacherPassword = "Test@1234";

    private static readonly (string Label, byte Grade, string Section, string Location)[] SeedClassrooms =
    [
        ("الأول متوسط - أ", 1, "أ", "الدور الأول - 101"),
        ("الأول متوسط - ب", 1, "ب", "الدور الأول - 102"),
        ("الثاني متوسط - أ", 2, "أ", "الدور الأول - 201"),
        ("الثاني متوسط - ب", 2, "ب", "الدور الأول - 202"),
        ("الثالث متوسط - أ", 3, "أ", "الدور الثاني - 301"),
        ("الثالث متوسط - ب", 3, "ب", "الدور الثاني - 302")
    ];

    private static readonly (string Name, string Kind)[] SeedRooms =
    [
        ("مختبر العلوم 1", "science"), ("مختبر العلوم 2", "science"),
        ("معمل الحاسب 1", "computer"), ("معمل الحاسب 2", "computer"),
        ("المرسم 1", "art"), ("المرسم 2", "art"),
        ("الصالة الرياضية 1", "gym"), ("الصالة الرياضية 2", "gym"),
        ("المكتبة ومصادر التعلم", "library")
    ];

    private static readonly (string FirstName, string LastName)[] TeacherNames =
    [
        ("أحمد", "الشمري"), ("محمد", "القحطاني"), ("خالد", "العتيبي"),
        ("سلمان", "الدوسري"), ("عبدالله", "الحربي"), ("يوسف", "الغامدي"),
        ("إبراهيم", "الزهراني"), ("عمر", "المطيري"), ("فهد", "العنزي"),
        ("ناصر", "السهلي"), ("سعد", "الرشيدي"), ("تركي", "المالكي"),
        ("بدر", "البلوي"), ("مازن", "العمري"), ("زياد", "السبيعي"),
        ("حسن", "القرني"), ("طارق", "الثقفي"), ("وليد", "الجهني"),
        ("عادل", "التميمي"), ("رامي", "الخالدي")
    ];

    private readonly AlFalahDbContext context;
    private readonly UserManager<ApplicationUser> userManager;
    private readonly RoleManager<ApplicationRole> roleManager;
    private readonly ILogger<IntermediateSchoolTimetableDataSeeder> logger;

    public IntermediateSchoolTimetableDataSeeder(
        AlFalahDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ILogger<IntermediateSchoolTimetableDataSeeder> logger)
    {
        this.context = context;
        this.userManager = userManager;
        this.roleManager = roleManager;
        this.logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var school = await context.Schools.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.Name == TargetSchoolName, cancellationToken)
            .ConfigureAwait(false);
        if (school is null)
        {
            logger.LogWarning("Intelligent timetable seed skipped: target school {SchoolName} was not found.", TargetSchoolName);
            return;
        }

        logger.LogInformation("Ensuring comprehensive intelligent timetable data for school {SchoolId}...", school.Id);
        school.Stage = SchoolStage.Intermediate;
        school.IsActive = true;
        school.IsDeleted = false;
        school.DeletedAt = null;
        school.DeletedByUserId = null;

        var actorUserId = await ResolveActorUserIdAsync(school, cancellationToken).ConfigureAwait(false);
        var academicYear = await EnsureAcademicYearAsync(cancellationToken).ConfigureAwait(false);
        var setup = await EnsureSetupAsync(school, academicYear, actorUserId, cancellationToken).ConfigureAwait(false);
        var schedule = await EnsureScheduleAsync(school, academicYear, actorUserId, cancellationToken).ConfigureAwait(false);
        setup.BellScheduleTemplateId = schedule.BellScheduleTemplateId;

        var classrooms = await EnsureClassroomsAsync(school, academicYear, actorUserId, cancellationToken).ConfigureAwait(false);
        var subjects = await EnsureSubjectsAsync(school, actorUserId, cancellationToken).ConfigureAwait(false);
        var rooms = await EnsureRoomsAsync(school, cancellationToken).ConfigureAwait(false);
        var instructors = await EnsureTeachersAsync(school, actorUserId, cancellationToken).ConfigureAwait(false);
        var teacherProfiles = await EnsureTeacherProfilesAsync(
            school, setup, schedule, instructors, actorUserId, cancellationToken).ConfigureAwait(false);

        var requirementPlans = await EnsureRequirementsAsync(
            school, setup, classrooms, subjects, rooms, actorUserId, cancellationToken).ConfigureAwait(false);
        var allocation = await ReplaceAssignmentsAsync(
            school, setup, requirementPlans, teacherProfiles, actorUserId, cancellationToken).ConfigureAwait(false);
        await ReplaceAvailabilityAsync(schedule, teacherProfiles, allocation.States, cancellationToken).ConfigureAwait(false);

        setup.Status = TimetableSetupStatus.Generated;
        setup.Revision++;
        setup.UpdatedAt = DateTimeOffset.UtcNow;
        setup.UpdatedByUserId = actorUserId;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var timetable = await ReplaceTimetableAsync(
            school, academicYear, setup, schedule, allocation, rooms, actorUserId, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Intelligent timetable seed ready: {Classrooms} classrooms, {Subjects} subjects, {Teachers} teachers, " +
            "{Requirements} requirements and {Entries} timetable entries (timetable {TimetableId}).",
            classrooms.Count, subjects.Count, teacherProfiles.Count, requirementPlans.Count,
            timetable.Entries.Count(x => !x.IsDeleted), timetable.Id);
    }

    private async Task<string> ResolveActorUserIdAsync(School school, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(school.ManagerUserId) &&
            await context.Users.IgnoreQueryFilters().AnyAsync(x => x.Id == school.ManagerUserId, cancellationToken).ConfigureAwait(false))
            return school.ManagerUserId;

        return await context.UserSchoolRoles.IgnoreQueryFilters()
            .Where(x => x.SchoolId == school.Id && x.IsActive && !x.IsDeleted && x.User.IsActive && !x.User.IsDeleted)
            .OrderBy(x => x.Role.Name == RoleNames.SchoolManager ? 0 : x.Role.Name == RoleNames.Secretary ? 1 : 2)
            .Select(x => x.UserId)
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<AcademicYear> EnsureAcademicYearAsync(CancellationToken cancellationToken)
    {
        var year = await context.AcademicYears.SingleOrDefaultAsync(x => x.Code == AcademicYearCode, cancellationToken)
            .ConfigureAwait(false);
        if (year is not null) return year;

        year = new AcademicYear
        {
            Code = AcademicYearCode,
            NameAr = "العام الدراسي 2026-2027",
            StartsOn = new DateOnly(2026, 8, 23),
            EndsOn = new DateOnly(2027, 6, 30),
            IsActive = true
        };
        context.AcademicYears.Add(year);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return year;
    }

    private async Task<TimetableSetupProfile> EnsureSetupAsync(
        School school, AcademicYear year, string actor, CancellationToken cancellationToken)
    {
        var setup = await context.TimetableSetupProfiles.IgnoreQueryFilters()
            .Where(x => x.SchoolId == school.Id && x.AcademicYearId == year.Id && x.Semester == TimetableSemester.First)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (setup is null)
        {
            setup = new TimetableSetupProfile
            {
                SchoolId = school.Id,
                AcademicYearId = year.Id,
                Semester = TimetableSemester.First,
                Name = SetupName,
                CreatedByUserId = actor,
                UpdatedByUserId = actor
            };
            context.TimetableSetupProfiles.Add(setup);
        }
        else
        {
            setup.Name = SetupName;
            setup.IsDeleted = false;
            setup.DeletedAt = null;
            setup.DeletedByUserId = null;
            setup.UpdatedByUserId = actor;
            setup.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return setup;
    }

    private async Task<BellScheduleRevision> EnsureScheduleAsync(
        School school, AcademicYear year, string actor, CancellationToken cancellationToken)
    {
        var template = await context.Set<BellScheduleTemplate>().IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.SchoolId == school.Id && x.AcademicYearId == year.Id &&
                x.Semester == TimetableSemester.First && x.Name == TimingName, cancellationToken)
            .ConfigureAwait(false);
        if (template is null)
        {
            template = new BellScheduleTemplate
            {
                SchoolId = school.Id,
                AcademicYearId = year.Id,
                Semester = TimetableSemester.First,
                Name = TimingName,
                Revision = 1,
                IsActive = true,
                CreatedByUserId = actor,
                UpdatedByUserId = actor
            };
            context.Add(template);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var revision = await context.Set<BellScheduleRevision>().AsNoTracking()
            .Include(x => x.Days).ThenInclude(x => x.Periods)
            .Include(x => x.Days).ThenInclude(x => x.Breaks).ThenInclude(x => x.Window)
            .SingleOrDefaultAsync(x => x.SchoolId == school.Id && x.BellScheduleTemplateId == template.Id &&
                x.Revision == template.Revision, cancellationToken)
            .ConfigureAwait(false);
        if (revision is not null) return revision;

        revision = BuildScheduleRevision(school.Id, template.Id, template.Revision, actor);
        context.Add(revision);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return revision;
    }

    private static BellScheduleRevision BuildScheduleRevision(int schoolId, int templateId, int revision, string actor)
    {
        var result = new BellScheduleRevision
        {
            SchoolId = schoolId,
            BellScheduleTemplateId = templateId,
            Revision = revision,
            Name = TimingName,
            SchoolTimeZoneId = "Asia/Riyadh",
            CreatedByUserId = actor
        };

        var defaultDay = new BellScheduleDay { Day = 0, IsStudyDay = false, UsesDefaultSchedule = false, UsesDefaultBreaks = false };
        AddPeriods(defaultDay,
            ("الأولى", "07:00", "07:45"), ("الثانية", "07:45", "08:30"), ("الثالثة", "08:30", "09:15"),
            ("الرابعة", "09:35", "10:20"), ("الخامسة", "10:20", "11:05"), ("السادسة", "11:05", "11:50"),
            ("السابعة", "12:10", "12:55"));
        AddBreak(defaultDay, "الفسحة الرئيسية", "Recess", "09:15", "09:35", actor);
        AddBreak(defaultDay, "استراحة الصلاة", "Prayer", "11:50", "12:10", actor);
        result.Days.Add(defaultDay);

        foreach (var day in Enumerable.Range(1, 7))
        {
            var studyDay = day is >= (int)TimetableDay.Sunday and <= (int)TimetableDay.Thursday;
            var scheduleDay = new BellScheduleDay
            {
                Day = day,
                IsStudyDay = studyDay,
                UsesDefaultSchedule = true,
                UsesDefaultBreaks = true
            };
            result.Days.Add(scheduleDay);
        }

        var thursday = result.Days.Single(x => x.Day == (int)TimetableDay.Thursday);
        thursday.UsesDefaultSchedule = false;
        thursday.UsesDefaultBreaks = false;
        AddPeriods(thursday,
            ("الأولى - الخميس", "07:00", "07:40"), ("الثانية - الخميس", "07:40", "08:20"),
            ("الثالثة - الخميس", "08:20", "09:00"), ("الرابعة - الخميس", "09:20", "10:00"),
            ("الخامسة - الخميس", "10:00", "10:40"), ("السادسة - الخميس", "10:40", "11:20"),
            ("السابعة - الخميس", "11:20", "12:00"));
        AddBreak(thursday, "فسحة الخميس", "Recess", "09:00", "09:20", actor);
        return result;
    }

    private static void AddPeriods(BellScheduleDay day, params (string Label, string Start, string End)[] periods)
    {
        for (var index = 0; index < periods.Length; index++)
        {
            var period = periods[index];
            day.Periods.Add(new BellPeriod
            {
                Sequence = index + 1,
                DisplayLabel = period.Label,
                StartLocalTime = TimeOnly.ParseExact(period.Start, "HH:mm"),
                EndLocalTime = TimeOnly.ParseExact(period.End, "HH:mm")
            });
        }
    }

    private static void AddBreak(BellScheduleDay day, string name, string category, string start, string end, string actor) =>
        day.Breaks.Add(new ScheduleBreakDefinition
        {
            Name = name,
            Category = category,
            CreatedByUserId = actor,
            Window = new ScheduleBreakWindow
            {
                StartLocalTime = TimeOnly.ParseExact(start, "HH:mm"),
                EndLocalTime = TimeOnly.ParseExact(end, "HH:mm")
            }
        });

    private async Task<IReadOnlyList<Classroom>> EnsureClassroomsAsync(
        School school, AcademicYear year, string actor, CancellationToken cancellationToken)
    {
        var existing = await context.Classrooms.IgnoreQueryFilters()
            .Where(x => x.SchoolId == school.Id && x.AcademicYearId == year.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var seed in SeedClassrooms)
        {
            var classroom = existing.SingleOrDefault(x => x.ClassLabel == seed.Label);
            if (classroom is null)
            {
                classroom = new Classroom
                {
                    SchoolId = school.Id,
                    AcademicYearId = year.Id,
                    ClassLabel = seed.Label,
                    CreatedByUserId = actor
                };
                context.Classrooms.Add(classroom);
                existing.Add(classroom);
            }

            classroom.Stage = SchoolStage.Intermediate;
            classroom.GradeLevel = seed.Grade;
            classroom.Section = seed.Section;
            classroom.PhysicalLocation = seed.Location;
            classroom.IsActive = true;
            classroom.IsDeleted = false;
            classroom.DeletedAt = null;
            classroom.DeletedByUserId = null;
            classroom.UpdatedByUserId = actor;
            classroom.UpdatedAt = DateTimeOffset.UtcNow;
        }

        foreach (var classroom in existing.Where(x => !x.IsDeleted))
        {
            classroom.Stage = SchoolStage.Intermediate;
            classroom.IsActive = true;
            if (string.IsNullOrWhiteSpace(classroom.PhysicalLocation)) classroom.PhysicalLocation = $"فصل دراسي - {classroom.Id}";
            classroom.UpdatedByUserId = actor;
        }
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return existing.Where(x => !x.IsDeleted && x.IsActive).OrderBy(x => x.Id).ToArray();
    }

    private async Task<IReadOnlyList<SubjectDefinition>> EnsureSubjectsAsync(
        School school, string actor, CancellationToken cancellationToken)
    {
        var existing = await context.Set<SubjectDefinition>().IgnoreQueryFilters()
            .Where(x => x.SchoolId == school.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var seed in IntermediateTimetableSeedPlan.Subjects)
        {
            var subject = existing.SingleOrDefault(x => x.Name == seed.Name);
            if (subject is null)
            {
                subject = new SubjectDefinition { SchoolId = school.Id, Name = seed.Name };
                context.Add(subject);
                existing.Add(subject);
            }
            subject.Color = seed.Color;
            subject.IsActive = true;
            subject.IsDeleted = false;
            subject.UpdatedByUserId = actor;
            subject.UpdatedAt = DateTimeOffset.UtcNow;
        }
        foreach (var extra in existing.Where(x => !x.IsDeleted && IntermediateTimetableSeedPlan.Subjects.All(s => s.Name != x.Name)))
        {
            extra.IsActive = false;
            extra.IsDeleted = true;
            extra.UpdatedByUserId = actor;
            extra.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return existing.Where(x => !x.IsDeleted && x.IsActive && IntermediateTimetableSeedPlan.Subjects.Any(s => s.Name == x.Name))
            .OrderBy(x => x.Id).ToArray();
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyList<TimetableRoom>>> EnsureRoomsAsync(
        School school, CancellationToken cancellationToken)
    {
        var existing = await context.Set<TimetableRoom>().IgnoreQueryFilters()
            .Where(x => x.SchoolId == school.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var seed in SeedRooms)
        {
            var room = existing.SingleOrDefault(x => x.Name == seed.Name);
            if (room is null)
            {
                room = new TimetableRoom { SchoolId = school.Id, Name = seed.Name };
                context.Add(room);
                existing.Add(room);
            }
            room.IsActive = true;
            room.IsDeleted = false;
        }
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return SeedRooms.Select(x => x.Kind).Distinct().ToDictionary(
            kind => kind,
            kind => (IReadOnlyList<TimetableRoom>)existing.Where(x => !x.IsDeleted && x.IsActive &&
                SeedRooms.Any(seed => seed.Kind == kind && seed.Name == x.Name)).OrderBy(x => x.Id).ToArray());
    }

    private async Task<IReadOnlyList<InstructorProfile>> EnsureTeachersAsync(
        School school, string actor, CancellationToken cancellationToken)
    {
        var instructors = await ActiveTeachers(school.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        var instructorRole = await roleManager.FindByNameAsync(RoleNames.Instructor)
            ?? throw new InvalidOperationException("Instructor role is required before timetable test data can be seeded.");

        for (var index = 0; instructors.Count < 20 && index < TeacherNames.Length; index++)
        {
            var username = $"timetable.teacher{index + 1:00}";
            var user = await EnsureTeacherUserAsync(username, TeacherNames[index], cancellationToken).ConfigureAwait(false);
            await EnsureTeacherSchoolRoleAsync(user, school, instructorRole, actor, cancellationToken).ConfigureAwait(false);

            var instructor = await context.InstructorProfiles.IgnoreQueryFilters()
                .SingleOrDefaultAsync(x => x.UserId == user.Id, cancellationToken).ConfigureAwait(false);
            if (instructor is null)
            {
                instructor = new InstructorProfile { UserId = user.Id, SchoolId = school.Id };
                context.InstructorProfiles.Add(instructor);
            }
            instructor.SchoolId = school.Id;
            instructor.Stage = SchoolStage.Intermediate;
            instructor.SubjectSpecialization = IntermediateTimetableSeedPlan.Subjects[index % IntermediateTimetableSeedPlan.Subjects.Count].Name;
            instructor.EmployeeNumber = $"TT-{index + 1:000}";
            instructor.QualificationAr = index % 3 == 0 ? "ماجستير تربية" : "بكالوريوس تربية";
            instructor.HireDate = new DateOnly(2012 + index % 10, 8, 20);
            instructor.IsActive = true;
            instructor.IsDeleted = false;
            instructor.DeletedAt = null;
            instructor.DeletedByUserId = null;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            instructors = await ActiveTeachers(school.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        if (instructors.Count < 20) throw new InvalidOperationException("Could not provision 20 active timetable teachers.");
        return instructors.OrderBy(x => x.Id).Take(20).ToArray();
    }

    private IQueryable<InstructorProfile> ActiveTeachers(int schoolId) => context.InstructorProfiles
        .IgnoreQueryFilters().Include(x => x.User)
        .Where(x => x.SchoolId == schoolId && x.IsActive && !x.IsDeleted && x.User.IsActive && !x.User.IsDeleted);

    private async Task<ApplicationUser> EnsureTeacherUserAsync(
        string username, (string FirstName, string LastName) name, CancellationToken cancellationToken)
    {
        var normalized = userManager.NormalizeName(username);
        var user = await context.Users.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.NormalizedUserName == normalized, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = username,
                Email = $"{username}@alfalah.test",
                EmailConfirmed = true,
                FirstName = name.FirstName,
                LastName = name.LastName,
                PreferredLanguage = "ar",
                IsActive = true
            };
            EnsureIdentity(await userManager.CreateAsync(user, TeacherPassword), $"create {username}");
        }
        else
        {
            user.FirstName = name.FirstName;
            user.LastName = name.LastName;
            user.IsActive = true;
            user.IsDeleted = false;
            user.DeletedAt = null;
            user.DeletedByUserId = null;
            EnsureIdentity(await userManager.UpdateAsync(user), $"update {username}");
        }
        if (!await userManager.IsInRoleAsync(user, RoleNames.Instructor).ConfigureAwait(false))
            EnsureIdentity(await userManager.AddToRoleAsync(user, RoleNames.Instructor), $"assign instructor to {username}");
        return user;
    }

    private async Task EnsureTeacherSchoolRoleAsync(ApplicationUser user, School school, ApplicationRole role, string actor, CancellationToken cancellationToken)
    {
        var assignment = await context.UserSchoolRoles.IgnoreQueryFilters().SingleOrDefaultAsync(
            x => x.UserId == user.Id && x.SchoolId == school.Id && x.RoleId == role.Id, cancellationToken).ConfigureAwait(false);
        if (assignment is null)
        {
            context.UserSchoolRoles.Add(new UserSchoolRole
            {
                UserId = user.Id, SchoolId = school.Id, RoleId = role.Id,
                CreatedByUserId = actor, UpdatedByUserId = actor
            });
        }
        else
        {
            assignment.IsActive = true;
            assignment.IsDeleted = false;
            assignment.DeletedAt = null;
            assignment.DeletedByUserId = null;
            assignment.UpdatedByUserId = actor;
        }
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureIdentity(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException($"Identity operation failed ({operation}): {string.Join(", ", result.Errors.Select(x => x.Description))}");
    }

    private async Task<IReadOnlyList<TeacherTimetableProfile>> EnsureTeacherProfilesAsync(
        School school, TimetableSetupProfile setup, BellScheduleRevision schedule,
        IReadOnlyList<InstructorProfile> instructors, string actor, CancellationToken cancellationToken)
    {
        var profiles = await context.TeacherTimetableProfiles
            .Where(x => x.SchoolId == school.Id && x.TimetableSetupProfileId == setup.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var instructor in instructors)
        {
            var profile = profiles.SingleOrDefault(x => x.InstructorProfileId == instructor.Id);
            if (profile is null)
            {
                profile = new TeacherTimetableProfile
                {
                    SchoolId = school.Id,
                    TimetableSetupProfileId = setup.Id,
                    InstructorProfileId = instructor.Id,
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedByUserId = actor,
                    Revision = 1
                };
                context.Add(profile);
                profiles.Add(profile);
            }
            profile.BellScheduleRevisionId = schedule.Id;
            profile.ShortDisplayName = instructor.User.FirstName;
            profile.MaximumWeeklyPeriods = 24;
            profile.IsVisiting = false;
            profile.HideFromPrint = false;
            profile.UpdatedAt = DateTimeOffset.UtcNow;
            profile.UpdatedByUserId = actor;
        }
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return profiles.Where(x => instructors.Any(i => i.Id == x.InstructorProfileId)).OrderBy(x => x.InstructorProfileId).ToArray();
    }

    private async Task<IReadOnlyList<RequirementPlan>> EnsureRequirementsAsync(
        School school, TimetableSetupProfile setup, IReadOnlyList<Classroom> classrooms,
        IReadOnlyList<SubjectDefinition> subjects, IReadOnlyDictionary<string, IReadOnlyList<TimetableRoom>> rooms,
        string actor, CancellationToken cancellationToken)
    {
        var existing = await context.Set<ClassSubjectRequirement>().IgnoreQueryFilters()
            .Where(x => x.SchoolId == school.Id && x.TimetableSetupProfileId == setup.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var plans = new List<RequirementPlan>();
        for (var classroomIndex = 0; classroomIndex < classrooms.Count; classroomIndex++)
        {
            var classroom = classrooms[classroomIndex];
            var week = IntermediateTimetableSeedPlan.BuildWeek(classroomIndex);
            foreach (var subjectSeed in IntermediateTimetableSeedPlan.Subjects)
            {
                var subject = subjects.Single(x => x.Name == subjectSeed.Name);
                var requirement = existing.SingleOrDefault(x => x.ClassroomId == classroom.Id && x.SubjectId == subject.Id);
                if (requirement is null)
                {
                    requirement = new ClassSubjectRequirement
                    {
                        SchoolId = school.Id,
                        TimetableSetupProfileId = setup.Id,
                        ClassroomId = classroom.Id,
                        SubjectId = subject.Id
                    };
                    context.Add(requirement);
                    existing.Add(requirement);
                }
                requirement.IndividualPeriodCount = subjectSeed.IndividualPeriods;
                requirement.PairedBlockCount = subjectSeed.PairedBlocks;
                requirement.TimePreference = subjectSeed.TimePreference;
                requirement.EarliestPeriodSequence = subjectSeed.EarliestPeriod;
                requirement.LatestPreferredPeriodSequence = subjectSeed.LatestPeriod;
                requirement.IsDeleted = false;
                requirement.UpdatedAt = DateTimeOffset.UtcNow;
                requirement.UpdatedByUserId = actor;
                plans.Add(new RequirementPlan(classroomIndex, classroom, subjectSeed, subject, requirement,
                    week.Where(x => x.SubjectName == subjectSeed.Name).ToArray()));
            }
        }
        var plannedRequirements = plans.Select(x => x.Requirement).ToHashSet();
        foreach (var extra in existing.Where(x => !x.IsDeleted && !plannedRequirements.Contains(x)))
        {
            extra.IsDeleted = true;
            extra.UpdatedAt = DateTimeOffset.UtcNow;
            extra.UpdatedByUserId = actor;
        }
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var requirementIds = plans.Select(x => x.Requirement.Id).ToArray();
        context.RemoveRange(context.Set<ClassSubjectAllowedDay>().Where(x => requirementIds.Contains(x.ClassSubjectRequirementId)));
        context.RemoveRange(context.Set<ClassSubjectFixedSlot>().Where(x => requirementIds.Contains(x.ClassSubjectRequirementId)));
        context.RemoveRange(context.Set<SubjectRoomRequirement>().Where(x => requirementIds.Contains(x.ClassSubjectRequirementId)));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var plan in plans)
        {
            if (plan.Subject.RoomKind is not null)
            {
                var choices = rooms[plan.Subject.RoomKind];
                if (plan.Subject.RoomIsRequired)
                {
                    foreach (var room in choices)
                        context.Add(new SubjectRoomRequirement { SchoolId = school.Id, ClassSubjectRequirementId = plan.Requirement.Id, RoomId = room.Id });
                }
                else
                {
                    var preferred = choices[plan.ClassroomIndex >= 5 ? 1 : 0];
                    context.Add(new SubjectRoomRequirement
                    {
                        SchoolId = school.Id,
                        ClassSubjectRequirementId = plan.Requirement.Id,
                        RoomId = preferred.Id,
                        IsPreferred = true
                    });
                }
            }

            if (plan.Subject.RoomKind is not null || plan.Subject.Name is "النشاط المدرسي" or "الدراسات الاجتماعية")
            {
                foreach (var day in plan.Occurrences.Select(x => x.Day).Distinct())
                    context.Add(new ClassSubjectAllowedDay { ClassSubjectRequirementId = plan.Requirement.Id, Day = day });
            }
            if (plan.Subject.Name == "المهارات الحياتية" || plan.Subject.Name == "النشاط المدرسي" && plan.ClassroomIndex % 2 == 0)
            {
                var slot = plan.Occurrences[0];
                context.Add(new ClassSubjectFixedSlot { ClassSubjectRequirementId = plan.Requirement.Id, Day = slot.Day, Period = slot.Period });
            }
        }
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return plans;
    }

    private async Task<AllocationResult> ReplaceAssignmentsAsync(
        School school, TimetableSetupProfile setup, IReadOnlyList<RequirementPlan> plans,
        IReadOnlyList<TeacherTimetableProfile> profiles, string actor, CancellationToken cancellationToken)
    {
        var oldAssignments = await context.Set<TeachingAssignment>().IgnoreQueryFilters()
            .Include(x => x.Members)
            .Where(x => x.SchoolId == school.Id && x.TimetableSetupProfileId == setup.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        context.RemoveRange(oldAssignments.SelectMany(x => x.Members));
        context.RemoveRange(oldAssignments);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var instructorIds = profiles.Select(x => x.InstructorProfileId).ToArray();
        var instructors = await ActiveTeachers(school.Id).Where(x => instructorIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken).ConfigureAwait(false);
        var states = profiles.Select(p => new TeacherState(p, instructors[p.InstructorProfileId])).ToArray();
        var entries = new List<PlannedEntry>();

        foreach (var plan in plans.OrderBy(x => x.ClassroomIndex).ThenByDescending(x => x.Occurrences.Count))
        {
            string mode;
            IReadOnlyList<TeacherAllocation> allocations;
            if (plan.ClassroomIndex == 0 && plan.Subject.Name == "المهارات الحياتية")
            {
                mode = "SingleTeacher";
                allocations = [Allocate(states[16], plan.Occurrences)];
            }
            else if (plan.ClassroomIndex == 0 && plan.Subject.Name == "النشاط المدرسي")
            {
                mode = "SingleTeacher";
                allocations = [Allocate(states[18], plan.Occurrences)];
            }
            else if (plan.ClassroomIndex == 1 && plan.Subject.Name == "التربية الفنية")
            {
                mode = "CoTeaching";
                var first = Pick(states, plan.Occurrences, plan.Subject.Name);
                var second = Pick(states, plan.Occurrences, plan.Subject.Name, first);
                allocations = [Allocate(first, plan.Occurrences), Allocate(second, plan.Occurrences)];
            }
            else if (plan.ClassroomIndex == 2 && plan.Subject.Name == "اللغة العربية")
            {
                mode = "SplitQuota";
                var units = plan.Occurrences.GroupBy(x => x.PairKey ?? -100 - x.Day * 10 - x.Period).ToArray();
                var firstOccurrences = units.Where(x => x.Key > 0).SelectMany(x => x)
                    .Concat(units.Where(x => x.Key < 0).Take(1).SelectMany(x => x)).ToArray();
                var secondOccurrences = plan.Occurrences.Except(firstOccurrences).ToArray();
                var first = Pick(states, firstOccurrences, plan.Subject.Name);
                var second = Pick(states, secondOccurrences, plan.Subject.Name, first);
                allocations = [Allocate(first, firstOccurrences), Allocate(second, secondOccurrences)];
            }
            else
            {
                var single = TryPick(states, plan.Occurrences, plan.Subject.Name);
                if (single is not null)
                {
                    mode = "SingleTeacher";
                    allocations = [Allocate(single, plan.Occurrences)];
                }
                else
                {
                    mode = "SplitQuota";
                    allocations = AllocateByWholeBlocks(states, plan.Occurrences, plan.Subject.Name);
                }
            }

            var assignment = new TeachingAssignment
            {
                SchoolId = school.Id,
                TimetableSetupProfileId = setup.Id,
                ClassSubjectRequirementId = plan.Requirement.Id,
                Mode = mode,
                UpdatedByUserId = actor,
                Members = allocations.Select(x => new TeachingAssignmentMember
                {
                    SchoolId = school.Id,
                    TimetableSetupProfileId = setup.Id,
                    TeacherTimetableProfileId = x.Teacher.Profile.Id,
                    AllocatedPeriodCount = x.Occurrences.Count,
                    AllocatedPairedBlockCount = x.Occurrences.Where(o => o.PairKey.HasValue).Select(o => o.PairKey).Distinct().Count()
                }).ToList()
            };
            context.Add(assignment);
            entries.AddRange(allocations.SelectMany(allocation => allocation.Occurrences.Select(occurrence =>
                new PlannedEntry(plan, allocation.Teacher, occurrence))));
        }
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        for (var index = 0; index < states.Length; index++)
        {
            var state = states[index];
            state.Profile.MaximumWeeklyPeriods = Math.Max(state.Load, index switch
            {
                16 => 12,
                17 => 18,
                18 => 16,
                19 => 10,
                _ => 24 + index % 3 * 2
            });
            state.Profile.IsVisiting = index is 15 or 19;
            state.Profile.HideFromPrint = index == 19;
            state.Profile.Revision++;
        }
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new AllocationResult(states, entries);
    }

    private static TeacherState Pick(
        IReadOnlyList<TeacherState> states, IReadOnlyList<TimetableSeedOccurrence> occurrences,
        string subject, TeacherState? excluded = null)
    {
        return TryPick(states, occurrences, subject, excluded)
            ?? throw new InvalidOperationException($"No collision-free teacher could be allocated to {subject}.");
    }

    private static TeacherState? TryPick(
        IReadOnlyList<TeacherState> states, IReadOnlyList<TimetableSeedOccurrence> occurrences,
        string subject, TeacherState? excluded = null) =>
        states.Take(16).Where(x => x != excluded && x.CanTeach(occurrences))
            .OrderBy(x => x.Instructor.SubjectSpecialization == subject ? 0 : 1)
            .ThenBy(x => x.Load)
            .ThenBy(x => x.Profile.Id)
            .FirstOrDefault();

    private static IReadOnlyList<TeacherAllocation> AllocateByWholeBlocks(
        IReadOnlyList<TeacherState> states, IReadOnlyList<TimetableSeedOccurrence> occurrences, string subject)
    {
        var pieces = new List<TeacherAllocation>();
        foreach (var block in occurrences.GroupBy(x => x.PairKey ?? -100 - x.Day * 10 - x.Period)
                     .OrderByDescending(x => x.Count()).ThenBy(x => x.Min(o => o.Day)).ThenBy(x => x.Min(o => o.Period)))
        {
            var blockOccurrences = block.ToArray();
            pieces.Add(Allocate(Pick(states, blockOccurrences, subject), blockOccurrences));
        }

        return pieces.GroupBy(x => x.Teacher)
            .Select(group => new TeacherAllocation(group.Key, group.SelectMany(x => x.Occurrences).ToArray()))
            .ToArray();
    }

    private static TeacherAllocation Allocate(TeacherState teacher, IReadOnlyList<TimetableSeedOccurrence> occurrences)
    {
        if (!teacher.CanTeach(occurrences)) throw new InvalidOperationException("Reserved timetable teacher has a slot collision.");
        foreach (var occurrence in occurrences) teacher.Occupied.Add((occurrence.Day, occurrence.Period));
        teacher.Load += occurrences.Count;
        return new TeacherAllocation(teacher, occurrences);
    }

    private async Task ReplaceAvailabilityAsync(
        BellScheduleRevision schedule, IReadOnlyList<TeacherTimetableProfile> profiles,
        IReadOnlyList<TeacherState> states, CancellationToken cancellationToken)
    {
        var profileIds = profiles.Select(x => x.Id).ToArray();
        context.RemoveRange(context.TeacherAvailabilitySlots.Where(x => profileIds.Contains(x.TeacherTimetableProfileId)));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var defaultPeriods = schedule.Days.Single(x => x.Day == 0).Periods.OrderBy(x => x.Sequence).ToArray();
        var cells = schedule.Days.Where(x => x.Day is >= 2 and <= 6 && x.IsStudyDay)
            .SelectMany(day => (day.UsesDefaultSchedule ? defaultPeriods : day.Periods.OrderBy(x => x.Sequence).ToArray())
                .Select(period => (Day: day.Day, Period: period))).ToArray();
        for (var index = 0; index < states.Count; index++)
        {
            var state = states[index];
            var unavailable = new HashSet<(int Day, int Period)>();
            if (index == 19) unavailable.Add(((int)TimetableDay.Sunday, 7));
            foreach (var cell in cells.Where(x => !state.Occupied.Contains((x.Day, x.Period.Sequence)))
                         .OrderBy(x => (x.Day * 7 + x.Period.Sequence + index * 3) % 35))
            {
                if (unavailable.Count >= index % 4) break;
                if (index is 17 or 18 && cell.Day == (int)TimetableDay.Sunday && cell.Period.Sequence == 7) continue;
                unavailable.Add((cell.Day, cell.Period.Sequence));
            }

            foreach (var cell in cells)
                context.TeacherAvailabilitySlots.Add(new TeacherAvailabilitySlot
                {
                    TeacherTimetableProfileId = state.Profile.Id,
                    Day = cell.Day,
                    BellPeriodId = cell.Period.Id,
                    IsAvailable = !unavailable.Contains((cell.Day, cell.Period.Sequence))
                });
        }
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<SchoolTimetable> ReplaceTimetableAsync(
        School school, AcademicYear year, TimetableSetupProfile setup, BellScheduleRevision schedule,
        AllocationResult allocation, IReadOnlyDictionary<string, IReadOnlyList<TimetableRoom>> rooms,
        string actor, CancellationToken cancellationToken)
    {
        var timetables = await context.SchoolTimetables.IgnoreQueryFilters()
            .Include(x => x.Entries)
            .Where(x => x.SchoolId == school.Id && x.AcademicYearId == year.Id && x.Semester == TimetableSemester.First)
            .OrderBy(x => x.TimetableSetupProfileId == setup.Id ? 0 : 1).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var timetable = timetables.FirstOrDefault();
        if (timetable is null)
        {
            timetable = new SchoolTimetable
            {
                SchoolId = school.Id,
                AcademicYearId = year.Id,
                Semester = TimetableSemester.First,
                TimetableSetupProfileId = setup.Id,
                CreatedByUserId = actor,
                UpdatedByUserId = actor
            };
            context.SchoolTimetables.Add(timetable);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var extra in timetables.Where(x => x != timetable && !x.IsDeleted))
        {
            extra.IsDeleted = true;
            extra.DeletedAt = DateTimeOffset.UtcNow;
            extra.DeletedByUserId = actor;
        }

        foreach (var oldEntry in timetable.Entries.Where(x => !x.IsDeleted))
        {
            oldEntry.IsDeleted = true;
            oldEntry.DeletedAt = DateTimeOffset.UtcNow;
        }
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        timetable.Title = TimetableTitle;
        timetable.TimetableSetupProfileId = setup.Id;
        timetable.BellScheduleRevisionId = schedule.Id;
        timetable.TimingsRequireRevalidation = false;
        timetable.SetupRevision = setup.Revision;
        timetable.IsPublished = true;
        timetable.PublishedAt = DateTimeOffset.UtcNow;
        timetable.PublishedByUserId = actor;
        timetable.Revision++;
        timetable.UpdatedAt = DateTimeOffset.UtcNow;
        timetable.UpdatedByUserId = actor;
        timetable.IsDeleted = false;
        timetable.DeletedAt = null;
        timetable.DeletedByUserId = null;

        foreach (var planned in allocation.Entries)
        {
            var roomId = ResolveRoomId(planned.Plan, rooms);
            timetable.Entries.Add(new SchoolTimetableEntry
            {
                SchoolId = school.Id,
                ClassroomId = planned.Plan.Classroom.Id,
                InstructorProfileId = planned.Teacher.Profile.InstructorProfileId,
                Day = (TimetableDay)planned.Occurrence.Day,
                Period = planned.Occurrence.Period,
                EntryType = TimetableEntryType.Lesson,
                ClassLabel = planned.Plan.Classroom.ClassLabel,
                Subject = planned.Plan.SubjectDefinition.Name,
                SubjectId = planned.Plan.SubjectDefinition.Id,
                ClassSubjectRequirementId = planned.Plan.Requirement.Id,
                RoomId = roomId
            });
        }

        // The green substitute is deliberately on standby in the source slot.
        // Daily cover suppresses that standby before validation, exercising that branch.
        timetable.Entries.Add(new SchoolTimetableEntry
        {
            SchoolId = school.Id,
            InstructorProfileId = allocation.States[17].Profile.InstructorProfileId,
            Day = TimetableDay.Sunday,
            Period = 7,
            EntryType = TimetableEntryType.Standby
        });

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return timetable;
    }

    private static int? ResolveRoomId(RequirementPlan plan, IReadOnlyDictionary<string, IReadOnlyList<TimetableRoom>> rooms)
    {
        // Preferred rooms remain a soft preference and are deliberately left
        // unpinned in the generated baseline. Required computer-lab lessons are
        // pinned and therefore exercise the hard room-validation branch.
        if (plan.Subject.RoomKind is null || !plan.Subject.RoomIsRequired) return null;
        var choices = rooms[plan.Subject.RoomKind];
        return choices[plan.ClassroomIndex >= 5 ? 1 : 0].Id;
    }

    private sealed record RequirementPlan(
        int ClassroomIndex,
        Classroom Classroom,
        TimetableSeedSubject Subject,
        SubjectDefinition SubjectDefinition,
        ClassSubjectRequirement Requirement,
        IReadOnlyList<TimetableSeedOccurrence> Occurrences);

    private sealed class TeacherState(TeacherTimetableProfile profile, InstructorProfile instructor)
    {
        public TeacherTimetableProfile Profile { get; } = profile;
        public InstructorProfile Instructor { get; } = instructor;
        public HashSet<(int Day, int Period)> Occupied { get; } = [];
        public int Load { get; set; }
        public bool CanTeach(IEnumerable<TimetableSeedOccurrence> occurrences) =>
            occurrences.All(x => !Occupied.Contains((x.Day, x.Period)));
    }

    private sealed record TeacherAllocation(TeacherState Teacher, IReadOnlyList<TimetableSeedOccurrence> Occurrences);
    private sealed record PlannedEntry(RequirementPlan Plan, TeacherState Teacher, TimetableSeedOccurrence Occurrence);
    private sealed record AllocationResult(IReadOnlyList<TeacherState> States, IReadOnlyList<PlannedEntry> Entries);
}
