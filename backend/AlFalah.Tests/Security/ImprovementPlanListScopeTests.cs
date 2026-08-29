using System.Security.Claims;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AlFalah.Tests.Security;

public sealed class ImprovementPlanListScopeTests
{
    [Fact]
    public async Task SchoolManager_GlobalPlanList_Contains_Only_The_Active_School()
    {
        await using var context = CreateContext();
        await SeedTwoSchoolsAsync(context);
        var service = CreateService(context, RoleNames.SchoolManager, activeSchoolId: 7);

        var plans = await service.GetPlansAsync();

        plans.Should().ContainSingle();
        plans[0].SchoolId.Should().Be(7);
        plans[0].InstructorFullName.Should().Be("معلم الأولى");
    }

    private static AlFalahDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase($"plan-list-scope-{Guid.NewGuid()}")
            .Options;
        return new AlFalahDbContext(options);
    }

    private static async Task SeedTwoSchoolsAsync(AlFalahDbContext context)
    {
        var manager = new ApplicationUser { Id = "MANAGER", UserName = "manager", FirstName = "مدير", LastName = "اختبار" };
        var firstInstructor = new ApplicationUser { Id = "I-1", UserName = "i1", FirstName = "معلم", LastName = "الأولى" };
        var secondInstructor = new ApplicationUser { Id = "I-2", UserName = "i2", FirstName = "معلم", LastName = "الثانية" };
        var firstSchool = new School { Id = 7, Name = "المدرسة الأولى", City = "القاهرة" };
        var secondSchool = new School { Id = 8, Name = "المدرسة الثانية", City = "الجيزة" };
        var rubric = new RubricVersion { Id = 1, VersionNumber = 1, IsActive = true };

        context.AddRange(manager, firstInstructor, secondInstructor, firstSchool, secondSchool, rubric);
        context.Visits.AddRange(
            new Visit
            {
                Id = 101, SchoolId = 7, InstructorId = firstInstructor.Id, CreatedByUserId = manager.Id,
                RubricVersionId = rubric.Id, Status = VisitStatus.Approved, VisitDate = DateTimeOffset.UtcNow
            },
            new Visit
            {
                Id = 102, SchoolId = 8, InstructorId = secondInstructor.Id, CreatedByUserId = manager.Id,
                RubricVersionId = rubric.Id, Status = VisitStatus.Approved, VisitDate = DateTimeOffset.UtcNow
            });
        context.ImprovementPlans.AddRange(
            NewPlan(1, 7, firstInstructor.Id, 101, manager.Id),
            NewPlan(2, 8, secondInstructor.Id, 102, manager.Id));
        await context.SaveChangesAsync();
    }

    private static ImprovementPlan NewPlan(int id, int schoolId, string instructorId, int visitId, string creatorId) => new()
    {
        Id = id,
        SchoolId = schoolId,
        InstructorId = instructorId,
        VisitId = visitId,
        Goal = $"هدف {id}",
        Actions = "إجراءات",
        StartDate = DateTimeOffset.UtcNow,
        EndDate = DateTimeOffset.UtcNow.AddMonths(1),
        SuccessIndicators = "مؤشرات",
        Status = PlanStatus.Active,
        CreatedByUserId = creatorId
    };

    private static ImprovementPlanService CreateService(AlFalahDbContext context, string role, int activeSchoolId)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "MANAGER"),
            new Claim(ClaimTypes.Role, role),
            new Claim("active_school_id", activeSchoolId.ToString())
        }, "test"));
        var currentUser = new CurrentUserService(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        });
        return new ImprovementPlanService(
            context,
            currentUser,
            new SchoolScopeGuard(context, currentUser, NullLogger<SchoolScopeGuard>.Instance),
            NullLogger<ImprovementPlanService>.Instance);
    }
}
