using System.Security.Claims;
using System.Text.Json;
using AlFalah.Application.DTOs.ImprovementPlans;
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

public sealed class ImprovementPlanOwnershipTests
{
    [Fact]
    public async Task P01_CreatePlan_Ignores_Client_Instructor_And_Derives_All_Ownership_From_Visit()
    {
        await using var context = CreateContext();
        await SeedVisitGraphAsync(context);
        var service = CreateService(context);

        const string json = """
            {
              "InstructorId": "ATTACKER-CONTROLLED-INSTRUCTOR",
              "VisitId": 100,
              "DomainId": 10,
              "Goal": "هدف اختباري",
              "Actions": "إجراءات اختبارية",
              "StartDate": "2026-07-15T00:00:00+00:00",
              "EndDate": "2026-08-15T00:00:00+00:00",
              "SuccessIndicators": "مؤشرات نجاح اختبارية"
            }
            """;
        var request = JsonSerializer.Deserialize<CreatePlanRequestDto>(json)!;

        var result = await service.CreatePlanAsync(request);
        var persisted = await context.ImprovementPlans.SingleAsync();

        result.VisitId.Should().Be(100);
        result.SchoolId.Should().Be(7);
        result.InstructorId.Should().Be("INSTRUCTOR-FROM-VISIT");
        result.DomainId.Should().Be(10);
        persisted.VisitId.Should().Be(100);
        persisted.SchoolId.Should().Be(7);
        persisted.InstructorId.Should().Be("INSTRUCTOR-FROM-VISIT");
        typeof(CreatePlanRequestDto).GetProperty("InstructorId").Should().BeNull(
            because: "plan ownership must not be part of the client-writable contract");
    }

    [Fact]
    public async Task P01_CreatePlan_Rejects_Domain_From_A_Different_Rubric_Snapshot()
    {
        await using var context = CreateContext();
        await SeedVisitGraphAsync(context);
        var service = CreateService(context);
        var request = ValidRequest(domainId: 20);

        var act = () => service.CreatePlanAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("النطاق المحدد لا ينتمي إلى نسخة أداة التقييم الخاصة بهذه الزيارة.");
        (await context.ImprovementPlans.CountAsync()).Should().Be(0);
    }

    private static AlFalahDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase($"plan-ownership-{Guid.NewGuid()}")
            .Options;
        return new AlFalahDbContext(options);
    }

    private static async Task SeedVisitGraphAsync(AlFalahDbContext context)
    {
        var creator = new ApplicationUser
        {
            Id = "MODERATOR-1",
            UserName = "moderator",
            FirstName = "مشرف",
            LastName = "اختبار"
        };
        var instructor = new ApplicationUser
        {
            Id = "INSTRUCTOR-FROM-VISIT",
            UserName = "instructor",
            FirstName = "معلم",
            LastName = "اختبار"
        };
        var school = new School { Id = 7, Name = "مدرسة الاختبار", City = "الرياض" };
        var visitRubric = new RubricVersion { Id = 1, VersionNumber = 1, IsActive = true };
        var otherRubric = new RubricVersion { Id = 2, VersionNumber = 2 };
        context.AddRange(
            creator,
            instructor,
            school,
            visitRubric,
            otherRubric,
            new RubricDomain
            {
                Id = 10,
                RubricVersionId = visitRubric.Id,
                Code = "D1",
                NameAr = "بيئة التعلم",
                SortOrder = 1
            },
            new RubricDomain
            {
                Id = 20,
                RubricVersionId = otherRubric.Id,
                Code = "D1",
                NameAr = "نطاق من نسخة أخرى",
                SortOrder = 1
            },
            new Visit
            {
                Id = 100,
                SchoolId = school.Id,
                InstructorId = instructor.Id,
                CreatedByUserId = creator.Id,
                RubricVersionId = visitRubric.Id,
                Status = VisitStatus.Approved,
                VisitDate = DateTimeOffset.UtcNow
            });
        await context.SaveChangesAsync();
    }

    private static ImprovementPlanService CreateService(AlFalahDbContext context)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "MODERATOR-1"),
            new Claim(ClaimTypes.Role, RoleNames.Moderator),
            new Claim("active_school_id", "7")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        var currentUser = new CurrentUserService(accessor);
        var guard = new SchoolScopeGuard(
            context,
            currentUser,
            NullLogger<SchoolScopeGuard>.Instance);
        return new ImprovementPlanService(
            context,
            currentUser,
            guard,
            NullLogger<ImprovementPlanService>.Instance);
    }

    private static CreatePlanRequestDto ValidRequest(int? domainId) => new()
    {
        VisitId = 100,
        DomainId = domainId,
        Goal = "هدف اختباري",
        Actions = "إجراءات اختبارية",
        StartDate = DateTimeOffset.UtcNow,
        EndDate = DateTimeOffset.UtcNow.AddMonths(1),
        SuccessIndicators = "مؤشرات نجاح اختبارية"
    };
}
