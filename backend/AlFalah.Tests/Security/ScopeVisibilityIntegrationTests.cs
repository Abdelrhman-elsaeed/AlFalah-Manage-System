using System.Security.Claims;
using AlFalah.Application.Common;
using AlFalah.Application.DTOs.ImprovementPlans;
using AlFalah.Application.DTOs.Teachers;
using AlFalah.Application.DTOs.Visits;
using AlFalah.Application.Interfaces;
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

/// <summary>
/// Service-level integration coverage for the server-authoritative visibility
/// boundary. Every test uses the real EF Core DbContext and real production
/// services; forbidden calls therefore exercise the same D-24/D-28/D-36/D-37,
/// D-53 and D-75 guards used by the API endpoints.
/// </summary>
public sealed class ScopeVisibilityIntegrationTests
{
    [Fact]
    public async Task Phase1_VisitMetadata_RoundTrips_With_Dynamic_Rubric_Count()
    {
        await using var context = await CreateSeededContextAsync();
        var service = CreateVisitService(context, User(RoleNames.Moderator, "MOD-1", 1));

        var created = await service.CreateAsync(new CreateVisitRequestDto
        {
            InstructorId = "TEACHER-A",
            VisitCategory = (int)VisitCategory.ClassroomOrPeriodic,
            VisitSequence = (int)VisitSequence.First,
            VisitDate = new DateTimeOffset(2026, 7, 15, 8, 0, 0, TimeSpan.Zero),
            Subject = "الرياضيات",
            GradeClass = "الصف الأول",
            LessonTitle = "الجمع حتى 100",
            PresentCount = 24,
            AbsentCount = 2,
            Notes = "ملاحظة أولية"
        });

        created.LessonTitle.Should().Be("الجمع حتى 100");
        created.PresentCount.Should().Be(24);
        created.AbsentCount.Should().Be(2);
        created.Scores.Should().HaveCount(2, "the active rubric snapshot has two dynamic standards");

        var updated = await service.UpdateAsync(created.Id, new UpdateVisitRequestDto
        {
            VisitCategory = (int)VisitCategory.ClassroomOrPeriodic,
            VisitSequence = (int)VisitSequence.First,
            VisitDate = created.VisitDate,
            Subject = "الرياضيات",
            GradeClass = "الصف الأول",
            LessonTitle = "الجمع والطرح حتى 100",
            PresentCount = 23,
            AbsentCount = null,
            Notes = "ملاحظة محدثة",
            Scores = created.Scores.Select(score => new VisitScoreInputDto
            {
                RubricStandardId = score.RubricStandardId,
                Score = 3,
                EvidenceNote = "شاهد صفّي"
            }).ToList()
        });

        updated.LessonTitle.Should().Be("الجمع والطرح حتى 100");
        updated.PresentCount.Should().Be(23);
        updated.AbsentCount.Should().Be(0, "an omitted absence count defaults to zero");
        updated.Scores.Should().OnlyContain(score => score.Score == 3 && score.EvidenceNote == "شاهد صفّي");
    }

    [Fact]
    public async Task D36_Instructor_Cannot_Use_VisitDetail_But_Can_Open_Own_Approved_Report()
    {
        await using var context = await CreateSeededContextAsync();
        var service = CreateVisitService(context, User(RoleNames.Instructor, "TEACHER-A", 1));

        var detail = () => service.GetByIdAsync(100);
        await detail.Should().ThrowAsync<UnauthorizedSchoolAccessException>()
            .WithMessage("*صفحة الزيارات للمشرفين*");

        var report = await service.GetInstructorReportAsync(100);
        report.VisitId.Should().Be(100);
        report.InstructorId.Should().Be("TEACHER-A");
        (await context.ReportViewLogs.CountAsync(x => x.VisitId == 100)).Should().Be(1);
    }

    [Fact]
    public async Task D36_Instructor_Cannot_Open_Others_Or_NonApproved_Report_And_Pdf()
    {
        await using var context = await CreateSeededContextAsync();
        var service = CreateVisitService(context, User(RoleNames.Instructor, "TEACHER-A", 1));

        await AssertForbiddenAsync(() => service.GetInstructorReportAsync(102));
        await AssertForbiddenAsync(() => service.GetVisitReportAsync(102));
        await AssertForbiddenAsync(() => service.GetInstructorReportAsync(101));
        await AssertForbiddenAsync(() => service.GetVisitReportAsync(101));
    }

