using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AlFalah.Infrastructure.Data.Seeders;

/// <summary>
/// Provisions a deterministic, development-only school context for end-to-end testing.
/// Program.cs is the environment boundary; this seeder must never be invoked in production.
/// </summary>
public sealed class StudentAffairsDataSeeder
{
    private const string DefaultDevelopmentPassword = "Test@1234";
    private const string TestSchoolName = "Al-Falah E2E Test School";
    private const string TestSchoolCity = "Cairo";
    private const string TestStudentNumber = "E2E-STUDENT-001";
    private const string TestClassLabel = "E2E-1-A";

    private static readonly TestAccount[] TestAccounts =
    {
        new("admin.test", "admin.test@alfalah.test", "E2E", "School Manager", RoleNames.SchoolManager),
        new("officer.test", "officer.test@alfalah.test", "E2E", "Student Affairs Officer", RoleNames.StudentAffairsOfficer),
        new("socialworker.test", "socialworker.test@alfalah.test", "E2E", "Social Worker", RoleNames.SocialWorker),
        new("secretary.test", "secretary.test@alfalah.test", "E2E", "Secretary", RoleNames.Secretary),
        new("guard.test", "guard.test@alfalah.test", "E2E", "Security Guard", RoleNames.SecurityGuard),
        new("teacher.test", "teacher.test@alfalah.test", "E2E", "Teacher", RoleNames.Instructor),
        new("parent.test", "parent.test@alfalah.test", "E2E", "Parent", RoleNames.Guardian)
    };

    private readonly AlFalahDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly TimeProvider _timeProvider;
    private readonly ITimetableReviewRepository _timetableReviewRepository;
    private readonly TimetableValidationEngine _timetableValidator;
    private readonly TimetableRepairEngine _timetableRepair;
    private readonly ILogger<StudentAffairsDataSeeder> _logger;
    private readonly string _testPassword;
    private readonly bool _seedShowcaseData;

