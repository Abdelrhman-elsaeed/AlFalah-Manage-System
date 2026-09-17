using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.IntelligentTimetable.Handlers;
using AlFalah.Application.Interfaces;
using AlFalah.Application.Validators.IntelligentTimetable;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AlFalah.Tests.Timetables;

public sealed class TimetableSettingsPhase1Tests
{
    [Fact]
    public void Create_validator_rejects_missing_name_and_invalid_scope()
    {
        var validator = new CreateTimetableSetupProfileRequestValidator();

        var result = validator.Validate(new CreateTimetableSetupProfileRequest(
            0,
            (TimetableSemester)99,
            "   "));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(x => x.PropertyName).Should().Contain(
            new[] { "AcademicYearId", "Semester", "Name" });
    }

    [Fact]
    public void Academic_year_validator_rejects_overlapping_or_out_of_range_semesters()
    {
        var validator = new CreateTimetableAcademicYearRequestValidator();

        var result = validator.Validate(new CreateTimetableAcademicYearRequest(
            "2026-2027",
            "العام الدراسي 2026-2027",
            new DateOnly(2026, 8, 1),
            new DateOnly(2027, 7, 31),
            new DateOnly(2026, 7, 20),
            new DateOnly(2027, 1, 15),
            new DateOnly(2027, 1, 10),
            new DateOnly(2027, 8, 1),
            TimetableSemester.First));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "FirstSemesterStartsOn");
        result.Errors.Should().Contain(error => error.PropertyName == "SecondSemesterStartsOn");
        result.Errors.Should().Contain(error => error.ErrorMessage.Contains("داخل العام الدراسي", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Create_academic_year_reuses_global_year_and_creates_two_school_terms()
    {
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase($"timetable-academic-year-{Guid.NewGuid()}")
            .Options;
        await using var context = new AlFalahDbContext(options);
        context.AcademicYears.Add(new AcademicYear
        {
            Id = 7,
            Code = "2026-2027",
            NameAr = "العام الدراسي 2026-2027",
            StartsOn = new DateOnly(2026, 8, 1),
            EndsOn = new DateOnly(2027, 7, 31),
            IsActive = true
        });
        await context.SaveChangesAsync();

        var repository = new TimetableSettingsRepository(context);
        var handler = new CreateTimetableAcademicYearCommandHandler(
            repository,
            new TestCurrentUser(RoleNames.Secretary, PermissionNames.TimetableManage),
            TimeProvider.System);

        var response = await handler.Handle(
            new CreateTimetableAcademicYearCommand(ValidAcademicYearRequest(TimetableSemester.Second)),
            CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        response.Data!.Id.Should().Be(7);
        (await context.AcademicYears.CountAsync()).Should().Be(1);
        var terms = await context.AcademicTerms.OrderBy(term => term.Semester).ToListAsync();
        terms.Should().HaveCount(2);
        terms.Should().OnlyContain(term => term.SchoolId == 1 && term.AcademicYearId == 7);
        terms.Single(term => term.Semester == TimetableSemester.Second).IsActive.Should().BeTrue();
        terms.Single(term => term.Semester == TimetableSemester.First).IsActive.Should().BeFalse();

        var listedYears = await repository.GetAcademicYearsAsync(1, CancellationToken.None);
        listedYears.Should().ContainSingle(year => year.Id == 7 && year.IsActive);
    }

    [Fact]
    public async Task Create_academic_year_rejects_duplicate_school_scope()
    {
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase($"timetable-academic-year-duplicate-{Guid.NewGuid()}")
            .Options;
        await using var context = new AlFalahDbContext(options);
        context.AcademicYears.Add(new AcademicYear
        {
            Id = 7,
            Code = "2026-2027",
            NameAr = "العام الدراسي 2026-2027",
            StartsOn = new DateOnly(2026, 8, 1),
            EndsOn = new DateOnly(2027, 7, 31),
            IsActive = true
        });
        context.AcademicTerms.Add(new AcademicTerm
        {
            SchoolId = 1,
            AcademicYearId = 7,
            Semester = TimetableSemester.First,
            StartsOn = new DateOnly(2026, 8, 1),
            EndsOn = new DateOnly(2026, 12, 31),
            IsActive = true,
            CreatedByUserId = "user-1",
            UpdatedByUserId = "user-1"
        });
        await context.SaveChangesAsync();

        var handler = new CreateTimetableAcademicYearCommandHandler(
            new TimetableSettingsRepository(context),
            new TestCurrentUser(RoleNames.Secretary, PermissionNames.TimetableManage),
            TimeProvider.System);

        var response = await handler.Handle(
            new CreateTimetableAcademicYearCommand(ValidAcademicYearRequest(TimetableSemester.First)),
            CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        response.Errors.Should().Contain(TimetableSettingsHandlerSupport.DuplicateAcademicYearScope);
        (await context.AcademicTerms.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Create_school_scope_does_not_replace_the_platform_global_active_year()
    {
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase($"timetable-academic-year-global-active-{Guid.NewGuid()}")
            .Options;
        await using var context = new AlFalahDbContext(options);
        context.AcademicYears.Add(new AcademicYear
        {
            Id = 3,
            Code = "2025-2026",
            NameAr = "العام الدراسي 2025-2026",
            StartsOn = new DateOnly(2025, 8, 1),
            EndsOn = new DateOnly(2026, 7, 31),
            IsActive = true
        });
        await context.SaveChangesAsync();

        var handler = new CreateTimetableAcademicYearCommandHandler(
            new TimetableSettingsRepository(context),
            new TestCurrentUser(RoleNames.Secretary, PermissionNames.TimetableManage),
            TimeProvider.System);

        var response = await handler.Handle(
            new CreateTimetableAcademicYearCommand(ValidAcademicYearRequest(TimetableSemester.First)),
            CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        (await context.AcademicYears.SingleAsync(year => year.Id == 3)).IsActive.Should().BeTrue();
        (await context.AcademicYears.SingleAsync(year => year.Code == "2026-2027")).IsActive.Should().BeFalse();
        (await context.AcademicTerms.SingleAsync(term => term.IsActive)).AcademicYearId.Should().Be(response.Data!.Id);
    }

    [Fact]
    public void Readiness_allows_an_active_classroom_without_students_when_location_is_present()
    {
        var profile = Profile();

        var (steps, warnings) = TimetableSettingsHandlerSupport.BuildReadiness(
            profile,
            Readiness(ActiveClassrooms: 1, ActiveTeachers: 1));

        steps.Single(x => x.Key == "classrooms").Status.Should().Be("complete");
        warnings.Should().NotContain(x => x.Contains("طالب", StringComparison.Ordinal));
    }

    [Fact]
    public void Readiness_marks_classrooms_incomplete_when_any_location_is_missing()
    {
        var (steps, warnings) = TimetableSettingsHandlerSupport.BuildReadiness(
            Profile(),
            Readiness(ActiveClassrooms: 3, ActiveStudents: 20, ClassroomsMissingLocation: 1, ActiveTeachers: 2));

        steps.Single(x => x.Key == "classrooms").Status.Should().Be("incomplete");
        warnings.Should().Contain(x => x.Contains("1 فصل", StringComparison.Ordinal));
    }

    [Fact]
    public void Readiness_marks_a_complete_seeded_setup_as_fully_ready()
    {
        var assignments = Enumerable.Range(1, 6)
            .Select(requirementId => new TimetableAssignmentReadinessData(
                requirementId,
                "SingleTeacher",
                IndividualPeriodCount: 5,
                PairedBlockCount: 1,
                [new(TeacherTimetableProfileId: requirementId, AllocatedPeriodCount: 7, AllocatedPairedBlockCount: 1, IsTeacherReady: true)]))
            .ToArray();
        var readiness = new TimetableReadinessData(
            ActiveClassrooms: 6,
            ActiveStudents: 0,
            ClassroomsMissingLocation: 0,
            ActiveTeachers: 20,
            ConfiguredTeachers: 20,
            AvailableSubjects: 10,
            CoveredClassrooms: 6,
            RequirementCount: 6,
            Assignments: assignments,
            TimetableExists: true,
            HasCurrentTimetable: true);

        var (steps, warnings) = TimetableSettingsHandlerSupport.BuildReadiness(
            Profile() with { BellScheduleTemplateId = 1, Status = TimetableSetupStatus.Generated },
            readiness);

        steps.Should().OnlyContain(step => step.Status == "complete");
        warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task Readiness_repository_reads_the_complete_saved_setup()
    {
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase($"timetable-readiness-{Guid.NewGuid()}")
            .Options;
        await using var context = new AlFalahDbContext(options);
        context.Schools.Add(new School { Id = 1, Name = "مدرسة الاختبار", IsActive = true });
        context.AcademicYears.Add(new AcademicYear
        {
            Id = 1,
            Code = "2026-2027",
            NameAr = "العام الدراسي 2026-2027",
            StartsOn = new DateOnly(2026, 8, 1),
            EndsOn = new DateOnly(2027, 7, 31),
            IsActive = true
        });
        await context.SaveChangesAsync();
        var schedule = await BellScheduleTestData.SeedAsync(context);
        var setup = await context.TimetableSetupProfiles.SingleAsync();
        setup.Revision = 3;

        context.Users.Add(new ApplicationUser
        {
            Id = "teacher-1",
            UserName = "teacher-1",
            FirstName = "أحمد",
            LastName = "المعلم",
            IsActive = true
        });
        context.InstructorProfiles.Add(new InstructorProfile
        {
            Id = 1,
            UserId = "teacher-1",
            SchoolId = 1,
            IsActive = true
        });
        context.TeacherTimetableProfiles.Add(new TeacherTimetableProfile
        {
            Id = 1,
            SchoolId = 1,
            TimetableSetupProfileId = setup.Id,
            InstructorProfileId = 1,
            BellScheduleRevisionId = schedule.Id,
            ShortDisplayName = "أحمد",
            MaximumWeeklyPeriods = 10
        });
        context.Classrooms.Add(new Classroom
        {
            Id = 1,
            SchoolId = 1,
            AcademicYearId = 1,
            ClassLabel = "الأول - أ",
            PhysicalLocation = "الدور الأول",
            IsActive = true
        });
        context.Set<SubjectDefinition>().Add(new SubjectDefinition
        {
            Id = 1,
            SchoolId = 1,
            Name = "الرياضيات",
            IsActive = true
        });
        context.Set<ClassSubjectRequirement>().Add(new ClassSubjectRequirement
        {
            Id = 1,
            SchoolId = 1,
            TimetableSetupProfileId = setup.Id,
            ClassroomId = 1,
            SubjectId = 1,
            IndividualPeriodCount = 1
        });
        context.Set<TeachingAssignment>().Add(new TeachingAssignment
        {
            Id = 1,
            SchoolId = 1,
            TimetableSetupProfileId = setup.Id,
            ClassSubjectRequirementId = 1,
            Mode = "SingleTeacher",
            Members =
            [
                new TeachingAssignmentMember
                {
                    SchoolId = 1,
                    TimetableSetupProfileId = setup.Id,
                    TeacherTimetableProfileId = 1,
                    AllocatedPeriodCount = 1
                }
            ]
        });
        context.SchoolTimetables.Add(new SchoolTimetable
        {
            Id = 1,
            SchoolId = 1,
            AcademicYearId = 1,
            TimetableSetupProfileId = setup.Id,
            BellScheduleRevisionId = schedule.Id,
            Semester = TimetableSemester.First,
            Title = "الجدول الكامل",
            SetupRevision = setup.Revision,
            CreatedByUserId = "manager",
            UpdatedByUserId = "manager",
            Entries =
            [
                new SchoolTimetableEntry
                {
                    SchoolId = 1,
                    ClassroomId = 1,
                    InstructorProfileId = 1,
                    Day = TimetableDay.Sunday,
                    Period = 1,
                    EntryType = TimetableEntryType.Lesson,
                    ClassSubjectRequirementId = 1
                }
            ]
        });
        await context.SaveChangesAsync();

        var readiness = await new TimetableSettingsRepository(context).GetReadinessDataAsync(
            schoolId: 1,
            academicYearId: 1,
            TimetableSemester.First,
            setup.Id,
            CancellationToken.None);

        readiness.Should().Match<TimetableReadinessData>(x =>
            x.ActiveClassrooms == 1
            && x.ClassroomsMissingLocation == 0
            && x.ActiveTeachers == 1
            && x.ConfiguredTeachers == 1
            && x.AvailableSubjects == 1
            && x.CoveredClassrooms == 1
            && x.RequirementCount == 1
            && x.Assignments.Count == 1
            && x.Assignments[0].Members.Count == 1
            && x.Assignments[0].Members[0].IsTeacherReady
            && x.TimetableExists
            && x.HasCurrentTimetable);

        var response = await new GetTimetableSettingsQueryHandler(
                new TimetableSettingsRepository(context),
                new TestCurrentUser(RoleNames.Secretary, PermissionNames.TimetableManage),
                new BellScheduleRepository(context))
            .Handle(new GetTimetableSettingsQuery(1, TimetableSemester.First, setup.Id), CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        response.Data!.CompletionPercent.Should().Be(100);
        response.Data.HardPrerequisitesValid.Should().BeTrue();
        response.Data.Steps.Should().OnlyContain(step => step.Status == "complete");
    }

    [Fact]
    public void Settings_access_excludes_instructors_even_if_legacy_timetable_view_is_present()
    {
        var currentUser = new TestCurrentUser(RoleNames.Instructor, PermissionNames.TimetableView);

        TimetableSettingsHandlerSupport.CanView(currentUser).Should().BeFalse();
    }

    [Fact]
    public void Ef_model_configures_tenant_key_unique_scope_and_revision_concurrency()
    {
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase($"timetable-settings-{Guid.NewGuid()}")
            .Options;
        using var context = new AlFalahDbContext(options);

        var profile = context.Model.FindEntityType(typeof(TimetableSetupProfile));

        profile.Should().NotBeNull();
        profile!.FindProperty(nameof(TimetableSetupProfile.Revision))!.IsConcurrencyToken.Should().BeTrue();
        profile.FindProperty(nameof(TimetableSetupProfile.Name))!.GetMaxLength().Should().Be(120);
        profile.GetIndexes().Should().Contain(index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { "SchoolId", "AcademicYearId", "Semester", "Name" }));
    }

    [Fact]
    public void Profile_list_query_filters_and_orders_entities_before_projection_to_sql_server()
    {
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=AlFalahQueryTranslationTests;Trusted_Connection=True;")
            .Options;
        using var context = new AlFalahDbContext(options);

        var sql = TimetableSettingsRepository.BuildProfilesQuery(
                context.TimetableSetupProfiles.AsNoTracking(),
                schoolId: 18,
                academicYearId: 1,
                TimetableSemester.First)
            .ToQueryString();

        sql.Should().Contain("ORDER BY");
        sql.Should().Contain("[t].[UpdatedAt] DESC");
        sql.Should().Contain("[t].[SchoolId] = @__schoolId_0");
    }

    private static TimetableSetupProfileDto Profile() => new(
        Id: 1,
        SchoolId: 10,
        AcademicYearId: 1,
        AcademicYearName: "2026-2027",
        Semester: 1,
        SemesterLabelAr: "الفصل الدراسي الأول",
        Name: "الجدول الاعتيادي",
        BellScheduleTemplateId: null,
        Status: TimetableSetupStatus.Draft,
        StatusLabelAr: "مسودة",
        Revision: 1,
        UpdatedAt: DateTimeOffset.UtcNow);

    private static TimetableReadinessData Readiness(
        int ActiveClassrooms = 0,
        int ActiveStudents = 0,
        int ClassroomsMissingLocation = 0,
        int ActiveTeachers = 0) => new(
            ActiveClassrooms,
            ActiveStudents,
            ClassroomsMissingLocation,
            ActiveTeachers,
            ConfiguredTeachers: 0,
            AvailableSubjects: 0,
            CoveredClassrooms: 0,
            RequirementCount: 0,
            Assignments: [],
            TimetableExists: false,
            HasCurrentTimetable: false);

    private static CreateTimetableAcademicYearRequest ValidAcademicYearRequest(TimetableSemester activeSemester) => new(
        "2026-2027",
        "العام الدراسي 2026-2027",
        new DateOnly(2026, 8, 1),
        new DateOnly(2027, 7, 31),
        new DateOnly(2026, 8, 1),
        new DateOnly(2026, 12, 31),
        new DateOnly(2027, 1, 1),
        new DateOnly(2027, 7, 31),
        activeSemester);

    private sealed class TestCurrentUser(string role, params string[] permissions) : ICurrentUserService
    {
        public string? UserId => "user-1";
        public string? Username => "user";
        public int? ActiveSchoolId => 1;
        public string? PreferredLanguage => "ar";
        public bool IsAuthenticated => true;
        public bool IsInRole(string roleName) => roleName == role;
        public bool HasPermission(string permissionName) => permissions.Contains(permissionName);
        public IEnumerable<string> GetRoles() => new[] { role };
        public IEnumerable<string> GetPermissions() => permissions;
        public bool IsGlobalAdmin() => false;
        public bool IsSchoolScopedRole() => true;
    }
}
