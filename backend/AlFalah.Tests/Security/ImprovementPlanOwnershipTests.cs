using System.Security.Claims;
using System.Text.Json;
using AlFalah.Application.DTOs.ImprovementPlans;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Services;
using AlFalah.Application.Validators.ImprovementPlans;
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
    public async Task CreatePlan_AppearsImmediatelyInCurrentPlansForTheSameVisit()
    {
        await using var context = CreateContext();
        await SeedVisitGraphAsync(context);
        var service = CreateService(context);

        var created = await service.CreatePlanAsync(ValidRequest(10));
        var currentPlans = await service.GetPlansForVisitAsync(100);

        currentPlans.Should().ContainSingle(plan => plan.Id == created.Id);
        currentPlans.Single(plan => plan.Id == created.Id).Status.Should().Be("active");
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

    [Fact]
    public async Task Phase3_WeakDomain_Suggestion_Matches_Verbatim_Template()
    {
        await using var context = CreateContext();
        await SeedVisitGraphAsync(context);
        var suggestions = await CreateService(context).GetWeakDomainSuggestionsAsync(100);

        suggestions.Should().ContainSingle();
        var suggestion = suggestions.Single();
        suggestion.DomainCode.Should().Be("D1");
        suggestion.AverageScore.Should().Be(2.25m);
        suggestion.PrefilledGoal.Should().Be("تحسين جودة بيئة التعلم وجعلها أكثر إثراءً وفاعلية للمتعلمين");
        suggestion.PrefilledActions.Should().Be(
            "- مراجعة توزيع المقاعد وترتيب الغرفة الصفية\n- إضافة مصادر تعلم متنوعة ومناسبة\n- تعزيز جانب القيم والهوية الوطنية في الديكور التعليمي\n- تطبيق استراتيجيات إدارة الوقت الصفي");
        suggestion.PrefilledSuccessIndicators.Should().Be(
            "ارتفاع متوسط درجات نطاق بيئة التعلم إلى 3.0 أو أعلى في الزيارة القادمة");
    }

    [Fact]
    public async Task D5_ClosedPlan_Is_ReadOnly_Until_Explicit_Reactivation()
    {
        await using var context = CreateContext();
        await SeedVisitGraphAsync(context);
        var service = CreateService(context);
        var created = await service.CreatePlanAsync(ValidRequest(10));

        var closed = await service.UpdatePlanAsync(created.Id, UpdateRequest("completed"));
        closed.Status.Should().Be("completed");
        closed.IsReadOnly.Should().BeTrue();

        await FluentActions.Awaiting(() => service.UpdatePlanAsync(created.Id, UpdateRequest("active")))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*للقراءة فقط*");
        await FluentActions.Awaiting(() => service.AddFollowUpAsync(created.Id, new CreateFollowUpRequestDto
        {
            FollowDate = DateTimeOffset.UtcNow,
            ProgressNote = "متابعة"
        })).Should().ThrowAsync<InvalidOperationException>().WithMessage("*للقراءة فقط*");

        var reactivated = await service.ReactivatePlanAsync(created.Id);
        reactivated.Status.Should().Be("active");
        reactivated.IsReadOnly.Should().BeFalse();
        (await service.AddFollowUpAsync(created.Id, new CreateFollowUpRequestDto
        {
            FollowDate = DateTimeOffset.UtcNow,
            ProgressNote = "متابعة بعد إعادة التنشيط",
            ProgressScore = 75
        })).ProgressScore.Should().Be(75);
    }

    [Fact]
    public async Task Phase3_EndDate_Before_StartDate_Is_Rejected_By_Api_Validator()
    {
        var request = ValidRequest(10);
        request.StartDate = DateTimeOffset.Parse("2026-09-01T00:00:00Z");
        request.EndDate = DateTimeOffset.Parse("2026-08-31T00:00:00Z");

        var result = await new CreatePlanRequestDtoValidator().ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(request.EndDate));
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
            },
            new VisitAnalysis
            {
                Id = 1,
                VisitId = 100,
                OverallScore = 2.25m,
                PerformanceLevelAr = "متحقق جزئياً",
                StrengthsJson = "[]",
                ImprovementAreasJson = "[]",
                PriorityStandardsJson = "[]",
                DomainAverages = new List<VisitDomainAverage>
                {
                    new()
                    {
                        Id = 1,
                        RubricDomainId = 10,
                        DomainCode = "D1",
                        DomainNameAr = "بيئة التعلم",
                        AverageScore = 2.25m
                    }
                }
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

    private static UpdatePlanRequestDto UpdateRequest(string status) => new()
    {
        Goal = "هدف محدث",
        Actions = "إجراءات محدثة",
        StartDate = DateTimeOffset.UtcNow,
        EndDate = DateTimeOffset.UtcNow.AddMonths(1),
        SuccessIndicators = "مؤشرات محدثة",
        Status = status
    };
}