    [Fact]
    public async Task D37_Moderator_Cannot_Open_OtherModerator_VisitDetail_Or_PdfReport()
    {
        await using var context = await CreateSeededContextAsync();
        var service = CreateVisitService(context, User(RoleNames.Moderator, "MOD-1", 1));

        await AssertForbiddenAsync(() => service.GetByIdAsync(102));
        await AssertForbiddenAsync(() => service.GetVisitReportAsync(102));
    }

    [Fact]
    public async Task D24_D28_CrossSchool_VisitDetail_And_PdfReport_Are_Forbidden()
    {
        await using var context = await CreateSeededContextAsync();
        var service = CreateVisitService(context, User(RoleNames.SchoolManager, "MANAGER-1", 1));

        await AssertForbiddenAsync(() => service.GetByIdAsync(200));
        await AssertForbiddenAsync(() => service.GetVisitReportAsync(200));
    }

    [Fact]
    public async Task D36_Instructor_Plan_Read_Is_OwnApprovedOnly_And_All_Mutations_Are_Forbidden()
    {
        await using var context = await CreateSeededContextAsync();
        var service = CreatePlanService(context, User(RoleNames.Instructor, "TEACHER-A", 1));

        var approved = await service.GetPlanByIdAsync(1000);
        approved.IsReadOnly.Should().BeTrue();
        approved.InstructorId.Should().Be("TEACHER-A");

        await AssertForbiddenAsync(() => service.GetPlanByIdAsync(1001));
        await AssertForbiddenAsync(() => service.GetPlanByIdAsync(1002));
        await AssertForbiddenAsync(() => service.UpdatePlanAsync(1000, UpdatePlan()));
        await AssertForbiddenAsync(() => service.SoftDeletePlanAsync(1000));
        await AssertForbiddenAsync(() => service.AddFollowUpAsync(1000, CreateFollowUp()));
    }

    [Fact]
    public async Task D37_Moderator_PlanCrud_And_FollowUpCrud_Reject_OtherModerator_Visit()
    {
        await using var context = await CreateSeededContextAsync();
        var service = CreatePlanService(context, User(RoleNames.Moderator, "MOD-1", 1));

        await AssertForbiddenAsync(() => service.GetPlanByIdAsync(1002));
        await AssertForbiddenAsync(() => service.UpdatePlanAsync(1002, UpdatePlan()));
        await AssertForbiddenAsync(() => service.SoftDeletePlanAsync(1002));
        await AssertForbiddenAsync(() => service.AddFollowUpAsync(1002, CreateFollowUp()));
        await AssertForbiddenAsync(() => service.UpdateFollowUpAsync(2002, UpdateFollowUp()));
        await AssertForbiddenAsync(() => service.SoftDeleteFollowUpAsync(2002));
    }

    [Fact]
    public async Task D24_D28_CrossSchool_PlanCrud_And_FollowUpCrud_Are_Forbidden()
    {
        await using var context = await CreateSeededContextAsync();
        var service = CreatePlanService(context, User(RoleNames.SchoolManager, "MANAGER-1", 1));

        await AssertForbiddenAsync(() => service.GetPlanByIdAsync(1003));
        await AssertForbiddenAsync(() => service.UpdatePlanAsync(1003, UpdatePlan()));
        await AssertForbiddenAsync(() => service.SoftDeletePlanAsync(1003));
        await AssertForbiddenAsync(() => service.AddFollowUpAsync(1003, CreateFollowUp()));
        await AssertForbiddenAsync(() => service.UpdateFollowUpAsync(2003, UpdateFollowUp()));
        await AssertForbiddenAsync(() => service.SoftDeleteFollowUpAsync(2003));
    }

    [Fact]
    public async Task D24_D28_TeacherProfile_Is_200_InSchool_And_403_CrossSchool()
    {
        await using var context = await CreateSeededContextAsync();
        var currentUser = User(RoleNames.SchoolManager, "MANAGER-1", 1);
        var service = CreateTeacherService(context, currentUser);

        var inSchool = await service.GetProfileAsync("TEACHER-A");
        inSchool.SchoolId.Should().Be(1);
        inSchool.UserId.Should().Be("TEACHER-A");

        await AssertForbiddenAsync(() => service.GetProfileAsync("TEACHER-B"));
    }

