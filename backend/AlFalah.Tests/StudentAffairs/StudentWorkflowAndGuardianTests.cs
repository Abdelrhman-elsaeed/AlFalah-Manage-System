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
using AlFalah.Application.StudentAffairs.Students;
using AlFalah.Application.StudentAffairs.Students.Handlers;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using FluentAssertions;
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
            new StudentSummaryDto(1, "ST-001", "Student One", 2, "1/A", true, null),
            "Student",
            null,
            "One",
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
        public StudentDetailsDto? StudentDetails { get; set; }

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
            Task.FromResult<Student?>(null);

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

        public Task<ClassroomDto?> GetClassroomDtoAsync(int schoolId, int classroomId, CancellationToken cancellationToken) =>
            Task.FromResult<ClassroomDto?>(null);

        public Task<Classroom?> GetClassroomForUpdateAsync(int schoolId, int classroomId, CancellationToken cancellationToken) =>
            Task.FromResult<Classroom?>(null);

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

        public void AddStudent(Student student) { }
        public void AddEnrollment(StudentEnrollment enrollment) { }
        public void AddGuardianLink(StudentGuardian link) { }
        public void AddClassroom(Classroom classroom) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