    public StudentAffairsDataSeeder(
        AlFalahDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        TimeProvider timeProvider,
        ITimetableReviewRepository timetableReviewRepository,
        TimetableValidationEngine timetableValidator,
        TimetableRepairEngine timetableRepair,
        IConfiguration configuration,
        ILogger<StudentAffairsDataSeeder> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _timeProvider = timeProvider;
        _timetableReviewRepository = timetableReviewRepository;
        _timetableValidator = timetableValidator;
        _timetableRepair = timetableRepair;
        _logger = logger;
        _testPassword = configuration["ALFALAH_E2E_PASSWORD"] ?? DefaultDevelopmentPassword;
        _seedShowcaseData = string.IsNullOrWhiteSpace(configuration["E2E:FixtureFile"]);
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Ensuring development Student Affairs test data...");

        var now = _timeProvider.GetUtcNow();
        var schoolTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, schoolTimeZone).DateTime);
        var school = await EnsureSchoolAsync(cancellationToken).ConfigureAwait(false);
        var users = await EnsureAccountsAsync(school, cancellationToken).ConfigureAwait(false);

        var manager = users[RoleNames.SchoolManager];
        if (school.ManagerUserId != manager.Id)
        {
            school.ManagerUserId = manager.Id;
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var academicYear = await EnsureAcademicYearAsync(today, cancellationToken).ConfigureAwait(false);
        var academicTerm = await EnsureAcademicTermAsync(
            school,
            academicYear,
            manager.Id,
            cancellationToken).ConfigureAwait(false);
        var classroom = await EnsureClassroomAsync(
            school,
            academicYear,
            manager.Id,
            cancellationToken).ConfigureAwait(false);
        var instructorProfile = await EnsureInstructorProfileAsync(
            users[RoleNames.Instructor],
            school,
            cancellationToken).ConfigureAwait(false);
        var substituteAccount = new TestAccount(
            "substitute.teacher.test",
            "substitute.teacher.test@alfalah.test",
            "E2E",
            "Substitute Teacher",
            RoleNames.Instructor);
        var substituteUser = await EnsureUserAsync(substituteAccount, cancellationToken).ConfigureAwait(false);
        await EnsureSchoolAssignmentAsync(substituteUser, school, RoleNames.Instructor, cancellationToken).ConfigureAwait(false);
        var substituteProfile = await EnsureInstructorProfileAsync(
            substituteUser,
            school,
            cancellationToken,
            "E2E-TEACHER-002").ConfigureAwait(false);

        await EnsurePublishedTimetableAsync(
            school,
            academicYear,
            classroom,
            instructorProfile,
            substituteProfile,
            manager.Id,
            today,
            cancellationToken).ConfigureAwait(false);
        await EnsureGuardianContextAsync(
            school,
            academicTerm,
            classroom,
            users[RoleNames.Guardian],
            manager.Id,
            today,
            cancellationToken).ConfigureAwait(false);
        if (_seedShowcaseData)
        {
            await EnsureShowcaseStudentsAsync(
                school,
                academicTerm,
                classroom,
                users[RoleNames.Guardian],
                manager.Id,
                today,
                cancellationToken).ConfigureAwait(false);
        }
        await EnsureStudentAffairsSettingsAsync(
            school,
            manager.Id,
            cancellationToken).ConfigureAwait(false);
        await EnsureSocialWorkerSeedDataAsync(
            school,
            academicTerm,
            users[RoleNames.SocialWorker],
            users[RoleNames.Guardian],
            manager.Id,
            cancellationToken).ConfigureAwait(false);
        if (_seedShowcaseData)
        {
            await EnsureShowcaseWorkflowDataAsync(
                school,
                academicTerm,
                classroom,
                instructorProfile,
                users[RoleNames.StudentAffairsOfficer],
                users[RoleNames.Guardian],
                users[RoleNames.Secretary],
                manager.Id,
                today,
                cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "Development Student Affairs test data is ready for school {SchoolName} (Id: {SchoolId}).",
            school.Name,
            school.Id);
    }

    private async Task<School> EnsureSchoolAsync(CancellationToken cancellationToken)
    {
        var school = await _context.Schools
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                candidate => candidate.Name == TestSchoolName && candidate.City == TestSchoolCity,
                cancellationToken)
            .ConfigureAwait(false);

        if (school is null)
        {
            school = new School
            {
                Name = TestSchoolName,
                Stage = SchoolStage.Primary,
                City = TestSchoolCity,
                LocationDetails = "Development seed data",
                IsActive = true
            };
            _context.Schools.Add(school);
        }
        else
        {
            school.Stage = SchoolStage.Primary;
            school.LocationDetails = "Development seed data";
            school.IsActive = true;
            school.IsDeleted = false;
            school.DeletedAt = null;
            school.DeletedByUserId = null;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return school;
    }

    private async Task<Dictionary<string, ApplicationUser>> EnsureAccountsAsync(
        School school,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, ApplicationUser>(StringComparer.Ordinal);

        foreach (var account in TestAccounts)
        {
            var user = await EnsureUserAsync(account, cancellationToken).ConfigureAwait(false);
            await EnsureSchoolAssignmentAsync(user, school, account.Role, cancellationToken).ConfigureAwait(false);
            result.Add(account.Role, user);
        }

        return result;
    }

    private async Task<ApplicationUser> EnsureUserAsync(
        TestAccount account,
        CancellationToken cancellationToken)
    {
        var normalizedUserName = _userManager.NormalizeName(account.UserName);
        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                candidate => candidate.NormalizedUserName == normalizedUserName,
                cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = account.UserName,
                Email = account.Email,
                EmailConfirmed = true,
                FirstName = account.FirstName,
                LastName = account.LastName,
                PreferredLanguage = "ar",
                IsActive = true
            };

            EnsureIdentitySuccess(
                await _userManager.CreateAsync(user, _testPassword).ConfigureAwait(false),
                $"create {account.UserName}");
        }
        else
        {
            user.UserName = account.UserName;
            user.Email = account.Email;
            user.EmailConfirmed = true;
            user.FirstName = account.FirstName;
            user.LastName = account.LastName;
            user.PreferredLanguage = "ar";
            user.IsActive = true;
            user.IsDeleted = false;
            user.DeletedAt = null;
            user.DeletedByUserId = null;
            user.LockoutEnd = null;
            user.AccessFailedCount = 0;

            EnsureIdentitySuccess(
                await _userManager.UpdateAsync(user).ConfigureAwait(false),
                $"update {account.UserName}");
        }

        if (!await _userManager.CheckPasswordAsync(user, _testPassword).ConfigureAwait(false))
        {
            if (!string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                EnsureIdentitySuccess(
                    await _userManager.RemovePasswordAsync(user).ConfigureAwait(false),
                    $"remove old password for {account.UserName}");
            }

            EnsureIdentitySuccess(
                await _userManager.AddPasswordAsync(user, _testPassword).ConfigureAwait(false),
                $"set password for {account.UserName}");
        }

        if (!await _userManager.IsInRoleAsync(user, account.Role).ConfigureAwait(false))
        {
            EnsureIdentitySuccess(
                await _userManager.AddToRoleAsync(user, account.Role).ConfigureAwait(false),
                $"assign {account.Role} to {account.UserName}");
        }

        return user;
    }

    private async Task EnsureSchoolAssignmentAsync(
        ApplicationUser user,
        School school,
        string roleName,
        CancellationToken cancellationToken)
    {
        var role = await _roleManager.FindByNameAsync(roleName).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Required seed role '{roleName}' does not exist.");

        var assignment = await _context.UserSchoolRoles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                candidate => candidate.UserId == user.Id
                    && candidate.SchoolId == school.Id
                    && candidate.RoleId == role.Id,
                cancellationToken)
            .ConfigureAwait(false);

        if (assignment is null)
        {
            _context.UserSchoolRoles.Add(new UserSchoolRole
            {
                UserId = user.Id,
                SchoolId = school.Id,
                RoleId = role.Id,
                IsActive = true,
                IsDeleted = false
            });
        }
        else
        {
            assignment.IsActive = true;
            assignment.IsDeleted = false;
            assignment.DeletedAt = null;
            assignment.DeletedByUserId = null;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<AcademicYear> EnsureAcademicYearAsync(
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var academicYear = await _context.AcademicYears
            .OrderByDescending(candidate => candidate.IsActive)
            .FirstOrDefaultAsync(
                candidate => candidate.StartsOn <= today && candidate.EndsOn >= today,
                cancellationToken)
            .ConfigureAwait(false);

        if (academicYear is null)
        {
            academicYear = new AcademicYear
            {
                Code = $"DEV-E2E-{today.Year}",
                NameAr = $"Development E2E {today.Year}",
                StartsOn = today.AddMonths(-1),
                EndsOn = today.AddMonths(11),
                IsActive = true
            };
            _context.AcademicYears.Add(academicYear);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return academicYear;
    }

    private async Task<AcademicTerm> EnsureAcademicTermAsync(
        School school,
        AcademicYear academicYear,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var term = await _context.AcademicTerms
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                candidate => candidate.SchoolId == school.Id
                    && candidate.AcademicYearId == academicYear.Id
                    && candidate.Semester == TimetableSemester.First,
                cancellationToken)
            .ConfigureAwait(false);

        if (term is null)
        {
            term = new AcademicTerm
            {
                SchoolId = school.Id,
                AcademicYearId = academicYear.Id,
                Semester = TimetableSemester.First,
                StartsOn = academicYear.StartsOn,
                EndsOn = academicYear.EndsOn,
                IsActive = true,
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            };
            _context.AcademicTerms.Add(term);
        }
        else
        {
            term.StartsOn = academicYear.StartsOn;
            term.EndsOn = academicYear.EndsOn;
            term.IsActive = true;
            term.UpdatedByUserId = actorUserId;
            term.IsDeleted = false;
            term.DeletedAt = null;
            term.DeletedByUserId = null;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return term;
    }

    private async Task<Classroom> EnsureClassroomAsync(
        School school,
        AcademicYear academicYear,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var classroom = await _context.Classrooms
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                candidate => candidate.SchoolId == school.Id
                    && candidate.AcademicYearId == academicYear.Id
                    && candidate.ClassLabel == TestClassLabel,
                cancellationToken)
            .ConfigureAwait(false);

        if (classroom is null)
        {
            classroom = new Classroom
            {
                SchoolId = school.Id,
                AcademicYearId = academicYear.Id,
                Stage = school.Stage,
                GradeLevel = 1,
                Section = "A",
                ClassLabel = TestClassLabel,
                IsActive = true,
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            };
            _context.Classrooms.Add(classroom);
        }
        else
        {
            classroom.Stage = school.Stage;
            classroom.GradeLevel = 1;
            classroom.Section = "A";
            classroom.IsActive = true;
            classroom.UpdatedByUserId = actorUserId;
            classroom.IsDeleted = false;
            classroom.DeletedAt = null;
            classroom.DeletedByUserId = null;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return classroom;
    }

    private async Task<InstructorProfile> EnsureInstructorProfileAsync(
        ApplicationUser instructor,
        School school,
        CancellationToken cancellationToken,
        string employeeNumber = "E2E-TEACHER-001")
    {
        var profile = await _context.InstructorProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(candidate => candidate.UserId == instructor.Id, cancellationToken)
            .ConfigureAwait(false);

        if (profile is null)
        {
            profile = new InstructorProfile
            {
                UserId = instructor.Id,
                SchoolId = school.Id,
                SubjectSpecialization = "Mathematics",
                Stage = school.Stage,
                EmployeeNumber = employeeNumber,
                IsActive = true
            };
            _context.InstructorProfiles.Add(profile);
        }
        else
        {
            profile.SchoolId = school.Id;
            profile.SubjectSpecialization = "Mathematics";
            profile.Stage = school.Stage;
            profile.EmployeeNumber = employeeNumber;
            profile.IsActive = true;
            profile.IsDeleted = false;
            profile.DeletedAt = null;
            profile.DeletedByUserId = null;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return profile;
    }

    private async Task EnsurePublishedTimetableAsync(
        School school,
        AcademicYear academicYear,
        Classroom classroom,
        InstructorProfile instructor,
        InstructorProfile substituteInstructor,
        string actorUserId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var timetable = await _context.SchoolTimetables
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                candidate => candidate.SchoolId == school.Id
                    && candidate.AcademicYearId == academicYear.Id
                    && candidate.Semester == TimetableSemester.First,
                cancellationToken)
            .ConfigureAwait(false);

        if (timetable is null)
        {
            timetable = new SchoolTimetable
            {
                SchoolId = school.Id,
                AcademicYearId = academicYear.Id,
                Semester = TimetableSemester.First,
                Title = "E2E Published Timetable",
                // Development data must pass through the real review/publish boundary.
                // Seeding a live row directly would create no immutable version or analysis.
                IsPublished = false,
                Revision = 1,
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            };
            _context.SchoolTimetables.Add(timetable);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var hasPublishedSnapshot = await _context.SchoolTimetableVersions
                .AsNoTracking()
                .AnyAsync(version => version.SchoolTimetableId == timetable.Id &&
                    version.ChangeKind == TimetableChangeKind.Published, cancellationToken)
                .ConfigureAwait(false);
            if (timetable.IsPublished && hasPublishedSnapshot)
            {
                await EnsureDatedSubstitutionAsync(
                    school,
                    timetable,
                    instructor,
                    substituteInstructor,
                    actorUserId,
                    today,
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            timetable.Title = "E2E Published Timetable";
            // Legacy development rows were marked live without a publication snapshot.
            // Their original state cannot be reconstructed safely, so quarantine them as
            // drafts instead of presenting a mutable/invalid timetable as operational.
            timetable.IsPublished = false;
            timetable.PublishedAt = null;
            timetable.PublishedByUserId = null;
            timetable.UpdatedByUserId = actorUserId;
            timetable.IsDeleted = false;
            timetable.DeletedAt = null;
            timetable.DeletedByUserId = null;
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (!timetable.BellScheduleRevisionId.HasValue)
        {
            var template = new BellScheduleTemplate { SchoolId = school.Id, AcademicYearId = academicYear.Id,
                Semester = TimetableSemester.First, Name = "توقيت المدرسة التجريبية", CreatedByUserId = actorUserId, UpdatedByUserId = actorUserId };
            var revision = new BellScheduleRevision { SchoolId = school.Id, Template = template, Revision = 1,
                Name = template.Name, SchoolTimeZoneId = "Africa/Cairo", CreatedByUserId = actorUserId };
            var defaults = new BellScheduleDay { Day = 0, IsStudyDay = true };
            for (var sequence = 1; sequence <= 6; sequence++)
            {
                var start = new TimeOnly(7, 0).AddMinutes((sequence - 1) * 50 + (sequence > 3 ? 20 : 0));
                defaults.Periods.Add(new BellPeriod { Sequence = sequence, DisplayLabel = $"الحصة {sequence}", StartLocalTime = start, EndLocalTime = start.AddMinutes(45) });
            }
            defaults.Breaks.Add(new ScheduleBreakDefinition
            {
                Name = "E2E Recess",
                Category = "Recess",
                CreatedByUserId = actorUserId,
                Window = new ScheduleBreakWindow
                {
                    StartLocalTime = new TimeOnly(9, 25),
                    EndLocalTime = new TimeOnly(9, 45)
                }
            });
            revision.Days.Add(defaults);
            foreach (var day in Enumerable.Range(1, 7)) revision.Days.Add(new() { Day = day, IsStudyDay = day is >= 2 and <= 6, UsesDefaultSchedule = true });
            _context.Add(revision);
            var profile = new TimetableSetupProfile { SchoolId = school.Id, AcademicYearId = academicYear.Id, Semester = TimetableSemester.First,
                Name = "إعداد المدرسة التجريبية", BellScheduleTemplate = template, CreatedByUserId = actorUserId, UpdatedByUserId = actorUserId };
            _context.Add(profile);
            timetable.TimetableSetupProfile = profile;
            timetable.BellScheduleRevision = revision;
            await _context.SaveChangesAsync(cancellationToken);
        }

        var source = await EnsureFixtureTimetableSourcesAsync(
            school,
            timetable,
            classroom,
            instructor,
            substituteInstructor,
            actorUserId,
            cancellationToken).ConfigureAwait(false);

        var existingEntries = await _context.SchoolTimetableEntries
            .IgnoreQueryFilters()
            .Where(candidate => candidate.SchoolId == school.Id
                && candidate.SchoolTimetableId == timetable.Id
                && candidate.InstructorProfileId == instructor.Id)
            .ToDictionaryAsync(candidate => (candidate.Day, candidate.Period), cancellationToken)
            .ConfigureAwait(false);

        foreach (var old in existingEntries.Values.Where(x => x.Day is TimetableDay.Saturday or TimetableDay.Friday || x.Period is not (1 or 3)))
        { old.IsDeleted = true; old.DeletedAt = _timeProvider.GetUtcNow(); }
        foreach (var day in new[] { TimetableDay.Sunday, TimetableDay.Monday, TimetableDay.Tuesday, TimetableDay.Wednesday, TimetableDay.Thursday })
        {
            foreach (var period in new[] { 1, 3 })
            {
                if (!existingEntries.TryGetValue((day, period), out var entry))
                {
                    _context.SchoolTimetableEntries.Add(new SchoolTimetableEntry
                    {
                        SchoolId = school.Id,
                        SchoolTimetableId = timetable.Id,
                        ClassroomId = classroom.Id,
                        InstructorProfileId = instructor.Id,
                        Day = day,
                        Period = period,
                        EntryType = TimetableEntryType.Lesson,
                        ClassLabel = classroom.ClassLabel,
                        Subject = source.Subject.Name,
                        SubjectId = source.Subject.Id,
                        ClassSubjectRequirementId = source.Requirement.Id
                    });
                    continue;
                }

                entry.ClassroomId = classroom.Id;
                entry.EntryType = TimetableEntryType.Lesson;
                entry.ClassLabel = classroom.ClassLabel;
                entry.Subject = source.Subject.Name;
                entry.SubjectId = source.Subject.Id;
                entry.ClassSubjectRequirementId = source.Requirement.Id;
                entry.IsDeleted = false;
                entry.DeletedAt = null;
            }
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var review = new TimetableReviewService(
            _timetableReviewRepository,
            _timetableValidator,
            _timetableRepair,
            new FixtureCurrentUser(actorUserId, school.Id));
        await review.PublishAsync(timetable.Id, timetable.Revision, cancellationToken).ConfigureAwait(false);
        await EnsureDatedSubstitutionAsync(
            school,
            timetable,
            instructor,
            substituteInstructor,
            actorUserId,
            today,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<FixtureTimetableSource> EnsureFixtureTimetableSourcesAsync(
        School school,
        SchoolTimetable timetable,
        Classroom classroom,
        InstructorProfile instructor,
        InstructorProfile substituteInstructor,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var setupId = timetable.TimetableSetupProfileId
            ?? throw new InvalidOperationException("The E2E timetable has no setup profile.");
        var bellRevisionId = timetable.BellScheduleRevisionId
            ?? throw new InvalidOperationException("The E2E timetable has no bell revision.");

        var subject = await _context.Set<SubjectDefinition>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                candidate => candidate.SchoolId == school.Id && candidate.Name == "E2E Mathematics",
                cancellationToken)
            .ConfigureAwait(false);
        if (subject is null)
        {
            subject = new SubjectDefinition
            {
                SchoolId = school.Id,
                Name = "E2E Mathematics",
                UpdatedByUserId = actorUserId
            };
            _context.Add(subject);
        }
        subject.IsActive = true;
        subject.IsDeleted = false;
        subject.UpdatedByUserId = actorUserId;

        var profiles = await _context.TeacherTimetableProfiles
            .Where(candidate => candidate.SchoolId == school.Id &&
                candidate.TimetableSetupProfileId == setupId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        TeacherTimetableProfile EnsureTeacherProfile(InstructorProfile candidate, string displayName)
        {
            var profile = profiles.FirstOrDefault(item => item.InstructorProfileId == candidate.Id);
            if (profile is null)
            {
                profile = new TeacherTimetableProfile
                {
                    SchoolId = school.Id,
                    TimetableSetupProfileId = setupId,
                    InstructorProfileId = candidate.Id,
                    CreatedAt = _timeProvider.GetUtcNow(),
                    CreatedByUserId = actorUserId
                };
                profiles.Add(profile);
                _context.Add(profile);
            }
            profile.BellScheduleRevisionId = bellRevisionId;
            profile.ShortDisplayName = displayName;
            profile.MaximumWeeklyPeriods = 24;
            profile.UpdatedAt = _timeProvider.GetUtcNow();
            profile.UpdatedByUserId = actorUserId;
            return profile;
        }

        var primaryProfile = EnsureTeacherProfile(instructor, "E2E Teacher");
        var substituteProfile = EnsureTeacherProfile(substituteInstructor, "E2E Substitute");
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // This setup profile belongs to the deterministic Student Affairs fixture. Other
        // development QA flows can leave availability overrides behind on the same test
        // accounts, which would make the next startup unable to republish its own schedule.
        var fixtureProfileIds = new[] { primaryProfile.Id, substituteProfile.Id };
        var availabilityOverrides = await _context.TeacherAvailabilitySlots
            .Where(slot => fixtureProfileIds.Contains(slot.TeacherTimetableProfileId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var slot in availabilityOverrides)
            slot.IsAvailable = true;

        var requirement = await _context.Set<ClassSubjectRequirement>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                candidate => candidate.SchoolId == school.Id &&
                    candidate.TimetableSetupProfileId == setupId &&
                    candidate.ClassroomId == classroom.Id &&
                    candidate.SubjectId == subject.Id,
                cancellationToken)
            .ConfigureAwait(false);
        if (requirement is null)
        {
            requirement = new ClassSubjectRequirement
            {
                SchoolId = school.Id,
                TimetableSetupProfileId = setupId,
                ClassroomId = classroom.Id,
                SubjectId = subject.Id
            };
            _context.Add(requirement);
        }
        requirement.IndividualPeriodCount = 10;
        requirement.PairedBlockCount = 0;
        requirement.TimePreference = "None";
        requirement.IsDeleted = false;
        requirement.UpdatedAt = _timeProvider.GetUtcNow();
        requirement.UpdatedByUserId = actorUserId;
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Keep the fixture profile isolated from subject requirements created by other
        // timetable QA scenarios. They have their own fixtures and must not prevent this
        // deterministic schedule from passing the real publish validation boundary.
        var unrelatedRequirements = await _context.Set<ClassSubjectRequirement>()
            .IgnoreQueryFilters()
            .Where(candidate => candidate.TimetableSetupProfileId == setupId
                && candidate.Id != requirement.Id
                && !candidate.IsDeleted)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var unrelatedRequirementIds = unrelatedRequirements.Select(candidate => candidate.Id).ToArray();
        if (unrelatedRequirementIds.Length > 0)
        {
            foreach (var unrelated in unrelatedRequirements)
            {
                unrelated.IsDeleted = true;
                unrelated.UpdatedAt = _timeProvider.GetUtcNow();
                unrelated.UpdatedByUserId = actorUserId;
            }

            var unrelatedAssignments = await _context.Set<TeachingAssignment>()
                .IgnoreQueryFilters()
                .Where(candidate => unrelatedRequirementIds.Contains(candidate.ClassSubjectRequirementId)
                    && !candidate.IsDeleted)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var unrelated in unrelatedAssignments)
            {
                unrelated.IsDeleted = true;
                unrelated.UpdatedAt = _timeProvider.GetUtcNow();
                unrelated.UpdatedByUserId = actorUserId;
            }

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var assignments = await _context.Set<TeachingAssignment>()
            .IgnoreQueryFilters()
            .Include(candidate => candidate.Members)
            .Where(candidate => candidate.SchoolId == school.Id &&
                candidate.TimetableSetupProfileId == setupId &&
                candidate.ClassSubjectRequirementId == requirement.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var assignment = assignments.FirstOrDefault();
        if (assignment is null)
        {
            assignment = new TeachingAssignment
            {
                SchoolId = school.Id,
                TimetableSetupProfileId = setupId,
                ClassSubjectRequirementId = requirement.Id
            };
            _context.Add(assignment);
        }
        foreach (var duplicate in assignments.Skip(1)) duplicate.IsDeleted = true;
        assignment.Mode = "SingleTeacher";
        assignment.IsDeleted = false;
        assignment.UpdatedAt = _timeProvider.GetUtcNow();
        assignment.UpdatedByUserId = actorUserId;
        var member = assignment.Members.FirstOrDefault(item => item.TeacherTimetableProfileId == primaryProfile.Id);
        if (member is null)
        {
            member = new TeachingAssignmentMember
            {
                SchoolId = school.Id,
                TimetableSetupProfileId = setupId,
                TeacherTimetableProfileId = primaryProfile.Id
            };
            assignment.Members.Add(member);
        }
        member.AllocatedPeriodCount = 10;
        member.AllocatedPairedBlockCount = 0;
        foreach (var extra in assignment.Members.Where(item => item != member).ToArray())
            _context.Remove(extra);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new FixtureTimetableSource(subject, requirement);
    }

    private async Task EnsureDatedSubstitutionAsync(
        School school,
        SchoolTimetable timetable,
        InstructorProfile instructor,
        InstructorProfile substituteInstructor,
        string actorUserId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var substitutionDate = today.DayOfWeek switch
        {
            DayOfWeek.Friday => today.AddDays(2),
            DayOfWeek.Saturday => today.AddDays(1),
            _ => today
        };
        var timetableDay = substitutionDate.DayOfWeek switch
        {
            DayOfWeek.Sunday => TimetableDay.Sunday,
            DayOfWeek.Monday => TimetableDay.Monday,
            DayOfWeek.Tuesday => TimetableDay.Tuesday,
            DayOfWeek.Wednesday => TimetableDay.Wednesday,
            DayOfWeek.Thursday => TimetableDay.Thursday,
            _ => throw new InvalidOperationException("The E2E substitution must be on a study day.")
        };
        var entry = await _context.SchoolTimetableEntries
            .AsNoTracking()
            .FirstAsync(candidate => candidate.SchoolTimetableId == timetable.Id &&
                candidate.Day == timetableDay && candidate.Period == 1 && !candidate.IsDeleted,
                cancellationToken)
            .ConfigureAwait(false);
        var alreadyExists = await _context.TimetableSubstitutions
            .AsNoTracking()
            .AnyAsync(candidate => candidate.SchoolId == school.Id &&
                candidate.SchoolTimetableId == timetable.Id &&
                candidate.LocalDate == substitutionDate &&
                candidate.ProposalId == "E2E-SUBSTITUTION",
                cancellationToken)
            .ConfigureAwait(false);
        if (alreadyExists) return;

        var publishedVersionId = await _context.SchoolTimetableVersions
            .AsNoTracking()
            .Where(candidate => candidate.SchoolTimetableId == timetable.Id &&
                candidate.ChangeKind == TimetableChangeKind.Published)
            .OrderByDescending(candidate => candidate.VersionNumber)
            .Select(candidate => candidate.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (publishedVersionId == 0)
            throw new InvalidOperationException("The E2E timetable publication snapshot was not created.");

        var requestBytes = new byte[16];
        BitConverter.TryWriteBytes(requestBytes.AsSpan(0, 4), school.Id);
        BitConverter.TryWriteBytes(requestBytes.AsSpan(4, 4), substitutionDate.DayNumber);
        requestBytes[15] = 1;
        _context.TimetableSubstitutions.Add(new TimetableSubstitution
        {
            SchoolId = school.Id,
            SchoolTimetableId = timetable.Id,
            RequestId = new Guid(requestBytes),
            ProposalId = "E2E-SUBSTITUTION",
            Kind = "Substitution",
            LocalDate = substitutionDate,
            BeforeRevision = timetable.Revision,
            AfterRevision = timetable.Revision,
            RequestedByUserId = actorUserId,
            ApprovedByUserId = actorUserId,
            ConfirmedAt = _timeProvider.GetUtcNow(),
            SchoolTimetableVersionId = publishedVersionId,
            Movements =
            [
                new TimetableSubstitutionMovement
                {
                    SchoolId = school.Id,
                    SchoolTimetableEntryId = entry.Id,
                    FromTeacherId = instructor.Id,
                    ToTeacherId = substituteInstructor.Id,
                    Day = (int)timetableDay,
                    ToDay = (int)timetableDay,
                    FromPeriod = entry.Period,
                    ToPeriod = entry.Period
                }
            ]
        });
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureGuardianContextAsync(
        School school,
        AcademicTerm academicTerm,
        Classroom classroom,
        ApplicationUser guardianUser,
        string actorUserId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var guardian = await _context.GuardianProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                candidate => candidate.SchoolId == school.Id
                    && candidate.ApplicationUserId == guardianUser.Id,
                cancellationToken)
            .ConfigureAwait(false);

        if (guardian is null)
        {
            guardian = new GuardianProfile
            {
                SchoolId = school.Id,
                ApplicationUserId = guardianUser.Id,
                PreferredContactLanguage = PreferredContactLanguage.Arabic,
                IsActive = true,
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            };
            _context.GuardianProfiles.Add(guardian);
        }
        else
        {
            guardian.PreferredContactLanguage = PreferredContactLanguage.Arabic;
            guardian.IsActive = true;
            guardian.UpdatedByUserId = actorUserId;
            guardian.IsDeleted = false;
            guardian.DeletedAt = null;
            guardian.DeletedByUserId = null;
        }

        var student = await _context.Students
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                candidate => candidate.SchoolId == school.Id
                    && candidate.StudentNumber == TestStudentNumber,
                cancellationToken)
            .ConfigureAwait(false);

        if (student is null)
        {
            student = new Student
            {
                SchoolId = school.Id,
                StudentNumber = TestStudentNumber,
                IdentityNumber = "1000000001",
                FirstName = "E2E",
                LastName = "Student",
                Gender = StudentGender.Male,
                IsActive = true,
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            };
            _context.Students.Add(student);
        }
        else
        {
            student.IdentityNumber = "1000000001";
            student.FirstName = "E2E";
            student.LastName = "Student";
            student.IsActive = true;
            student.UpdatedByUserId = actorUserId;
            student.IsDeleted = false;
            student.DeletedAt = null;
            student.DeletedByUserId = null;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var link = await _context.StudentGuardians
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                candidate => candidate.SchoolId == school.Id
                    && candidate.StudentId == student.Id
                    && candidate.GuardianProfileId == guardian.Id,
                cancellationToken)
            .ConfigureAwait(false);

        if (link is null)
        {
            link = new StudentGuardian
            {
                SchoolId = school.Id,
                StudentId = student.Id,
                GuardianProfileId = guardian.Id,
                RelationshipType = GuardianRelationshipType.Father,
                IsPrimary = true,
                ReceivesNotifications = true,
                CanSubmitExcuses = true,
                CanRequestGatePass = true,
                ValidFrom = academicTerm.StartsOn,
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            };
            _context.StudentGuardians.Add(link);
        }
        else
        {
            link.RelationshipType = GuardianRelationshipType.Father;
            link.IsPrimary = true;
            link.ReceivesNotifications = true;
            link.CanSubmitExcuses = true;
            link.CanRequestGatePass = true;
            link.ValidFrom = academicTerm.StartsOn;
            link.ValidTo = null;
            link.UpdatedByUserId = actorUserId;
            link.IsDeleted = false;
            link.DeletedAt = null;
            link.DeletedByUserId = null;
        }

        var enrollment = await _context.StudentEnrollments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                candidate => candidate.SchoolId == school.Id
                    && candidate.StudentId == student.Id
                    && candidate.AcademicTermId == academicTerm.Id,
                cancellationToken)
            .ConfigureAwait(false);

        if (enrollment is null)
        {
            enrollment = new StudentEnrollment
            {
                SchoolId = school.Id,
                StudentId = student.Id,
                ClassroomId = classroom.Id,
                AcademicTermId = academicTerm.Id,
                RollNumber = 1,
                EnrolledOn = today < academicTerm.StartsOn ? academicTerm.StartsOn : today,
                Status = StudentEnrollmentStatus.Active,
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            };
            _context.StudentEnrollments.Add(enrollment);
        }
        else
        {
            enrollment.ClassroomId = classroom.Id;
            enrollment.RollNumber = 1;
            enrollment.WithdrawnOn = null;
            enrollment.Status = StudentEnrollmentStatus.Active;
            enrollment.UpdatedByUserId = actorUserId;
            enrollment.IsDeleted = false;
            enrollment.DeletedAt = null;
            enrollment.DeletedByUserId = null;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureStudentAffairsSettingsAsync(
        School school,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var settings = await _context.SchoolStudentAffairsSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(candidate => candidate.SchoolId == school.Id, cancellationToken)
            .ConfigureAwait(false);

        if (settings is null)
        {
            _context.SchoolStudentAffairsSettings.Add(new SchoolStudentAffairsSettings
            {
                SchoolId = school.Id,
                MorningDelayThresholdPerTerm = 10,
                BehaviorIncidentMultiplePerTerm = 10,
                AcademicConcernThresholdPerTerm = 3,
                ClassroomEntryPermitThresholdPerTerm = 5,
                AbsenceVisualAlertThresholdPerTerm = 3,
                AbsenceReferralThresholdPerTerm = 5,
                AbsenceChildRightsThresholdPerTerm = 10,
                BehaviorCountabilityPolicy = "ApprovedOnly",
                ArrivalCutoffLocalTime = new TimeOnly(7, 0),
                ArrivalGraceMinutes = 10,
                Version = 1,
                EffectiveFrom = _timeProvider.GetUtcNow(),
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            });
        }
        else
        {
            settings.UpdatedByUserId = actorUserId;
            settings.IsDeleted = false;
            settings.DeletedAt = null;
            settings.DeletedByUserId = null;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureShowcaseStudentsAsync(
        School school,
        AcademicTerm academicTerm,
        Classroom classroom,
        ApplicationUser guardianUser,
        string actorUserId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var guardianProfile = await _context.GuardianProfiles
            .IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.SchoolId == school.Id
                && candidate.ApplicationUserId == guardianUser.Id, cancellationToken)
            .ConfigureAwait(false);
        var showcaseStudents = new[]
        {
            new { Number = "DEMO-101", Identity = "1000000101", First = "سلمان", Last = "العتيبي", Gender = StudentGender.Male },
            new { Number = "DEMO-102", Identity = "1000000102", First = "ليان", Last = "القحطاني", Gender = StudentGender.Female },
            new { Number = "DEMO-103", Identity = "1000000103", First = "عبدالله", Last = "الشهري", Gender = StudentGender.Male },
            new { Number = "DEMO-104", Identity = "1000000104", First = "جود", Last = "الحربي", Gender = StudentGender.Female },
            new { Number = "DEMO-105", Identity = "1000000105", First = "راكان", Last = "المطيري", Gender = StudentGender.Male },
            new { Number = "DEMO-106", Identity = "1000000106", First = "نورة", Last = "الغامدي", Gender = StudentGender.Female },
            new { Number = "DEMO-107", Identity = "1000000107", First = "زياد", Last = "الدوسري", Gender = StudentGender.Male },
            new { Number = "DEMO-108", Identity = "1000000108", First = "تالا", Last = "الزهراني", Gender = StudentGender.Female }
        };

        for (var index = 0; index < showcaseStudents.Length; index++)
        {
            var fixture = showcaseStudents[index];
            var student = await _context.Students
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    candidate => candidate.SchoolId == school.Id && candidate.StudentNumber == fixture.Number,
                    cancellationToken)
                .ConfigureAwait(false);

            if (student is null)
            {
                student = new Student
                {
                    SchoolId = school.Id,
                    StudentNumber = fixture.Number,
                    IdentityNumber = fixture.Identity,
                    FirstName = fixture.First,
                    LastName = fixture.Last,
                    Gender = fixture.Gender,
                    IsActive = true,
                    CreatedByUserId = actorUserId,
                    UpdatedByUserId = actorUserId
                };
                _context.Students.Add(student);
                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                student.IdentityNumber = fixture.Identity;
                student.FirstName = fixture.First;
                student.LastName = fixture.Last;
                student.Gender = fixture.Gender;
                student.IsActive = true;
                student.IsDeleted = false;
                student.DeletedAt = null;
                student.DeletedByUserId = null;
                student.UpdatedByUserId = actorUserId;
            }

            var enrollment = await _context.StudentEnrollments
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    candidate => candidate.SchoolId == school.Id
                        && candidate.StudentId == student.Id
                        && candidate.AcademicTermId == academicTerm.Id,
                    cancellationToken)
                .ConfigureAwait(false);

            if (enrollment is null)
            {
                _context.StudentEnrollments.Add(new StudentEnrollment
                {
                    SchoolId = school.Id,
                    StudentId = student.Id,
                    ClassroomId = classroom.Id,
                    AcademicTermId = academicTerm.Id,
                    RollNumber = index + 3,
                    EnrolledOn = today < academicTerm.StartsOn ? academicTerm.StartsOn : today,
                    Status = StudentEnrollmentStatus.Active,
                    CreatedByUserId = actorUserId,
                    UpdatedByUserId = actorUserId
                });
            }
            else
            {
                enrollment.ClassroomId = classroom.Id;
                enrollment.RollNumber = index + 3;
                enrollment.WithdrawnOn = null;
                enrollment.Status = StudentEnrollmentStatus.Active;
                enrollment.IsDeleted = false;
                enrollment.DeletedAt = null;
                enrollment.DeletedByUserId = null;
                enrollment.UpdatedByUserId = actorUserId;
            }

            // The first six showcase students participate in real Social Worker flows,
            // so each needs an active guardian for summons and case messaging tests.
            if (index < 6)
            {
                var guardianLink = await _context.StudentGuardians.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(candidate => candidate.SchoolId == school.Id
                        && candidate.StudentId == student.Id
                        && candidate.GuardianProfileId == guardianProfile.Id, cancellationToken)
                    .ConfigureAwait(false);
                if (guardianLink is null)
                {
                    _context.StudentGuardians.Add(new StudentGuardian
                    {
                        SchoolId = school.Id,
                        StudentId = student.Id,
                        GuardianProfileId = guardianProfile.Id,
                        RelationshipType = GuardianRelationshipType.LegalGuardian,
                        IsPrimary = index == 0,
                        ReceivesNotifications = true,
                        CanSubmitExcuses = true,
                        CanRequestGatePass = true,
                        ValidFrom = academicTerm.StartsOn,
                        CreatedByUserId = actorUserId,
                        UpdatedByUserId = actorUserId
                    });
                }
                else
                {
                    guardianLink.RelationshipType = GuardianRelationshipType.LegalGuardian;
                    guardianLink.ReceivesNotifications = true;
                    guardianLink.ValidFrom = academicTerm.StartsOn;
                    guardianLink.ValidTo = null;
                    guardianLink.IsDeleted = false;
                    guardianLink.DeletedAt = null;
                    guardianLink.DeletedByUserId = null;
                    guardianLink.UpdatedByUserId = actorUserId;
                }
            }

            if (index < 4)
            {
                var hasShowcaseReferral = await _context.StudentReferrals
                    .IgnoreQueryFilters()
                    .AnyAsync(
                        candidate => candidate.SchoolId == school.Id
                            && candidate.StudentId == student.Id
                            && candidate.AcademicTermId == academicTerm.Id
                            && candidate.SourceType == ReferralSourceType.Manual,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (!hasShowcaseReferral)
                {
                    var reasons = new[]
                    {
                        "متابعة انتظام الطالب بعد تكرار التأخر الصباحي خلال الأسبوعين الماضيين",
                        "دعم الطالبة في التكيف الدراسي والتنسيق مع الأسرة والمعلمات",
                        "خطة متابعة قصيرة لتحسين المشاركة الصفية والالتزام بالواجبات",
                        "تقييم احتياج الطالبة إلى جلسة إرشادية بعد ملاحظة تغير مفاجئ في الأداء"
                    };
                    var priorities = new[]
                    {
                        ReferralPriority.High,
                        ReferralPriority.Normal,
                        ReferralPriority.Normal,
                        ReferralPriority.Critical
                    };
                    var createdAt = _timeProvider.GetUtcNow().AddDays(-(index + 1));
                    _context.StudentReferrals.Add(new StudentReferral
                    {
                        SchoolId = school.Id,
                        StudentId = student.Id,
                        AcademicTermId = academicTerm.Id,
                        SourceType = ReferralSourceType.Manual,
                        Priority = priorities[index],
                        Status = StudentReferralStatus.Open,
                        RecommendedActions = reasons[index],
                        CreatedAt = createdAt,
                        CreatedByUserId = actorUserId,
                        UpdatedAt = createdAt,
                        UpdatedByUserId = actorUserId
                    });
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureShowcaseWorkflowDataAsync(
        School school,
        AcademicTerm academicTerm,
        Classroom classroom,
        InstructorProfile instructorProfile,
        ApplicationUser officerUser,
        ApplicationUser guardianUser,
        ApplicationUser secretaryUser,
        string actorUserId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var students = await _context.Students
            .Where(student => student.SchoolId == school.Id
                && (student.StudentNumber == TestStudentNumber || student.StudentNumber.StartsWith("DEMO-")))
            .OrderBy(student => student.StudentNumber)
            .Take(8)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (students.Count < 5) return;

        var now = _timeProvider.GetUtcNow();
        var guardian = await _context.GuardianProfiles
            .SingleAsync(profile => profile.SchoolId == school.Id
                && profile.ApplicationUserId == guardianUser.Id, cancellationToken)
            .ConfigureAwait(false);

        for (var index = 0; index < 4; index++)
        {
            var localDate = today.AddDays(-(index + 1));
            if (!await _context.MorningArrivalDelays.AnyAsync(delay => delay.SchoolId == school.Id
                    && delay.StudentId == students[index].Id && delay.SchoolLocalDate == localDate,
                cancellationToken).ConfigureAwait(false))
            {
                _context.MorningArrivalDelays.Add(new MorningArrivalDelay
                {
                    SchoolId = school.Id,
                    StudentId = students[index].Id,
                    AcademicTermId = academicTerm.Id,
                    ArrivalAt = now.AddDays(-(index + 1)).AddMinutes(index * 4),
                    SchoolLocalDate = localDate,
                    CutoffTimeSnapshot = new TimeOnly(7, 10),
                    DelayMinutes = 8 + index * 6,
                    Reason = index % 2 == 0 ? "ازدحام مروري في الطريق إلى المدرسة" : "تأخر الحافلة المدرسية",
                    NotificationPolicySnapshot = "ImmediateGuardian",
                    CreatedByUserId = actorUserId,
                    UpdatedByUserId = actorUserId
                });
            }
        }

        if (!await _context.SessionDelays.AnyAsync(item => item.SchoolId == school.Id
            && item.Reason == "بيانات عرض: تأخر بعد الفسحة", cancellationToken).ConfigureAwait(false))
        {
            _context.SessionDelays.AddRange(
                new SessionDelay
                {
                    SchoolId = school.Id, StudentId = students[1].Id, AcademicTermId = academicTerm.Id,
                    ClassroomId = classroom.Id, Period = 3, OccurredAt = now.AddDays(-1), DelayMinutes = 7,
                    Reason = "بيانات عرض: تأخر بعد الفسحة", ReportedByInstructorProfileId = instructorProfile.Id,
                    GuardianNotificationStatus = GuardianNotificationStatus.Delivered,
                    CreatedByUserId = instructorProfile.UserId, UpdatedByUserId = instructorProfile.UserId
                },
                new SessionDelay
                {
                    SchoolId = school.Id, StudentId = students[2].Id, AcademicTermId = academicTerm.Id,
                    ClassroomId = classroom.Id, Period = 2, OccurredAt = now.AddDays(-3), DelayMinutes = 12,
                    Reason = "بيانات عرض: مراجعة شؤون الطلاب", ReportedByInstructorProfileId = instructorProfile.Id,
                    GuardianNotificationStatus = GuardianNotificationStatus.Pending,
                    CreatedByUserId = instructorProfile.UserId, UpdatedByUserId = instructorProfile.UserId
                });
        }

        if (!await _context.BehaviorIncidents.AnyAsync(item => item.SchoolId == school.Id
            && item.Description.StartsWith("بيانات عرض:"), cancellationToken).ConfigureAwait(false))
        {
            _context.BehaviorIncidents.AddRange(
                new BehaviorIncident
                {
                    SchoolId = school.Id, StudentId = students[0].Id, AcademicTermId = academicTerm.Id,
                    ClassroomId = classroom.Id, CategoryCode = "CLASSROOM_CONDUCT", Severity = BehaviorSeverity.Medium,
                    Description = "بيانات عرض: مقاطعة متكررة لشرح المعلم مع استجابة جيدة للتوجيه",
                    OccurredAt = now.AddDays(-2), Location = "الفصل", ReportedByInstructorProfileId = instructorProfile.Id,
                    ImmediateActionTaken = "تنبيه الطالب والاتفاق على قواعد المشاركة",
                    GuardianDispatchDecision = GuardianDispatchDecision.PendingOfficerDecision,
                    CreatedByUserId = instructorProfile.UserId, UpdatedByUserId = instructorProfile.UserId
                },
                new BehaviorIncident
                {
                    SchoolId = school.Id, StudentId = students[3].Id, AcademicTermId = academicTerm.Id,
                    ClassroomId = classroom.Id, CategoryCode = "PEER_CONFLICT", Severity = BehaviorSeverity.High,
                    Description = "بيانات عرض: خلاف بين طالبين أثناء الفسحة يحتاج متابعة هادئة",
                    OccurredAt = now.AddDays(-4), Location = "ساحة المدرسة", ReportedByStaffUserId = officerUser.Id,
                    ImmediateActionTaken = "فصل الطرفين وتوثيق إفادة كل طالب",
                    GuardianDispatchDecision = GuardianDispatchDecision.PendingOfficerDecision,
                    CreatedByUserId = officerUser.Id, UpdatedByUserId = officerUser.Id
                });
        }

        if (!await _context.AcademicConcerns.AnyAsync(item => item.SchoolId == school.Id
            && item.Description.StartsWith("بيانات عرض:"), cancellationToken).ConfigureAwait(false))
        {
            _context.AcademicConcerns.AddRange(
                new AcademicConcern
                {
                    SchoolId = school.Id, StudentId = students[2].Id, AcademicTermId = academicTerm.Id,
                    ClassroomId = classroom.Id, Category = "Homework", Description = "بيانات عرض: عدم اكتمال الواجبات ثلاث مرات خلال أسبوعين",
                    OccurredAt = now.AddDays(-2), ReportedByInstructorProfileId = instructorProfile.Id,
                    GuardianDispatchDecision = GuardianDispatchDecision.PendingOfficerDecision,
                    CreatedByUserId = instructorProfile.UserId, UpdatedByUserId = instructorProfile.UserId
                },
                new AcademicConcern
                {
                    SchoolId = school.Id, StudentId = students[4].Id, AcademicTermId = academicTerm.Id,
                    ClassroomId = classroom.Id, Category = "Participation", Description = "بيانات عرض: انخفاض مفاجئ في المشاركة الصفية",
                    OccurredAt = now.AddDays(-5), ReportedByInstructorProfileId = instructorProfile.Id,
                    GuardianDispatchDecision = GuardianDispatchDecision.Approved,
                    CreatedByUserId = instructorProfile.UserId, UpdatedByUserId = instructorProfile.UserId
                });
        }

        if (!await _context.StudentRecognitions.AnyAsync(item => item.SchoolId == school.Id
            && item.Title.StartsWith("بيانات عرض:"), cancellationToken).ConfigureAwait(false))
        {
            _context.StudentRecognitions.AddRange(
                new StudentRecognition
                {
                    SchoolId = school.Id, StudentId = students[1].Id, AcademicTermId = academicTerm.Id,
                    ClassroomId = classroom.Id, RecognitionType = "AcademicExcellence", Title = "بيانات عرض: تميز في مشروع العلوم",
                    Description = "قدمت الطالبة مشروعًا منظمًا وساعدت فريقها على إتمام العرض.", RecognizedAt = now.AddDays(-1),
                    ReportedByInstructorProfileId = instructorProfile.Id, GuardianNotificationStatus = GuardianNotificationStatus.Delivered,
                    CreatedByUserId = instructorProfile.UserId, UpdatedByUserId = instructorProfile.UserId
                },
                new StudentRecognition
                {
                    SchoolId = school.Id, StudentId = students[4].Id, AcademicTermId = academicTerm.Id,
                    ClassroomId = classroom.Id, RecognitionType = "PositiveConduct", Title = "بيانات عرض: مبادرة إيجابية داخل الفصل",
                    Description = "بادر الطالب بمساعدة زملائه والمحافظة على ترتيب الفصل.", RecognizedAt = now.AddDays(-4),
                    ReportedByInstructorProfileId = instructorProfile.Id, GuardianNotificationStatus = GuardianNotificationStatus.Queued,
                    CreatedByUserId = instructorProfile.UserId, UpdatedByUserId = instructorProfile.UserId
                });
        }

        // Approval queues are backed by Notification records rather than the source fact alone.
        // Persist the showcase facts first so their generated IDs can be referenced safely.
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var pendingBehavior = await _context.BehaviorIncidents
            .Where(item => item.SchoolId == school.Id
                && item.Description.StartsWith("بيانات عرض:")
                && item.GuardianDispatchDecision == GuardianDispatchDecision.PendingOfficerDecision)
            .OrderBy(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var pendingConcern = await _context.AcademicConcerns
            .Where(item => item.SchoolId == school.Id
                && item.Description.StartsWith("بيانات عرض:")
                && item.GuardianDispatchDecision == GuardianDispatchDecision.PendingOfficerDecision)
            .OrderBy(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (pendingBehavior is not null
            && !await _context.Notifications.AnyAsync(item => item.SchoolId == school.Id
                && item.DeduplicationKey == "showcase-behavior-approval", cancellationToken).ConfigureAwait(false))
        {
            _context.Notifications.Add(new Notification
            {
                SchoolId = school.Id, UserId = guardianUser.Id, StudentId = pendingBehavior.StudentId,
                Title = "ملاحظة سلوكية بانتظار الاعتماد",
                Message = "راجع الواقعة السلوكية وسياقها قبل اعتماد إرسالها إلى ولي الأمر.",
                Type = "GuardianApprovalRequired", RelatedEntityType = nameof(BehaviorIncident),
                RelatedEntityId = pendingBehavior.Id.ToString(), Priority = NotificationPriority.High,
                TemplateKey = "student-affairs.behavior.approval", CorrelationId = Guid.NewGuid(),
                DeduplicationKey = "showcase-behavior-approval", DeliveryStatus = NotificationDeliveryStatus.Pending,
                RequiresApproval = true, CreatedAt = now.AddHours(-3), UpdatedAt = now.AddHours(-3),
                CreatedByUserId = actorUserId, UpdatedByUserId = actorUserId
            });
        }

        if (pendingConcern is not null
            && !await _context.Notifications.AnyAsync(item => item.SchoolId == school.Id
                && item.DeduplicationKey == "showcase-academic-approval", cancellationToken).ConfigureAwait(false))
        {
            _context.Notifications.Add(new Notification
            {
                SchoolId = school.Id, UserId = guardianUser.Id, StudentId = pendingConcern.StudentId,
                Title = "ملاحظة أكاديمية بانتظار الاعتماد",
                Message = "راجع الملاحظة الأكاديمية قبل مشاركتها مع ولي الأمر.",
                Type = "GuardianApprovalRequired", RelatedEntityType = nameof(AcademicConcern),
                RelatedEntityId = pendingConcern.Id.ToString(), Priority = NotificationPriority.High,
                TemplateKey = "student-affairs.academic-concern.approval", CorrelationId = Guid.NewGuid(),
                DeduplicationKey = "showcase-academic-approval", DeliveryStatus = NotificationDeliveryStatus.Pending,
                RequiresApproval = true, CreatedAt = now.AddHours(-2), UpdatedAt = now.AddHours(-2),
                CreatedByUserId = actorUserId, UpdatedByUserId = actorUserId
            });
        }

        if (!await _context.ClassroomEntryPermits.AnyAsync(item => item.SchoolId == school.Id
            && item.Reason == "بيانات عرض: عودة من العيادة المدرسية", cancellationToken).ConfigureAwait(false))
        {
            _context.ClassroomEntryPermits.Add(new ClassroomEntryPermit
            {
                SchoolId = school.Id, StudentId = students[3].Id, AcademicTermId = academicTerm.Id,
                ClassroomId = classroom.Id, IssuedByStudentAffairsUserId = officerUser.Id,
                IssuedAt = now.AddMinutes(-5), Reason = "بيانات عرض: عودة من العيادة المدرسية",
                ValidFrom = now.AddMinutes(-5), ValidUntil = now.AddMinutes(25), TargetInstructorProfileId = instructorProfile.Id,
                Status = ClassroomEntryPermitStatus.Issued,
                CreatedByUserId = officerUser.Id, UpdatedByUserId = officerUser.Id
            });
        }

        if (!await _context.GatePasses.AnyAsync(item => item.SchoolId == school.Id
            && item.IdempotencyKey == "showcase-gate-pass", cancellationToken).ConfigureAwait(false))
        {
            _context.GatePasses.Add(new GatePass
            {
                SchoolId = school.Id, StudentId = students[0].Id, AcademicTermId = academicTerm.Id,
                RequestedByGuardianProfileId = guardian.Id, IdempotencyKey = "showcase-gate-pass",
                RequestedAt = now.AddHours(-1), RequestedExitAt = now.AddHours(2),
                Reason = "موعد طبي مجدول", PickupPersonName = "ولي الأمر", PickupRelationship = "الأب",
                PickupIdentityHint = "سيتم إبراز الهوية الوطنية عند البوابة", Status = GatePassStatus.Requested,
                CreatedByUserId = guardianUser.Id, UpdatedByUserId = guardianUser.Id
            });
        }

        var attendanceDate = today.AddDays(-1);
        var attendance = await _context.DailyStudentAttendances.FirstOrDefaultAsync(item => item.SchoolId == school.Id
            && item.StudentId == students[0].Id && item.AttendanceDate == attendanceDate, cancellationToken).ConfigureAwait(false);
        if (attendance is null)
        {
            attendance = new DailyStudentAttendance
            {
                SchoolId = school.Id, StudentId = students[0].Id, AcademicTermId = academicTerm.Id,
                ClassroomId = classroom.Id, AttendanceDate = attendanceDate, Status = StudentAttendanceStatus.Absent,
                ExcuseStatus = AbsenceExcuseStatus.Pending, RecordedByUserId = secretaryUser.Id, RecordedAt = now.AddDays(-1),
                Source = StudentAttendanceSource.SecretaryRoster,
                CreatedByUserId = secretaryUser.Id, UpdatedByUserId = secretaryUser.Id
            };
            _context.DailyStudentAttendances.Add(attendance);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        if (!await _context.AbsenceExcuses.AnyAsync(item => item.SchoolId == school.Id
            && item.IdempotencyKey == "showcase-pending-excuse", cancellationToken).ConfigureAwait(false))
        {
            _context.AbsenceExcuses.Add(new AbsenceExcuse
            {
                SchoolId = school.Id, DailyStudentAttendanceId = attendance.Id, GuardianProfileId = guardian.Id,
                IdempotencyKey = "showcase-pending-excuse", ExcuseType = AbsenceExcuseType.Medical,
                GuardianNotes = "تعرض الطالب لوعكة صحية صباحية ويجري استكمال المستند الطبي.",
                Status = AbsenceExcuseStatus.Pending, SubmittedAt = now.AddHours(-4),
                CreatedByUserId = guardianUser.Id, UpdatedByUserId = guardianUser.Id
            });
        }

        var summon = await _context.GuardianSummons.FirstOrDefaultAsync(item => item.SchoolId == school.Id
            && item.Status == GuardianSummonStatus.Pending, cancellationToken).ConfigureAwait(false);
        if (summon is null)
        {
            summon = new GuardianSummon
            {
                SchoolId = school.Id, StudentId = students[2].Id, AcademicTermId = academicTerm.Id,
                GuardianProfileId = guardian.Id,
                CreatedReason = "بيانات عرض: مراجعة استدعاء بعد انخفاض مؤشر الغياب",
                Priority = ReferralPriority.High, Status = GuardianSummonStatus.Pending,
                SourceCountSnapshot = 5, ThresholdSnapshot = 5,
                IdempotencyKey = "showcase-automation-review",
                IdempotencyPayloadHash = new string('C', 64),
                RequiresOfficerReview = true,
                OfficerReviewReason = "انخفض المؤشر الحالي بعد تصحيح بيانات سابقة؛ يلزم قرار وكيل الشؤون.",
                OfficerReviewFlaggedAt = now.AddHours(-2),
                CreatedAt = now.AddDays(-2), UpdatedAt = now.AddHours(-2),
                CreatedByUserId = actorUserId, UpdatedByUserId = officerUser.Id
            };
            _context.GuardianSummons.Add(summon);
        }
        else if (!summon.RequiresOfficerReview)
        {
            summon.RequiresOfficerReview = true;
            summon.OfficerReviewReason = "انخفض المؤشر الحالي بعد تصحيح بيانات سابقة؛ يلزم قرار وكيل الشؤون.";
            summon.OfficerReviewFlaggedAt = now.AddHours(-2);
            summon.SourceCountSnapshot ??= 5;
            summon.ThresholdSnapshot ??= 5;
            summon.UpdatedByUserId = officerUser.Id;
        }

        if (!await _context.ConversationThreads.AnyAsync(thread => thread.SchoolId == school.Id
            && thread.Subject == "متابعة انتظام الطالب - تجربة", cancellationToken).ConfigureAwait(false))
        {
            var thread = new ConversationThread
            {
                SchoolId = school.Id, StudentId = students[0].Id,
                ThreadType = ConversationThreadType.GuardianStudentAffairs,
                Subject = "متابعة انتظام الطالب - تجربة", Status = ConversationThreadStatus.Open,
                CreatedAt = now.AddDays(-1), UpdatedAt = now.AddMinutes(-30),
                CreatedByUserId = guardianUser.Id, UpdatedByUserId = officerUser.Id
            };
            thread.Participants.Add(new ConversationParticipant
            {
                SchoolId = school.Id, ApplicationUserId = guardianUser.Id, ParticipantRoleSnapshot = RoleNames.Guardian,
                JoinedAt = now.AddDays(-1), CreatedAt = now.AddDays(-1), UpdatedAt = now.AddDays(-1),
                CreatedByUserId = guardianUser.Id, UpdatedByUserId = guardianUser.Id
            });
            thread.Participants.Add(new ConversationParticipant
            {
                SchoolId = school.Id, ApplicationUserId = officerUser.Id, ParticipantRoleSnapshot = RoleNames.StudentAffairsOfficer,
                JoinedAt = now.AddDays(-1), CreatedAt = now.AddDays(-1), UpdatedAt = now.AddDays(-1),
                CreatedByUserId = guardianUser.Id, UpdatedByUserId = guardianUser.Id
            });
            var firstMessage = new ConversationMessage
            {
                SchoolId = school.Id, SenderUserId = guardianUser.Id,
                Body = "أرغب في معرفة خطة متابعة انتظام ابني خلال هذا الأسبوع.",
                SentAt = now.AddDays(-1), QueuedAt = now.AddDays(-1), ReleasedAt = now.AddDays(-1),
                OfficeHoursDisposition = OfficeHoursDisposition.SentImmediately,
                IdempotencyKey = "showcase-message-guardian", IdempotencyPayloadHash = new string('A', 64),
                CreatedAt = now.AddDays(-1), UpdatedAt = now.AddDays(-1),
                CreatedByUserId = guardianUser.Id, UpdatedByUserId = guardianUser.Id
            };
            firstMessage.Receipts.Add(new MessageReceipt
            {
                SchoolId = school.Id, RecipientUserId = officerUser.Id,
                DeliveryState = MessageDeliveryState.Delivered, DeliveredAt = now.AddDays(-1), ReadAt = now.AddHours(-20),
                CreatedAt = now.AddDays(-1)
            });
            var reply = new ConversationMessage
            {
                SchoolId = school.Id, SenderUserId = officerUser.Id,
                Body = "تمت مراجعة السجل، وسنشاركك ملخص المتابعة بعد نهاية الأسبوع.",
                SentAt = now.AddMinutes(-30), QueuedAt = now.AddMinutes(-30), ReleasedAt = now.AddMinutes(-30),
                OfficeHoursDisposition = OfficeHoursDisposition.SentImmediately,
                IdempotencyKey = "showcase-message-officer", IdempotencyPayloadHash = new string('B', 64),
                CreatedAt = now.AddMinutes(-30), UpdatedAt = now.AddMinutes(-30),
                CreatedByUserId = officerUser.Id, UpdatedByUserId = officerUser.Id
            };
            reply.Receipts.Add(new MessageReceipt
            {
                SchoolId = school.Id, RecipientUserId = guardianUser.Id,
                DeliveryState = MessageDeliveryState.Delivered, DeliveredAt = now.AddMinutes(-30),
                CreatedAt = now.AddMinutes(-30)
            });
            thread.Messages.Add(firstMessage);
            thread.Messages.Add(reply);
            _context.ConversationThreads.Add(thread);
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureSocialWorkerSeedDataAsync(
        School school,
        AcademicTerm academicTerm,
        ApplicationUser socialWorkerUser,
        ApplicationUser guardianUser,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var students = await _context.Students
            .IgnoreQueryFilters()
            .Where(candidate => candidate.SchoolId == school.Id
                && (candidate.StudentNumber == TestStudentNumber || candidate.StudentNumber.StartsWith("DEMO-")))
            .OrderBy(candidate => candidate.StudentNumber == TestStudentNumber ? 0 : 1)
            .ThenBy(candidate => candidate.StudentNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var student = students.FirstOrDefault();

        var guardian = await _context.GuardianProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(g => g.SchoolId == school.Id && g.ApplicationUserId == guardianUser.Id, cancellationToken)
            .ConfigureAwait(false);

        if (student is null || guardian is null) return;

        var now = _timeProvider.GetUtcNow();

        var referralFixtures = new[]
        {
            new { Key = "showcase-referral-assigned", Source = ReferralSourceType.Absence, Priority = ReferralPriority.High,
                Status = StudentReferralStatus.Assigned, Count = 6, Threshold = 5, DaysAgo = 1,
                Summary = "متابعة انتظام الطالب بعد تكرار الغياب والتواصل مع الأسرة لوضع خطة حضور." },
            new { Key = "showcase-referral-progress", Source = ReferralSourceType.Behavior, Priority = ReferralPriority.Critical,
                Status = StudentReferralStatus.InProgress, Count = 4, Threshold = 3, DaysAgo = 3,
                Summary = "جلسة إرشادية وخطة متابعة سلوكية قصيرة بمؤشرات أسبوعية واضحة." },
            new { Key = "showcase-referral-academic", Source = ReferralSourceType.AcademicConcern, Priority = ReferralPriority.Normal,
                Status = StudentReferralStatus.InProgress, Count = 3, Threshold = 3, DaysAgo = 5,
                Summary = "متابعة انخفاض المشاركة والواجبات بالتنسيق مع المعلم وولي الأمر." },
            new { Key = "showcase-referral-resolved", Source = ReferralSourceType.MorningDelay, Priority = ReferralPriority.Normal,
                Status = StudentReferralStatus.Resolved, Count = 5, Threshold = 5, DaysAgo = 9,
                Summary = "تحسن انتظام الوصول بعد تنفيذ اتفاق المتابعة مع الطالب والأسرة." }
        };

        var referralsByKey = await _context.StudentReferrals
            .IgnoreQueryFilters()
            .Where(item => item.SchoolId == school.Id && item.IdempotencyKey != null
                && item.IdempotencyKey.StartsWith("showcase-referral-"))
            .ToDictionaryAsync(item => item.IdempotencyKey!, cancellationToken)
            .ConfigureAwait(false);

        for (var fixtureIndex = 0; fixtureIndex < referralFixtures.Length; fixtureIndex++)
        {
            var fixture = referralFixtures[fixtureIndex];
            if (referralsByKey.ContainsKey(fixture.Key)) continue;
            var referralStudent = students[fixtureIndex % students.Count];

            var referral = new StudentReferral
            {
                SchoolId = school.Id,
                StudentId = referralStudent.Id,
                AcademicTermId = academicTerm.Id,
                SourceType = fixture.Source,
                Priority = fixture.Priority,
                Status = fixture.Status,
                AssignedSocialWorkerUserId = socialWorkerUser.Id,
                CountSnapshot = fixture.Count,
                ThresholdSnapshot = fixture.Threshold,
                IdempotencyKey = fixture.Key,
                IdempotencyPayloadHash = new string('D', 64),
                RecommendedActions = fixture.Summary,
                ResolutionNotes = fixture.Status == StudentReferralStatus.Resolved
                    ? "اكتملت خطة المتابعة وثبت تحسن الانتظام لمدة أسبوعين."
                    : null,
                CreatedAt = now.AddDays(-fixture.DaysAgo),
                CreatedByUserId = actorUserId,
                UpdatedAt = now.AddHours(-fixture.DaysAgo * 3),
                UpdatedByUserId = socialWorkerUser.Id
            };

            if (fixture.Status is StudentReferralStatus.InProgress or StudentReferralStatus.Resolved)
            {
                referral.Actions.Add(new StudentCaseAction
                {
                    SchoolId = school.Id,
                    ActionType = StudentCaseActionType.CounselingSession,
                    Description = fixture.Status == StudentReferralStatus.Resolved
                        ? "مراجعة نتائج الخطة مع الطالب وتوثيق التحسن."
                        : "جلسة إرشاد فردية لفهم الأسباب والاتفاق على خطوات عملية.",
                    ActorUserId = socialWorkerUser.Id,
                    ActionAt = now.AddDays(-Math.Max(1, fixture.DaysAgo - 1)),
                    Result = fixture.Status == StudentReferralStatus.Resolved
                        ? "استقرت المؤشرات ضمن المستوى المطلوب."
                        : "أبدى الطالب تجاوبًا وبدأ تنفيذ الخطة.",
                    CreatedAt = now.AddDays(-Math.Max(1, fixture.DaysAgo - 1)),
                    CreatedByUserId = socialWorkerUser.Id,
                    UpdatedAt = now.AddDays(-Math.Max(1, fixture.DaysAgo - 1)),
                    UpdatedByUserId = socialWorkerUser.Id
                });
            }

            _context.StudentReferrals.Add(referral);
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var summonFixtures = new[]
        {
            new { Key = "showcase-summon-pending", Status = GuardianSummonStatus.Pending, Priority = ReferralPriority.High,
                ScheduledAt = (DateTimeOffset?)now.AddDays(1).AddHours(2), DaysAgo = 1,
                Reason = "مناقشة خطة تحسين الحضور والالتزام بمواعيد بداية اليوم." },
            new { Key = "showcase-summon-attended", Status = GuardianSummonStatus.Attended, Priority = ReferralPriority.Normal,
                ScheduledAt = (DateTimeOffset?)now.AddDays(-1), DaysAgo = 4,
                Reason = "مراجعة مستوى المشاركة الصفية والاتفاق على وسائل دعم منزلية." },
            new { Key = "showcase-summon-observation", Status = GuardianSummonStatus.UnderObservation, Priority = ReferralPriority.High,
                ScheduledAt = (DateTimeOffset?)now.AddDays(-5), DaysAgo = 7,
                Reason = "بدء خطة متابعة سلوكية مشتركة بين المدرسة والأسرة لمدة أسبوعين." },
            new { Key = "showcase-summon-improved", Status = GuardianSummonStatus.Improved, Priority = ReferralPriority.Normal,
                ScheduledAt = (DateTimeOffset?)now.AddDays(-12), DaysAgo = 15,
                Reason = "مراجعة ختامية لخطة الانتظام وتوثيق مؤشرات التحسن." }
        };

        var summonsByKey = await _context.GuardianSummons
            .IgnoreQueryFilters()
            .Where(item => item.SchoolId == school.Id && item.IdempotencyKey != null
                && item.IdempotencyKey.StartsWith("showcase-summon-"))
            .ToDictionaryAsync(item => item.IdempotencyKey!, cancellationToken)
            .ConfigureAwait(false);

        foreach (var fixture in summonFixtures)
        {
            if (summonsByKey.ContainsKey(fixture.Key)) continue;

            var summon = new GuardianSummon
            {
                SchoolId = school.Id,
                StudentId = student.Id,
                AcademicTermId = academicTerm.Id,
                GuardianProfileId = guardian.Id,
                CreatedReason = fixture.Reason,
                Priority = fixture.Priority,
                Status = fixture.Status,
                ScheduledAt = fixture.ScheduledAt,
                ScheduledBySocialWorkerUserId = socialWorkerUser.Id,
                Location = "مكتب الموجه الطلابي - الدور الأرضي",
                Instructions = "يرجى الحضور قبل الموعد بعشر دقائق.",
                SourceCountSnapshot = fixture.Priority == ReferralPriority.High ? 6 : 3,
                ThresholdSnapshot = fixture.Priority == ReferralPriority.High ? 5 : 3,
                IdempotencyKey = fixture.Key,
                IdempotencyPayloadHash = new string('E', 64),
                GuardianNotifiedAt = now.AddDays(-fixture.DaysAgo).AddMinutes(10),
                AttendedAt = fixture.Status == GuardianSummonStatus.Pending ? null : fixture.ScheduledAt,
                AttendanceNotes = fixture.Status == GuardianSummonStatus.Pending ? null : "حضر ولي الأمر وتمت مناقشة خطة المتابعة.",
                ObservationStartedAt = fixture.Status is GuardianSummonStatus.UnderObservation or GuardianSummonStatus.Improved ? now.AddDays(-5) : null,
                ObservationGoals = fixture.Status is GuardianSummonStatus.UnderObservation or GuardianSummonStatus.Improved ? "رفع الانتظام وتقليل الملاحظات السلوكية." : null,
                ObservationStartDate = fixture.Status is GuardianSummonStatus.UnderObservation or GuardianSummonStatus.Improved ? DateOnly.FromDateTime(now.AddDays(-5).DateTime) : null,
                ObservationReviewDate = fixture.Status is GuardianSummonStatus.UnderObservation or GuardianSummonStatus.Improved ? DateOnly.FromDateTime(now.AddDays(7).DateTime) : null,
                ObservationIndicatorsJson = fixture.Status is GuardianSummonStatus.UnderObservation or GuardianSummonStatus.Improved ? "[\"الحضور في الموعد\",\"الالتزام داخل الفصل\"]" : null,
                ObservationNotes = fixture.Status is GuardianSummonStatus.UnderObservation or GuardianSummonStatus.Improved ? "مراجعة أسبوعية مع الطالب والأسرة." : null,
                ImprovedAt = fixture.Status == GuardianSummonStatus.Improved ? now.AddDays(-1) : null,
                ImprovementNotes = fixture.Status == GuardianSummonStatus.Improved ? "تحسن واضح ومستقر في الانتظام والسلوك." : null,
                ImprovementVerificationDetails = fixture.Status == GuardianSummonStatus.Improved ? "مقارنة سجل الأسبوعين ومراجعة إفادة المعلم." : null,
                CreatedAt = now.AddDays(-fixture.DaysAgo),
                CreatedByUserId = socialWorkerUser.Id,
                UpdatedAt = now.AddHours(-fixture.DaysAgo * 2),
                UpdatedByUserId = socialWorkerUser.Id
            };

            summon.StatusHistory.Add(new GuardianSummonStatusHistory
            {
                SchoolId = school.Id,
                FromStatus = GuardianSummonStatus.Pending,
                ToStatus = fixture.Status,
                ActorUserId = socialWorkerUser.Id,
                ActorRole = RoleNames.SocialWorker,
                OccurredAt = now.AddHours(-fixture.DaysAgo * 2),
                Notes = fixture.Status == GuardianSummonStatus.Pending ? "تم تحديد موعد الاستدعاء." : "بيانات عرض لمسار الاستدعاء.",
                CorrelationId = Guid.NewGuid()
            });

            _context.GuardianSummons.Add(summon);
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureIdentitySuccess(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Identity seed operation failed ({operation}): "
            + string.Join(", ", result.Errors.Select(error => error.Description)));
    }

    private sealed record TestAccount(
        string UserName,
        string Email,
        string FirstName,
        string LastName,
        string Role);

    private sealed record FixtureTimetableSource(
        SubjectDefinition Subject,
        ClassSubjectRequirement Requirement);

    private sealed class FixtureCurrentUser(string userId, int schoolId) : ICurrentUserService
    {
        public string? UserId => userId;
        public string? Username => "student-affairs-fixture";
        public int? ActiveSchoolId => schoolId;
        public string? PreferredLanguage => "ar";
        public bool IsAuthenticated => true;
        public bool IsInRole(string roleName) => roleName == RoleNames.SchoolManager;
        public bool HasPermission(string permissionName) => permissionName == PermissionNames.TimetableManage;
        public IEnumerable<string> GetRoles() => [RoleNames.SchoolManager];
        public IEnumerable<string> GetPermissions() => [PermissionNames.TimetableManage];
        public bool IsGlobalAdmin() => false;
        public bool IsSchoolScopedRole() => true;
    }
}
