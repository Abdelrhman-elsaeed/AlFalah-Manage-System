using System.Reflection;
using AlFalah.Api.Controllers.StudentAffairs;
using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Classrooms;
using AlFalah.Application.StudentAffairs.DTOs.Attendance;
using AlFalah.Application.StudentAffairs.DTOs.Permits;
using AlFalah.Application.StudentAffairs.DTOs.Referrals;
using AlFalah.Application.StudentAffairs.DTOs.Students;
using AlFalah.Application.StudentAffairs.DTOs.Summons;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace AlFalah.Tests.Security;

public sealed class SecretaryAttendanceAuthorizationTests
{
    [Fact]
    public async Task Classrooms_List_Allows_Secretary_Attendance_Management_Permission()
    {
        var controller = new ClassroomsController(
            CreateMediator(),
            new SecretaryCurrentUser(PermissionNames.AttendanceManageStudents));

        var result = await controller.List(new ClassroomListQuery(), CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Attendance_Sheet_Allows_Secretary_Attendance_Management_Permission()
    {
        var controller = new StudentAttendanceController(
            CreateMediator(),
            new SecretaryCurrentUser(PermissionNames.AttendanceManageStudents));

        var result = await controller.Sheet(new DateOnly(2026, 9, 1), 1, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(200);
    }

    [Theory]
    [InlineData("The attendance roster revision is stale")]
    [InlineData("The Idempotency-Key was already used for a different attendance request")]
    public async Task Attendance_Submit_MapsRevisionAndIdempotencyConflictsTo409(string error)
    {
        var controller = new StudentAttendanceController(
            CreateFailingMediator(error),
            new SecretaryCurrentUser(PermissionNames.AttendanceManageStudents));

        var result = await controller.SubmitSheet(
            new SubmitAbsentRosterRequestDto(new DateOnly(2026, 9, 1), 1, Array.Empty<int>(), "revision"),
            "key",
            CancellationToken.None);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task Classrooms_Create_Allows_Secretary_Classroom_Management_Permission()
    {
        var controller = new ClassroomsController(
            CreateMediator(),
            new SecretaryCurrentUser(PermissionNames.ClassroomManage));

        var result = await controller.Create(
            new CreateClassroomRequestDto(1, SchoolStage.Primary, 1, "أ", "1/أ"),
            CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(201);
    }

    [Fact]
    public async Task Classroom_AcademicYears_Allows_Secretary_Classroom_Management_Permission()
    {
        var controller = new ClassroomsController(
            CreateMediator(),
            new SecretaryCurrentUser(PermissionNames.ClassroomManage));

        var result = await controller.AcademicYears(CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Classrooms_Update_Allows_Secretary_Classroom_Management_Permission()
    {
        var controller = new ClassroomsController(
            CreateMediator(),
            new SecretaryCurrentUser(PermissionNames.ClassroomManage));

        var result = await controller.Update(
            1,
            new UpdateClassroomRequestDto("1/أ", "أ", true, string.Empty),
            CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Classrooms_Delete_Allows_Secretary_Classroom_Management_Permission()
    {
        var controller = new ClassroomsController(
            CreateMediator(),
            new SecretaryCurrentUser(PermissionNames.ClassroomManage));

        var result = await controller.Delete(
            1,
            new DeleteClassroomRequestDto("حذف من إدارة الفصول", string.Empty),
            CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Students_Crud_Allows_Secretary_Student_Management_Permission()
    {
        var controller = new StudentsController(
            CreateMediator(),
            new SecretaryCurrentUser(PermissionNames.StudentManage));

        (await controller.List(new StudentListQuery(), CancellationToken.None))
            .Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(200);
        (await controller.Create(
                new CreateStudentRequestDto("ST-001", "1000000001", "أحمد", null, "علي", null, null, null, 1, null),
                CancellationToken.None))
            .Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(201);
        (await controller.Update(
                1,
                new UpdateStudentRequestDto("ST-001", "1000000001", "أحمد", null, "علي", null, null, null, true, 1, null, string.Empty),
                CancellationToken.None))
            .Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(200);
        (await controller.Delete(
                1,
                new DeleteStudentRequestDto("حذف من إدارة الطلاب", string.Empty),
                CancellationToken.None))
            .Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Students_GuardianLinking_Allows_Secretary_With_StudentManagement_Permission()
    {
        var controller = new StudentsController(
            CreateMediator(),
            new SecretaryCurrentUser(PermissionNames.StudentManage));

        (await controller.GuardianOptions(CancellationToken.None))
            .Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(200);
        (await controller.Guardians(17, CancellationToken.None))
            .Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(200);
        (await controller.LinkGuardian(
                17,
                new LinkStudentGuardianRequestDto(
                    9, GuardianRelationshipType.Father, true, true, true, true,
                    new DateOnly(2026, 10, 2), null),
                CancellationToken.None))
            .Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(201);
        (await controller.RevokeGuardian(
                17,
                3,
                new RevokeStudentGuardianRequestDto("Secretary correction", string.Empty),
                CancellationToken.None))
            .Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Students_Transfer_Allows_Secretary_With_Enrollment_Permission()
    {
        var controller = new StudentsController(
            CreateMediator(),
            new SecretaryCurrentUser(PermissionNames.StudentEnrollmentManage));

        var result = await controller.UpdateEnrollment(
            17,
            4,
            new UpdateStudentEnrollmentRequestDto(
                StudentEnrollmentStatus.Active,
                22,
                new DateOnly(2026, 10, 2),
                "Move to another classroom",
                string.Empty),
            CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Students_GuardianLinking_DoesNotTreat_StudentManage_AsUniversalGuardianPermission()
    {
        var controller = new StudentsController(
            CreateMediator(),
            new SocialWorkerCurrentUser(PermissionNames.StudentManage));

        (await controller.GuardianOptions(CancellationToken.None))
            .Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(403);
        (await controller.LinkGuardian(
                17,
                new LinkStudentGuardianRequestDto(
                    9, GuardianRelationshipType.Father, true, true, true, true,
                    new DateOnly(2026, 10, 2), null),
                CancellationToken.None))
            .Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Secretary_CannotReadReferralCaseActionsOrManageSummons()
    {
        var user = new SecretaryCurrentUser();

        var referrals = await new ReferralsController(CreateMediator(), user)
            .List(new ReferralListQuery(), CancellationToken.None);
        var summons = await new SummonsController(CreateMediator(), user)
            .List(new SummonListQuery(), CancellationToken.None);

        referrals.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
        summons.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Students_Guardians_Allows_SocialWorker_With_Summon_Permission()
    {
        var controller = new StudentsController(
            CreateMediator(),
            new SocialWorkerCurrentUser(PermissionNames.SummonCreate));

        var result = await controller.Guardians(17, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task ClassroomEntryPermitController_NoLongerUsesDeferred501Containment()
    {
        var denied = await new ClassroomEntryPermitsController(CreateMediator(), new SecretaryCurrentUser())
            .List(new ClassroomEntryPermitListQuery(), CancellationToken.None);
        var explicitlyGrantedButDeferred = await new ClassroomEntryPermitsController(
                CreateMediator(),
                new SecretaryCurrentUser(PermissionNames.ClassroomEntryPermitView))
            .List(new ClassroomEntryPermitListQuery(), CancellationToken.None);

        denied.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
        explicitlyGrantedButDeferred.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(200);
    }

    private static IMediator CreateMediator() =>
        DispatchProxy.Create<IMediator, SuccessfulMediatorProxy>();

    private static IMediator CreateFailingMediator(string error)
    {
        var mediator = DispatchProxy.Create<IMediator, FailingMediatorProxy>();
        ((FailingMediatorProxy)(object)mediator).Error = error;
        return mediator;
    }

    private class SuccessfulMediatorProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var returnType = targetMethod?.ReturnType
                ?? throw new InvalidOperationException("The mediator method has no return type.");

            if (!returnType.IsGenericType || returnType.GetGenericTypeDefinition() != typeof(Task<>))
                throw new NotSupportedException($"Unexpected mediator return type: {returnType}.");

            var responseType = returnType.GetGenericArguments()[0];
            var response = Activator.CreateInstance(responseType);
            responseType.GetProperty("IsSuccess")?.SetValue(response, true);
            return typeof(Task)
                .GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(responseType)
                .Invoke(null, new[] { response });
        }
    }

    private class FailingMediatorProxy : DispatchProxy
    {
        public string Error { get; set; } = string.Empty;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var returnType = targetMethod?.ReturnType
                ?? throw new InvalidOperationException("The mediator method has no return type.");
            var responseType = returnType.GetGenericArguments()[0];
            var response = Activator.CreateInstance(responseType);
            responseType.GetProperty("Errors")?.SetValue(response, new List<string> { Error });
            return typeof(Task)
                .GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(responseType)
                .Invoke(null, new[] { response });
        }
    }

    private sealed class SecretaryCurrentUser : ICurrentUserService
    {
        private readonly HashSet<string> _permissions;

        public SecretaryCurrentUser(params string[] permissions) =>
            _permissions = new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);

        public string? UserId => "secretary-test";
        public string? Username => "secretary.test";
        public int? ActiveSchoolId => 18;
        public string? PreferredLanguage => "ar";
        public bool IsAuthenticated => true;
        public bool IsInRole(string roleName) =>
            string.Equals(roleName, RoleNames.Secretary, StringComparison.OrdinalIgnoreCase);
        public bool HasPermission(string permissionName) => _permissions.Contains(permissionName);
        public IEnumerable<string> GetRoles() => new[] { RoleNames.Secretary };
        public IEnumerable<string> GetPermissions() => _permissions;
        public bool IsGlobalAdmin() => false;
        public bool IsSchoolScopedRole() => true;
    }

    private sealed class SocialWorkerCurrentUser : ICurrentUserService
    {
        private readonly HashSet<string> _permissions;

        public SocialWorkerCurrentUser(params string[] permissions) =>
            _permissions = new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);

        public string? UserId => "social-worker-test";
        public string? Username => "socialworker.test";
        public int? ActiveSchoolId => 18;
        public string? PreferredLanguage => "ar";
        public bool IsAuthenticated => true;
        public bool IsInRole(string roleName) =>
            string.Equals(roleName, RoleNames.SocialWorker, StringComparison.OrdinalIgnoreCase);
        public bool HasPermission(string permissionName) => _permissions.Contains(permissionName);
        public IEnumerable<string> GetRoles() => new[] { RoleNames.SocialWorker };
        public IEnumerable<string> GetPermissions() => _permissions;
        public bool IsGlobalAdmin() => false;
        public bool IsSchoolScopedRole() => true;
    }
}
