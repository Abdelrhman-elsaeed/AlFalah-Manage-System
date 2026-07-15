using System.Reflection;
using System.Security.Claims;
using AlFalah.Api.Controllers;
using AlFalah.Application.DTOs.Teachers;
using AlFalah.Application.DTOs.Users;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Data.Seeders;
using AlFalah.Infrastructure.Services;
using AlFalah.Shared.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AlFalah.Tests.Security;

public sealed class ModeratorTeacherAccessTests
{
    [Fact]
    public async Task P03_Moderator_TeacherList_Returns_Only_ActiveSchool_Teachers()
    {
        await using var context = await CreateTeacherContextAsync();
        var currentUser = Moderator();
        var controller = CreateTeachersController(context, currentUser);

        var action = await controller.List(page: 1, pageSize: 20, search: null);

        var ok = action.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should()
            .BeOfType<ApiResponse<PagedResult<TeacherListItemDto>>>().Subject;
        response.IsSuccess.Should().BeTrue();
        response.Data!.Items.Should().ContainSingle();
        response.Data.Items.Single().UserId.Should().Be("TEACHER-IN-SCHOOL");
        response.Data.Items.Single().SchoolId.Should().Be(1);
    }

    [Fact]
    public async Task P03_Moderator_Can_Open_InSchool_Profile_To_Start_Visit_But_CrossSchool_Is_403()
    {
        await using var context = await CreateTeacherContextAsync();
        var currentUser = Moderator();
        var controller = CreateTeachersController(context, currentUser);

        var inSchool = await controller.GetProfile("TEACHER-IN-SCHOOL", default);
        var ok = inSchool.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<ApiResponse<TeacherProfileDto>>().Subject;
        response.Data!.SchoolId.Should().Be(1);
        currentUser.HasPermission(PermissionNames.VisitCreate).Should().BeTrue(
            because: "the profile's زيارة جديدة action reuses the existing visit-create route");

        var crossSchool = await controller.GetProfile("TEACHER-OTHER-SCHOOL", default);
        AssertStatus(crossSchool, StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task P03_Moderator_Has_No_Broad_User_Directory_Or_Teacher_Mutation_Access()
    {
        var currentUser = Moderator();
        var controller = new UsersController(userService: null!, currentUser);
        SetHttpContext(controller);

        AssertStatus(await controller.List(new UserListQuery(), default), StatusCodes.Status403Forbidden);
        AssertStatus(await controller.GetById("TEACHER-IN-SCHOOL", default), StatusCodes.Status403Forbidden);
        AssertStatus(
            await controller.Update("TEACHER-IN-SCHOOL", new UserUpdateRequestDto(), default),
            StatusCodes.Status403Forbidden);
        AssertStatus(
            await controller.Deactivate("TEACHER-IN-SCHOOL", default),
            StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task P03_Moderator_Remains_Forbidden_From_Complaints()
    {
        var currentUser = Moderator();
        var controller = new ComplaintsController(complaintService: null!, currentUser);
        SetHttpContext(controller);

        AssertStatus(await controller.List(status: null, default), StatusCodes.Status403Forbidden);
        AssertStatus(await controller.GetById(1, default), StatusCodes.Status403Forbidden);
    }

    [Fact]
    public void P03_Seeder_Grants_Only_Narrow_Teacher_Read_To_Moderator()
    {
        var method = typeof(DatabaseSeeder).GetMethod(
            "GetRolePermissionMap",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();
        var map = method!.Invoke(null, null)
            .Should().BeAssignableTo<Dictionary<string, IEnumerable<string>>>().Subject;
        var permissions = map[RoleNames.Moderator].ToHashSet();

        permissions.Should().Contain(PermissionNames.InstructorView);
        permissions.Should().NotContain(PermissionNames.UserView);
        permissions.Should().NotContain(PermissionNames.UserEdit);
        permissions.Should().NotContain(PermissionNames.UserDelete);
        permissions.Should().NotContain(PermissionNames.ComplaintView);
        permissions.Should().NotContain(PermissionNames.ComplaintManage);
    }

    private static async Task<AlFalahDbContext> CreateTeacherContextAsync()
    {
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase($"moderator-teachers-{Guid.NewGuid()}")
            .Options;
        var context = new AlFalahDbContext(options);
        var instructorRole = new ApplicationRole
        {
            Id = "ROLE-INSTRUCTOR",
            Name = RoleNames.Instructor,
            NormalizedName = RoleNames.Instructor.ToUpperInvariant()
        };
        context.AddRange(
            instructorRole,
            User("TEACHER-IN-SCHOOL", "معلم المدرسة"),
            User("TEACHER-OTHER-SCHOOL", "معلم مدرسة أخرى"),
            new School { Id = 1, Name = "مدرسة المشرف", City = "الرياض" },
            new School { Id = 2, Name = "مدرسة أخرى", City = "جدة" },
            new UserSchoolRole
            {
                Id = 1,
                UserId = "TEACHER-IN-SCHOOL",
                SchoolId = 1,
                RoleId = instructorRole.Id,
                IsActive = true
            },
            new UserSchoolRole
            {
                Id = 2,
                UserId = "TEACHER-OTHER-SCHOOL",
                SchoolId = 2,
                RoleId = instructorRole.Id,
                IsActive = true
            });
        await context.SaveChangesAsync();
        return context;
    }

    private static ApplicationUser User(string id, string fullName)
    {
        var parts = fullName.Split(' ', 2);
        return new ApplicationUser
        {
            Id = id,
            UserName = id,
            NormalizedUserName = id,
            FirstName = parts[0],
            LastName = parts.Length > 1 ? parts[1] : string.Empty
        };
    }

    private static ICurrentUserService Moderator()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "MODERATOR-1"),
            new(ClaimTypes.Role, RoleNames.Moderator),
            new("active_school_id", "1"),
            new("permission", PermissionNames.InstructorView),
            new("permission", PermissionNames.VisitView),
            new("permission", PermissionNames.VisitCreate)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "integration-test"));
        return new CurrentUserService(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        });
    }

    private static TeachersController CreateTeachersController(
        AlFalahDbContext context,
        ICurrentUserService currentUser)
    {
        var accessor = new HttpContextAccessor();
        var guard = new SchoolScopeGuard(
            context,
            currentUser,
            NullLogger<SchoolScopeGuard>.Instance);
        var service = new TeacherService(
            context,
            userManager: null!,
            currentUser,
            guard,
            new AuditLogWriter(context, accessor, NullLogger<AuditLogWriter>.Instance),
            NullLogger<TeacherService>.Instance);
        var controller = new TeachersController(service, currentUser);
        SetHttpContext(controller);
        return controller;
    }

    private static void SetHttpContext(ControllerBase controller)
    {
        var services = new ServiceCollection().BuildServiceProvider();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services }
        };
    }

    private static void AssertStatus(IActionResult result, int expectedStatus)
    {
        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(expectedStatus);
    }
}
