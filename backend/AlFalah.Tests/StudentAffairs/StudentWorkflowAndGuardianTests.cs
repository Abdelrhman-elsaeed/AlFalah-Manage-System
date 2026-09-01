using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Classrooms;
using AlFalah.Application.StudentAffairs.DTOs.Dashboards;
using AlFalah.Application.StudentAffairs.DTOs.GatePasses;
using AlFalah.Application.StudentAffairs.DTOs.Guardian;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Application.StudentAffairs.DTOs.Students;
using AlFalah.Application.StudentAffairs.DTOs.Teacher;
using AlFalah.Application.StudentAffairs.Classrooms.Handlers;
using AlFalah.Application.StudentAffairs.Students;
using AlFalah.Application.StudentAffairs.Students.Handlers;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Shared.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AlFalah.Tests.StudentAffairs;

public sealed class StudentWorkflowAndGuardianTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetStudentGuardiansQuery_WhenSocialWorker_ReturnsGuardians()
    {
        var repository = new FakeStudentWorkflowRepository
        {
            Guardians = new List<StudentGuardianLinkDto>
            {
                new(
                    1,
                    new GuardianSummaryDto(10, "Ahmad Guardian", GuardianRelationshipType.Father, true, true),
                    true,
                    true,
                    new DateOnly(2026, 1, 1),
                    null,
                    true,
                    ""
                )
            }
        };

        var handler = new GetStudentGuardiansQueryHandler(
            repository,
            CreateUser(RoleNames.SocialWorker, PermissionNames.SummonMarkAttended),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(new GetStudentGuardiansQuery(17), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Should().HaveCount(1);
        result.Data![0].Guardian.DisplayName.Should().Be("Ahmad Guardian");
        result.Data[0].IsActive.Should().BeTrue();
        repository.QueriedStudentId.Should().Be(17);
        repository.QueriedSchoolId.Should().Be(42);
    }

    [Fact]
    public async Task GetStudentGuardiansQuery_WhenGuardianViewPermission_ReturnsGuardians()
    {
        var repository = new FakeStudentWorkflowRepository
        {
            Guardians = new List<StudentGuardianLinkDto>()
        };

        var handler = new GetStudentGuardiansQueryHandler(
            repository,
            CreateUser("CustomRole", PermissionNames.GuardianView),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(new GetStudentGuardiansQuery(5), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task GetStudentGuardiansQuery_WhenUnauthorized_Fails()
    {
        var repository = new FakeStudentWorkflowRepository();
        var handler = new GetStudentGuardiansQueryHandler(
            repository,
            CreateUser(RoleNames.Instructor),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(new GetStudentGuardiansQuery(17), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(StudentHandlerSupport.PermissionDenied);
    }

    [Fact]
    public async Task GetStudentsQuery_WhenAuthorized_ReturnsPagedStudents()
    {
        var repository = new FakeStudentWorkflowRepository
        {
            Students = new PagedResult<StudentListItemDto>
            {
                Items = new List<StudentListItemDto>
                {
                    new(
                        new StudentSummaryDto(1, "ST-001", "Student One", 2, "1/A", true, null),
                        Array.Empty<MetricBadgeDto>()
                    )
                },
                TotalCount = 1,
                Page = 1,
                PageSize = 20
            }
        };

        var handler = new GetStudentsQueryHandler(
            repository,
            CreateUser(RoleNames.StudentAffairsOfficer, PermissionNames.StudentView),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(new GetStudentsQuery(new StudentListQuery()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetStudentByIdQuery_WhenFound_ReturnsStudentDetails()
    {
        var details = new StudentDetailsDto(
            new StudentSummaryDto(1, "ST-001", "1000000001", "Student One", 2, "1/A", true, null),
            "1000000001",
            "Student",
            null,
            "One",
            null,
            new DateOnly(2015, 5, 10),
            StudentGender.Male,
            null,
            Array.Empty<StudentGuardianLinkDto>(),
            Array.Empty<MetricBadgeDto>(),
            Array.Empty<StudentTimelineItemDto>(),
            new AuditSummaryDto(new ActorSummaryDto("admin", "Admin", "Admin"), Now, null, null),
            ""
        );

        var repository = new FakeStudentWorkflowRepository { StudentDetails = details };
        var handler = new GetStudentByIdQueryHandler(
            repository,
            CreateUser(RoleNames.SocialWorker, PermissionNames.StudentView),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(new GetStudentByIdQuery(1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.FirstName.Should().Be("Student");
    }

    [Fact]
    public async Task CreateClassroomCommand_WhenSecretaryHasPermission_CreatesSchoolScopedClassroom()
    {
        var repository = new FakeStudentWorkflowRepository();
        var handler = new CreateClassroomCommandHandler(
            repository,
            CreateUser(RoleNames.Secretary, PermissionNames.ClassroomManage),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new CreateClassroomCommand(new CreateClassroomRequestDto(3, SchoolStage.Primary, 1, "أ", "1/أ")),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Classroom.Should().NotBeNull();
        repository.Classroom!.SchoolId.Should().Be(42);
        repository.Classroom.ClassLabel.Should().Be("1/أ");
    }

    [Fact]
    public async Task UpdateClassroomCommand_WhenSecretaryHasPermission_UpdatesMutableFields()
    {
        var repository = new FakeStudentWorkflowRepository
        {
            Classroom = ExistingClassroom()
        };
        var handler = new UpdateClassroomCommandHandler(
            repository,
            CreateUser(RoleNames.Secretary, PermissionNames.ClassroomManage),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new UpdateClassroomCommand(7, new UpdateClassroomRequestDto("1/ب", "ب", false, string.Empty)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Classroom!.ClassLabel.Should().Be("1/ب");
        repository.Classroom.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteClassroomCommand_WhenNoActiveEnrollments_SoftDeletesClassroom()
    {
        var repository = new FakeStudentWorkflowRepository
        {
            Classroom = ExistingClassroom()
        };
        var handler = new DeleteClassroomCommandHandler(
            repository,
            CreateUser(RoleNames.Secretary, PermissionNames.ClassroomManage),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new DeleteClassroomCommand(7, new DeleteClassroomRequestDto("اختبار الحذف", string.Empty)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Classroom!.IsDeleted.Should().BeTrue();
        repository.Classroom.IsActive.Should().BeFalse();
        repository.Classroom.DeletedByUserId.Should().Be("worker-user-1");
    }

    [Fact]
    public async Task DeleteClassroomCommand_WhenActiveEnrollmentsAndNotForced_LeavesClassroomUntouched()
    {
        var repository = new FakeStudentWorkflowRepository
        {
            Classroom = ExistingClassroom(),
            HasActiveEnrollments = true
        };
        var handler = new DeleteClassroomCommandHandler(
            repository,
            CreateUser(RoleNames.Secretary, PermissionNames.ClassroomManage),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new DeleteClassroomCommand(7, new DeleteClassroomRequestDto("اختبار الحذف", string.Empty, false)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        repository.Classroom!.IsDeleted.Should().BeFalse();
        repository.UnassignedClassroomEnrollmentCount.Should().Be(0);
    }

    [Fact]
    public async Task DeleteClassroomCommand_WhenActiveEnrollmentsAndForced_UnassignsAndSoftDeletes()
    {
        var repository = new FakeStudentWorkflowRepository
        {
            Classroom = ExistingClassroom(),
            HasActiveEnrollments = true
        };
        var handler = new DeleteClassroomCommandHandler(
            repository,
            CreateUser(RoleNames.Secretary, PermissionNames.ClassroomManage),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new DeleteClassroomCommand(7, new DeleteClassroomRequestDto("اختبار الحذف", string.Empty, true)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.UnassignedClassroomEnrollmentCount.Should().Be(1);
        repository.Classroom!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteStudentCommand_SoftDeletesStudentAndUnassignsEnrollments()
    {
        var repository = new FakeStudentWorkflowRepository
        {
            Student = new Student
            {
                Id = 15,
                SchoolId = 42,
                StudentNumber = "ST-015",
                FirstName = "أحمد",
                LastName = "علي",
                IsActive = true
            }
        };
        var handler = new DeleteStudentCommandHandler(
            repository,
            CreateUser(RoleNames.Secretary, PermissionNames.StudentManage),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new DeleteStudentCommand(15, new DeleteStudentRequestDto("اختبار الحذف", string.Empty)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Student!.IsDeleted.Should().BeTrue();
        repository.Student.IsActive.Should().BeFalse();
        repository.UnassignedStudentEnrollmentCount.Should().Be(1);
    }

    [Fact]
    public async Task GetStudentsStatsQuery_WhenOfficerOrSocialWorker_ReturnsStats()
    {
        var repository = new FakeStudentWorkflowRepository
        {
            StudentStats = new StudentStatsPageResult
            {
                Items = new List<StudentStatsDto>
                {
                    new(10, "STU-10", "Khalid Omar", "1098765432", null, "1/A", 1, true, 2, 1, 1, 0)
                },
                TotalCount = 1,
                TotalClassrooms = 1,
                Page = 1,
                PageSize = 20
            }
        };

        var handler = new GetStudentsStatsQueryHandler(
            repository,
            CreateUser(RoleNames.StudentAffairsOfficer, PermissionNames.StudentView),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(new GetStudentsStatsQuery(new StudentStatsQuery()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Items.Should().HaveCount(1);
        result.Data.Items[0].Name.Should().Be("Khalid Omar");
        result.Data.Items[0].TotalAbsences.Should().Be(2);
        result.Data.Items[0].TotalDelays.Should().Be(1);
    }

    [Fact]
    public async Task GetStudentsStatsQuery_WhenSecretary_FailsPermissionDenied()
    {
        var repository = new FakeStudentWorkflowRepository();
        var handler = new GetStudentsStatsQueryHandler(
            repository,
            CreateUser(RoleNames.Secretary, PermissionNames.StudentView),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(new GetStudentsStatsQuery(new StudentStatsQuery()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(StudentHandlerSupport.PermissionDenied);
    }

    [Fact]
    public async Task GetStudentAnalyticsProfileQuery_WhenSocialWorker_ReturnsProfile()
    {
        var profile = new StudentAnalyticsProfileDto(
            15,
            "STU-15",
            "Tariq Al-Mansoor",
            "1088776655",
            null,
            new DateOnly(2015, 4, 10),
            StudentGender.Male,
            true,
            null,
            2,
            "2/B",
            "Primary",
            2,
            "B",
            5,
            StudentEnrollmentStatus.Active,
            3,
            2,
            2,
            1,
            0,
            1,
            0,
            new List<MonthlyAttendanceTrendDto>
            {
                new("2026-09", "سبتمبر 2026", 3, 2, 2)
            },
            new List<StudentAnalyticsEventDto>
            {
                new("att-1", "Absence", "غياب (بدون عذر)", "2026-09-01", Now, "danger", "pi pi-calendar-times", "بدون عذر", null)
            },
            Array.Empty<StudentGuardianLinkDto>()
        );

        var repository = new FakeStudentWorkflowRepository
        {
            AnalyticsProfile = profile
        };

        var handler = new GetStudentAnalyticsProfileQueryHandler(
            repository,
            CreateUser(RoleNames.SocialWorker, PermissionNames.ReferralView),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(new GetStudentAnalyticsProfileQuery(15), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.StudentId.Should().Be(15);
        result.Data.FullName.Should().Be("Tariq Al-Mansoor");
        result.Data.TotalAbsences.Should().Be(3);
        result.Data.MonthlyTrends.Should().HaveCount(1);
        result.Data.RecentEvents.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetStudentAnalyticsProfileQuery_WhenSecretary_FailsPermissionDenied()
    {
        var repository = new FakeStudentWorkflowRepository();
        var handler = new GetStudentAnalyticsProfileQueryHandler(
            repository,
            CreateUser(RoleNames.Secretary, PermissionNames.StudentView),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(new GetStudentAnalyticsProfileQuery(15), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(StudentHandlerSupport.PermissionDenied);
    }

    [Fact]
    public async Task GetStudentAnalyticsProfileAsync_WhenStudentHasNoHistory_ReturnsCleanProfile()
    {
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new AlFalahDbContext(options);
        var student = new Student
        {
            Id = 10,
            SchoolId = 42,
            StudentNumber = "E2E-STUDENT-001",
            FirstName = "E2E",
            LastName = "Student",
            IdentityNumber = "1000000001",
            IsActive = true
        };
        context.Students.Add(student);
        await context.SaveChangesAsync();

        var repo = new StudentWorkflowRepository(context);
        var profile = await repo.GetStudentAnalyticsProfileAsync(42, 10, new DateOnly(2026, 9, 1), CancellationToken.None);

        profile.Should().NotBeNull();
        profile!.StudentId.Should().Be(10);
        profile.FullName.Should().Be("E2E Student");
        profile.TotalAbsences.Should().Be(0);
        profile.TotalDelays.Should().Be(0);
        profile.TotalExcuses.Should().Be(0);
        profile.TotalReferrals.Should().Be(0);
        profile.MonthlyTrends.Should().HaveCount(6);
        profile.RecentEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task GetStudentAnalyticsProfileAsync_WhenStudentHasReferralsAndDelays_AggregatesCorrectly()
    {
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new AlFalahDbContext(options);
        var student = new Student
        {
            Id = 11,
            SchoolId = 42,
            StudentNumber = "E2E-STUDENT-002",
            FirstName = "E2E",
            LastName = "Student",
            IdentityNumber = "1000000002",
            IsActive = true
        };
        var referral = new StudentReferral
        {
            Id = 101,
            SchoolId = 42,
            StudentId = 11,
            AcademicTermId = 1,
            SourceType = ReferralSourceType.Absence,
            Priority = ReferralPriority.High,
            Status = StudentReferralStatus.Open,
            CreatedAt = Now,
            CreatedByUserId = "test-user"
        };
        var delay = new MorningArrivalDelay
        {
            Id = 201,
            SchoolId = 42,
            StudentId = 11,
            AcademicTermId = 1,
            ArrivalAt = Now,
            SchoolLocalDate = new DateOnly(2026, 9, 1),
            DelayMinutes = 15,
            NotificationPolicySnapshot = "Immediate"
        };
        context.Students.Add(student);
        context.StudentReferrals.Add(referral);
        context.MorningArrivalDelays.Add(delay);
        await context.SaveChangesAsync();

        var repo = new StudentWorkflowRepository(context);
        var profile = await repo.GetStudentAnalyticsProfileAsync(42, 11, new DateOnly(2026, 9, 1), CancellationToken.None);

        profile.Should().NotBeNull();
        profile!.StudentId.Should().Be(11);
        profile.TotalReferrals.Should().Be(1);
        profile.TotalDelays.Should().Be(1);
        profile.RecentEvents.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetStudentsStatsAsync_WithRealDbContext_ReturnsTotalClassrooms()
    {
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new AlFalahDbContext(options);
        context.Classrooms.Add(new Classroom { Id = 1, SchoolId = 42, AcademicYearId = 1, ClassLabel = "1/A", Stage = SchoolStage.Primary, GradeLevel = 1, Section = "A", IsActive = true });
        context.Classrooms.Add(new Classroom { Id = 2, SchoolId = 42, AcademicYearId = 1, ClassLabel = "2/A", Stage = SchoolStage.Primary, GradeLevel = 2, Section = "A", IsActive = true });
        context.Students.Add(new Student { Id = 1, SchoolId = 42, StudentNumber = "ST-1", FirstName = "A", LastName = "B", IdentityNumber = "111", IsActive = true });
        await context.SaveChangesAsync();

        var repo = new StudentWorkflowRepository(context);
        var stats = await repo.GetStudentsStatsAsync(42, new StudentStatsQuery(), new DateOnly(2026, 9, 1), CancellationToken.None);

        stats.Should().NotBeNull();
        stats.TotalClassrooms.Should().Be(2);
        stats.TotalCount.Should().Be(1);
        stats.Items.Should().HaveCount(1);
    }

    private static Classroom ExistingClassroom() => new()
    {
        Id = 7,
        SchoolId = 42,
        AcademicYearId = 3,
        Stage = SchoolStage.Primary,
        GradeLevel = 1,
        Section = "أ",
        ClassLabel = "1/أ",
        IsActive = true
    };


    private static ICurrentUserService CreateUser(string role, params string[] permissions) =>
        new FakeCurrentUserService("worker-user-1", 42, role, permissions);

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        private readonly HashSet<string> _roles;
        private readonly HashSet<string> _permissions;

        public FakeCurrentUserService(string userId, int schoolId, string role, params string[] permissions)
        {
            UserId = userId;
            ActiveSchoolId = schoolId;
            _roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { role };
            _permissions = new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);
        }

        public string? UserId { get; }
        public string? Username => "test.user";
        public int? ActiveSchoolId { get; }
        public string? PreferredLanguage => "ar";
        public bool IsAuthenticated => true;

        public bool IsInRole(string roleName) => _roles.Contains(roleName);
        public bool HasPermission(string permissionName) => _permissions.Contains(permissionName);
        public IEnumerable<string> GetRoles() => _roles;
        public IEnumerable<string> GetPermissions() => _permissions;
        public bool IsGlobalAdmin() => false;
        public bool IsSchoolScopedRole() => true;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private sealed class FakeStudentWorkflowRepository : IStudentWorkflowRepository
    {
        public int QueriedSchoolId { get; private set; }
        public int QueriedStudentId { get; private set; }
        public IReadOnlyList<StudentGuardianLinkDto> Guardians { get; set; } = new List<StudentGuardianLinkDto>();
        public PagedResult<StudentListItemDto> Students { get; set; } = new();
        public StudentStatsPageResult StudentStats { get; set; } = new();
        public StudentAnalyticsProfileDto? AnalyticsProfile { get; set; }
        public StudentDetailsDto? StudentDetails { get; set; }
        public Student? Student { get; set; }
        public Classroom? Classroom { get; set; }
        public bool AcademicYearExists { get; set; } = true;
        public bool LabelExists { get; set; }
        public bool HasActiveEnrollments { get; set; }
        public int UnassignedClassroomEnrollmentCount { get; private set; }
        public int UnassignedStudentEnrollmentCount { get; private set; }

        public Task<StudentStatsPageResult> GetStudentsStatsAsync(int schoolId, StudentStatsQuery query, DateOnly onDate, CancellationToken cancellationToken) =>
            Task.FromResult(StudentStats);

        public Task<StudentAnalyticsProfileDto?> GetStudentAnalyticsProfileAsync(int schoolId, int studentId, DateOnly onDate, CancellationToken cancellationToken) =>
            Task.FromResult(AnalyticsProfile);

        public Task<IReadOnlyList<StudentGuardianLinkDto>> GetStudentGuardiansAsync(
            int schoolId,
            int studentId,
            DateOnly onDate,
            CancellationToken cancellationToken)
        {
            QueriedSchoolId = schoolId;
            QueriedStudentId = studentId;
            return Task.FromResult(Guardians);
        }

        public Task<PagedResult<StudentListItemDto>> GetStudentsAsync(int schoolId, StudentListQuery query, DateOnly onDate, CancellationToken cancellationToken) =>
            Task.FromResult(Students);

        public Task<StudentDetailsDto?> GetStudentDetailsAsync(int schoolId, int studentId, DateOnly onDate, CancellationToken cancellationToken) =>
            Task.FromResult(StudentDetails);

        public Task<Student?> GetStudentForUpdateAsync(int schoolId, int studentId, CancellationToken cancellationToken) =>
            Task.FromResult(Student?.Id == studentId && Student.SchoolId == schoolId ? Student : null);

        public Task<StudentEnrollment?> GetActiveStudentEnrollmentForUpdateAsync(int schoolId, int studentId, CancellationToken cancellationToken) =>
            Task.FromResult<StudentEnrollment?>(null);

        public Task<StudentEnrollmentTarget?> GetStudentEnrollmentTargetAsync(int schoolId, int classroomId, CancellationToken cancellationToken) =>
            Task.FromResult<StudentEnrollmentTarget?>(new StudentEnrollmentTarget(classroomId, 11));

        public Task<bool> StudentNumberExistsAsync(int schoolId, string studentNumber, int? excludingStudentId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> StudentIdentityNumberExistsAsync(int schoolId, string identityNumber, int? excludingStudentId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> StudentNationalIdExistsAsync(int schoolId, string nationalId, int? excludingStudentId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<StudentGuardian?> GetGuardianLinkForUpdateAsync(int schoolId, int studentId, int linkId, CancellationToken cancellationToken) =>
            Task.FromResult<StudentGuardian?>(null);

        public Task<StudentEnrollment?> GetEnrollmentForUpdateAsync(int schoolId, int studentId, int enrollmentId, CancellationToken cancellationToken) =>
            Task.FromResult<StudentEnrollment?>(null);

        public Task<PagedResult<StudentTimelineItemDto>> GetStudentTimelineAsync(int schoolId, int studentId, StudentTimelineQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<StudentTimelineItemDto>());

        public Task<StudentEnrollmentDto?> GetEnrollmentDtoAsync(int schoolId, int enrollmentId, CancellationToken cancellationToken) =>
            Task.FromResult<StudentEnrollmentDto?>(null);

        public Task<StudentGuardianLinkDto?> GetGuardianLinkDtoAsync(int schoolId, int linkId, DateOnly onDate, CancellationToken cancellationToken) =>
            Task.FromResult<StudentGuardianLinkDto?>(null);

        public Task<IReadOnlyList<GuardianStudentDto>> GetGuardianStudentsAsync(int schoolId, string guardianUserId, DateOnly onDate, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GuardianStudentDto>>(new List<GuardianStudentDto>());

        public Task<GuardianStudentSummaryDto?> GetGuardianStudentSummaryAsync(int schoolId, string guardianUserId, int studentId, DateOnly onDate, CancellationToken cancellationToken) =>
            Task.FromResult<GuardianStudentSummaryDto?>(null);

        public Task<PagedResult<GuardianNotificationDto>> GetGuardianStudentNotificationsAsync(int schoolId, string guardianUserId, int studentId, StudentAffairsPageQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<GuardianNotificationDto>());

        public Task<PagedResult<ClassroomDto>> GetClassroomsAsync(int schoolId, ClassroomListQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<ClassroomDto>());

        public Task<IReadOnlyList<ClassroomAcademicYearDto>> GetClassroomAcademicYearsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ClassroomAcademicYearDto>>(new[]
            {
                new ClassroomAcademicYearDto(3, "2026-2027", "1448 هـ", true)
            });

        public Task<ClassroomDto?> GetClassroomDtoAsync(int schoolId, int classroomId, CancellationToken cancellationToken) =>
            Task.FromResult(Classroom is null ? null : new ClassroomDto(
                Classroom.Id,
                Classroom.ClassLabel,
                Classroom.Stage,
                Classroom.GradeLevel,
                Classroom.Section,
                Classroom.AcademicYearId,
                "2026/2027",
                Classroom.IsActive,
                0,
                string.Empty));

        public Task<Classroom?> GetClassroomForUpdateAsync(int schoolId, int classroomId, CancellationToken cancellationToken) =>
            Task.FromResult(Classroom?.Id == classroomId && Classroom.SchoolId == schoolId ? Classroom : null);

        public Task<bool> AcademicYearExistsAsync(int academicYearId, CancellationToken cancellationToken) =>
            Task.FromResult(AcademicYearExists);

        public Task<bool> ClassroomLabelExistsAsync(int schoolId, int academicYearId, string classLabel, int? excludingClassroomId, CancellationToken cancellationToken) =>
            Task.FromResult(LabelExists);

        public Task<bool> HasActiveClassroomEnrollmentsAsync(int schoolId, int classroomId, CancellationToken cancellationToken) =>
            Task.FromResult(HasActiveEnrollments);

        public Task<int> UnassignActiveClassroomEnrollmentsAsync(int schoolId, int classroomId, DateOnly effectiveOn, DateTimeOffset changedAt, string changedByUserId, CancellationToken cancellationToken)
        {
            UnassignedClassroomEnrollmentCount = HasActiveEnrollments ? 1 : 0;
            return Task.FromResult(UnassignedClassroomEnrollmentCount);
        }

        public Task<int> UnassignActiveStudentEnrollmentsAsync(int schoolId, int studentId, DateOnly effectiveOn, DateTimeOffset changedAt, string changedByUserId, CancellationToken cancellationToken)
        {
            UnassignedStudentEnrollmentCount = 1;
            return Task.FromResult(UnassignedStudentEnrollmentCount);
        }

        public Task<IReadOnlyList<StudentSummaryDto>> GetClassroomStudentsAsync(int schoolId, int classroomId, int? academicTermId, DateOnly onDate, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StudentSummaryDto>>(new List<StudentSummaryDto>());

        public Task<TeacherStudentAffairsDashboardDto> GetTeacherDashboardAsync(int schoolId, string teacherUserId, DateOnly onDate, CancellationToken cancellationToken) =>
            Task.FromResult(new TeacherStudentAffairsDashboardDto(
                new TeacherTopPriorityDto(
                    new TeacherCurrentContextDto(new ActorSummaryDto(teacherUserId, "T", RoleNames.Instructor), DateTimeOffset.UtcNow, "UTC", 1, null, Array.Empty<StudentSummaryDto>(), Array.Empty<string>()),
                    0, 0, Array.Empty<string>()),
                Array.Empty<DashboardCountDto>()));

        public Task<OfficerStudentAffairsDashboardDto> GetOfficerDashboardAsync(int schoolId, DateOnly onDate, CancellationToken cancellationToken) =>
            Task.FromResult(new OfficerStudentAffairsDashboardDto(Array.Empty<DashboardCountDto>(), Array.Empty<DashboardCountDto>()));

        public Task<SocialWorkerStudentAffairsDashboardDto> GetSocialWorkerDashboardAsync(int schoolId, string socialWorkerUserId, DateOnly onDate, CancellationToken cancellationToken) =>
            Task.FromResult(new SocialWorkerStudentAffairsDashboardDto(Array.Empty<DashboardCountDto>(), Array.Empty<DashboardCountDto>()));

        public Task<SecurityStudentAffairsDashboardDto> GetSecurityDashboardAsync(int schoolId, DateOnly onDate, CancellationToken cancellationToken) =>
            Task.FromResult(new SecurityStudentAffairsDashboardDto(Array.Empty<SecurityGatePassQueueItemDto>(), Array.Empty<DashboardCountDto>()));

        public Task<GuardianStudentAffairsDashboardDto> GetGuardianDashboardAsync(int schoolId, string guardianUserId, DateOnly onDate, CancellationToken cancellationToken) =>
            Task.FromResult(new GuardianStudentAffairsDashboardDto(Array.Empty<StudentContextDto>(), Array.Empty<DashboardCountDto>()));

        public Task<SchoolOversightDashboardDto> GetSchoolOversightDashboardAsync(int schoolId, DateOnly onDate, CancellationToken cancellationToken) =>
            Task.FromResult(new SchoolOversightDashboardDto(0, 0, 0, Array.Empty<ClassroomAttendanceAggregateDto>(), Array.Empty<DashboardCountDto>(), Array.Empty<DashboardCountDto>(), DateTimeOffset.UtcNow));

        public void AddStudent(Student student)
        {
            student.Id = 102;
            Student = student;
        }
        public void AddEnrollment(StudentEnrollment enrollment) { }
        public void AddGuardianLink(StudentGuardian link) { }
        public void AddClassroom(Classroom classroom)
        {
            classroom.Id = 101;
            Classroom = classroom;
        }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