    [Fact]
    public async Task D75_Moderator_Cannot_List_Or_Open_Any_Complaint()
    {
        await using var context = await CreateSeededContextAsync();
        var service = CreateComplaintService(context, User(RoleNames.Moderator, "MOD-1", 1));

        await AssertForbiddenAsync(() => service.ListAsync(null));
        await AssertForbiddenAsync(() => service.GetByIdAsync(3000));
    }

    [Fact]
    public async Task D53_MainManager_Cannot_Open_Complaint_Details()
    {
        await using var context = await CreateSeededContextAsync();
        var service = CreateComplaintService(context, User(RoleNames.MainManager, "MAIN-1", null));

        await AssertForbiddenAsync(() => service.GetByIdAsync(3000));
    }

    private static async Task<AlFalahDbContext> CreateSeededContextAsync()
    {
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase($"scope-visibility-{Guid.NewGuid()}")
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
            AppUser("TEACHER-A", "معلم", "أ"),
            AppUser("TEACHER-B", "معلم", "ب"),
            AppUser("MOD-1", "مشرف", "أ"),
            AppUser("MOD-2", "مشرف", "ب"),
            AppUser("MANAGER-1", "مدير", "أ"),
            new School { Id = 1, Name = "المدرسة الأولى", City = "الرياض" },
            new School { Id = 2, Name = "المدرسة الثانية", City = "جدة" },
            new RubricVersion { Id = 1, VersionNumber = 1, IsActive = true },
            new RubricDomain
            {
                Id = 10,
                RubricVersionId = 1,
                Code = "D1",
                NameAr = "بيئة التعلم",
                SortOrder = 1
            },
            new RubricStandard
            {
                Id = 11,
                RubricDomainId = 10,
                Code = "D1-S1",
                TextAr = "المعيار الأول",
                SortOrder = 1
            },
            new RubricStandard
            {
                Id = 12,
                RubricDomainId = 10,
                Code = "D1-S2",
                TextAr = "المعيار الثاني",
                SortOrder = 2
            });
        context.UserSchoolRoles.AddRange(
            Assignment(1, "TEACHER-A", 1, instructorRole.Id),
            Assignment(2, "TEACHER-B", 2, instructorRole.Id));
        context.Visits.AddRange(
            Visit(100, 1, "TEACHER-A", "MOD-1", VisitStatus.Approved),
            Visit(101, 1, "TEACHER-A", "MOD-1", VisitStatus.PendingApproval),
            Visit(102, 1, "TEACHER-B", "MOD-2", VisitStatus.Approved),
            Visit(200, 2, "TEACHER-B", "MOD-2", VisitStatus.Approved));
        context.ImprovementPlans.AddRange(
            Plan(1000, 100, 1, "TEACHER-A", "MOD-1"),
            Plan(1001, 101, 1, "TEACHER-A", "MOD-1"),
            Plan(1002, 102, 1, "TEACHER-B", "MOD-2"),
            Plan(1003, 200, 2, "TEACHER-B", "MOD-2"));
        context.PlanFollowUps.AddRange(
            FollowUp(2002, 1002, "MOD-2"),
            FollowUp(2003, 1003, "MOD-2"));
        await context.SaveChangesAsync();
        return context;
    }

    private static ApplicationUser AppUser(string id, string firstName, string lastName) => new()
    {
        Id = id,
        UserName = id,
        NormalizedUserName = id,
        FirstName = firstName,
        LastName = lastName,
        Email = $"{id.ToLowerInvariant()}@example.test"
    };

    private static UserSchoolRole Assignment(int id, string userId, int schoolId, string roleId) => new()
    {
        Id = id,
        UserId = userId,
        SchoolId = schoolId,
        RoleId = roleId,
        IsActive = true
    };

    private static Visit Visit(
        int id,
        int schoolId,
        string instructorId,
        string creatorId,
        VisitStatus status) => new()
    {
        Id = id,
        SchoolId = schoolId,
        InstructorId = instructorId,
        CreatedByUserId = creatorId,
        RubricVersionId = 1,
        Status = status,
        VisitDate = new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero),
        ApprovedAt = status == VisitStatus.Approved ? DateTimeOffset.UtcNow : null
    };

    private static ImprovementPlan Plan(
        int id,
        int visitId,
        int schoolId,
        string instructorId,
        string creatorId) => new()
    {
        Id = id,
        VisitId = visitId,
        SchoolId = schoolId,
        InstructorId = instructorId,
        DomainId = 10,
        Goal = "هدف",
        Actions = "إجراءات",
        StartDate = DateTimeOffset.UtcNow,
        EndDate = DateTimeOffset.UtcNow.AddMonths(1),
        SuccessIndicators = "مؤشرات",
        CreatedByUserId = creatorId
    };

    private static PlanFollowUp FollowUp(int id, int planId, string creatorId) => new()
    {
        Id = id,
        ImprovementPlanId = planId,
        FollowDate = DateTimeOffset.UtcNow,
        ProgressNote = "متابعة",
        ProgressScore = 50,
        CreatedByUserId = creatorId
    };

    private static ICurrentUserService User(string role, string userId, int? activeSchoolId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Role, role)
        };
        if (activeSchoolId.HasValue)
            claims.Add(new Claim("active_school_id", activeSchoolId.Value.ToString()));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "integration-test"));
        return new CurrentUserService(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        });
    }

    private static SchoolScopeGuard Guard(AlFalahDbContext context, ICurrentUserService user) =>
        new(context, user, NullLogger<SchoolScopeGuard>.Instance);

    private static VisitService CreateVisitService(AlFalahDbContext context, ICurrentUserService user) => new(
        context,
        userManager: null!,
        roleManager: null!,
        user,
        Guard(context, user),
        NullLogger<VisitService>.Instance,
        new HttpContextAccessor(),
        new ImageAssetLoader());

    private static ImprovementPlanService CreatePlanService(
        AlFalahDbContext context,
        ICurrentUserService user) => new(
            context,
            user,
            Guard(context, user),
            NullLogger<ImprovementPlanService>.Instance);

    private static TeacherService CreateTeacherService(
        AlFalahDbContext context,
        ICurrentUserService user)
    {
        var accessor = new HttpContextAccessor();
        return new TeacherService(
            context,
            userManager: null!,
            user,
            Guard(context, user),
            new AuditLogWriter(context, accessor, NullLogger<AuditLogWriter>.Instance),
            NullLogger<TeacherService>.Instance);
    }

    private static ComplaintService CreateComplaintService(
        AlFalahDbContext context,
        ICurrentUserService user) => new(
            context,
            user,
            Guard(context, user),
            visitService: null!,
            new HttpContextAccessor(),
            NullLogger<ComplaintService>.Instance);

    private static UpdatePlanRequestDto UpdatePlan() => new()
    {
        Goal = "هدف محدث",
        Actions = "إجراءات محدثة",
        StartDate = DateTimeOffset.UtcNow,
        EndDate = DateTimeOffset.UtcNow.AddMonths(1),
        SuccessIndicators = "مؤشرات محدثة",
        Status = "active"
    };

    private static CreateFollowUpRequestDto CreateFollowUp() => new()
    {
        FollowDate = DateTimeOffset.UtcNow,
        ProgressNote = "متابعة جديدة",
        ProgressScore = 60
    };

    private static UpdateFollowUpRequestDto UpdateFollowUp() => new()
    {
        FollowDate = DateTimeOffset.UtcNow,
        ProgressNote = "متابعة محدثة",
        ProgressScore = 70
    };

    private static async Task AssertForbiddenAsync(Func<Task> action)
    {
        await action.Should().ThrowAsync<Exception>()
            .Where(exception => exception.GetType() == typeof(UnauthorizedSchoolAccessException)
                || exception.GetType() == typeof(UnauthorizedAccessException));
    }

    private static async Task AssertForbiddenAsync<T>(Func<Task<T>> action)
    {
        await action.Should().ThrowAsync<Exception>()
            .Where(exception => exception.GetType() == typeof(UnauthorizedSchoolAccessException)
                || exception.GetType() == typeof(UnauthorizedAccessException));
    }
}
