using System.Reflection;
using AlFalah.Api.Controllers.StudentAffairs;
using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Classrooms;
using AlFalah.Application.StudentAffairs.DTOs.Students;
using AlFalah.Domain.Enums;
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

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Attendance_Sheet_Allows_Secretary_Attendance_Management_Permission()
    {
        var controller = new StudentAttendanceController(
            CreateMediator(),
            new SecretaryCurrentUser(PermissionNames.AttendanceManageStudents));

        var result = await controller.Sheet(new DateOnly(2026, 9, 1), 1, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
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

        result.Should().BeOfType<OkObjectResult>();
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

        result.Should().BeOfType<OkObjectResult>();
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

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Students_Crud_Allows_Secretary_Student_Management_Permission()
    {
        var controller = new StudentsController(
            CreateMediator(),
            new SecretaryCurrentUser(PermissionNames.StudentManage));

        (await controller.List(new StudentListQuery(), CancellationToken.None))
            .Should().BeOfType<OkObjectResult>();
        (await controller.Create(
                new CreateStudentRequestDto("ST-001", "1000000001", "أحمد", null, "علي", null, null, null, 1, null),
                CancellationToken.None))
            .Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(201);
        (await controller.Update(
                1,
                new UpdateStudentRequestDto("ST-001", "1000000001", "أحمد", null, "علي", null, null, null, true, 1, null, string.Empty),
                CancellationToken.None))
            .Should().BeOfType<OkObjectResult>();
        (await controller.Delete(
                1,
                new DeleteStudentRequestDto("حذف من إدارة الطلاب", string.Empty),
                CancellationToken.None))
            .Should().BeOfType<OkObjectResult>();
    }

    private static IMediator CreateMediator() =>
        DispatchProxy.Create<IMediator, SuccessfulMediatorProxy>();

    private class SuccessfulMediatorProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var returnType = targetMethod?.ReturnType
                ?? throw new InvalidOperationException("The mediator method has no return type.");

            if (!returnType.IsGenericType || returnType.GetGenericTypeDefinition() != typeof(Task<>))
                throw new NotSupportedException($"Unexpected mediator return type: {returnType}.");

            var responseType = returnType.GetGenericArguments()[0];
            var response = responseType.IsValueType ? Activator.CreateInstance(responseType) : null;
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
}
