using System.Security.Claims;
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

public sealed class TeacherLongitudinalProgressTests
{
    [Fact]
    public async Task Phase4_Moderator_Progress_Uses_Approved_Own_Visits_Active_Axes_And_LatestMinusEarliest()
    {
        await using var context = await CreateContextAsync();
        var currentUser = Moderator();
        var service = CreateService(context, currentUser);

        var result = await service.GetProgressAsync("TEACHER-1");

        result.AxisLabels.Select(x => x.DomainCode).Should().Equal("D1", "D2", "D3");
        result.Visits.Select(x => x.VisitId).Should().Equal(new[] { 10, 20 },
            because: "pending visits and another moderator's approved visits are outside the official scoped trend");
        result.Visits[0].DomainAverages.Single(x => x.DomainCode == "D3")
            .AverageScore.Should().BeNull("the historical snapshot did not contain the newly-active domain");

        result.FirstToLastComparison.Should().NotBeNull();
        var comparison = result.FirstToLastComparison!;
        comparison.FirstVisitId.Should().Be(10);
        comparison.LastVisitId.Should().Be(20);
        comparison.FirstVisitDate.Should().BeBefore(comparison.LastVisitDate);
        comparison.DomainDeltas.Single(x => x.DomainCode == "D1").Delta.Should().Be(1.000m);
        comparison.DomainDeltas.Single(x => x.DomainCode == "D2").Delta.Should().Be(-0.500m);
        comparison.DomainDeltas.Single(x => x.DomainCode == "D3").Delta.Should().BeNull();
    }

    private static async Task<AlFalahDbContext> CreateContextAsync()
    {
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase($"teacher-longitudinal-{Guid.NewGuid()}")
            .Options;
        var context = new AlFalahDbContext(options);

        var instructorRole = new ApplicationRole
        {
            Id = "ROLE-INSTRUCTOR",
            Name = RoleNames.Instructor,
            NormalizedName = RoleNames.Instructor.ToUpperInvariant()
        };
        var rubric = new RubricVersion { Id = 1, VersionNumber = 1, IsActive = true };
        context.AddRange(
            instructorRole,
            User("TEACHER-1", "معلم الاختبار"),
            User("MODERATOR-1", "المشرف الأول"),
            User("MODERATOR-2", "المشرف الثاني"),
            new School { Id = 1, Name = "مدرسة الاختبار", City = "الرياض" },
            new UserSchoolRole
            {
                Id = 1,
                UserId = "TEACHER-1",
                SchoolId = 1,
                RoleId = instructorRole.Id,
                IsActive = true
            },
            rubric,
            new RubricDomain { Id = 1, RubricVersionId = 1, Code = "D1", NameAr = "النطاق الأول", SortOrder = 1 },
            new RubricDomain { Id = 2, RubricVersionId = 1, Code = "D2", NameAr = "النطاق الثاني", SortOrder = 2 },
            new RubricDomain { Id = 3, RubricVersionId = 1, Code = "D3", NameAr = "نطاق نشط جديد", SortOrder = 3 });

        AddVisit(context, 10, "MODERATOR-1", VisitStatus.Approved, new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero), 2.000m, 3.000m);
        AddVisit(context, 20, "MODERATOR-1", VisitStatus.Approved, new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero), 3.000m, 2.500m);
        AddVisit(context, 30, "MODERATOR-2", VisitStatus.Approved, new DateTimeOffset(2026, 4, 10, 0, 0, 0, TimeSpan.Zero), 4.000m, 4.000m);
        AddVisit(context, 40, "MODERATOR-1", VisitStatus.PendingApproval, new DateTimeOffset(2026, 5, 10, 0, 0, 0, TimeSpan.Zero), 0.000m, 0.000m);

        await context.SaveChangesAsync();
        return context;
    }

    private static void AddVisit(
        AlFalahDbContext context,
        int id,
        string creatorId,
        VisitStatus status,
        DateTimeOffset visitDate,
        decimal d1,
        decimal d2)
    {
        var analysis = new VisitAnalysis
        {
            Id = id,
            VisitId = id,
            OverallScore = (d1 + d2) / 2,
            PerformanceLevelAr = "جيد",
            ComputedAt = visitDate,
            DomainAverages = new List<VisitDomainAverage>
            {
                new() { Id = id * 10 + 1, VisitAnalysisId = id, RubricDomainId = 1, DomainCode = "D1", DomainNameAr = "نطاق تاريخي أول", AverageScore = d1 },
                new() { Id = id * 10 + 2, VisitAnalysisId = id, RubricDomainId = 2, DomainCode = "D2", DomainNameAr = "نطاق تاريخي ثان", AverageScore = d2 }
            }
        };
        context.Visits.Add(new Visit
        {
            Id = id,
            SchoolId = 1,
            InstructorId = "TEACHER-1",
            CreatedByUserId = creatorId,
            RubricVersionId = 1,
            VisitCategory = VisitCategory.ClassroomOrPeriodic,
            VisitSequence = VisitSequence.First,
            Status = status,
            VisitDate = visitDate,
            Analysis = analysis
        });
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
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "MODERATOR-1"),
            new Claim(ClaimTypes.Role, RoleNames.Moderator),
            new Claim("active_school_id", "1")
        }, "integration-test"));
        return new CurrentUserService(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        });
    }

    private static TeacherService CreateService(AlFalahDbContext context, ICurrentUserService currentUser)
    {
        var accessor = new HttpContextAccessor();
        return new TeacherService(
            context,
            userManager: null!,
            currentUser,
            new SchoolScopeGuard(context, currentUser, NullLogger<SchoolScopeGuard>.Instance),
            new AuditLogWriter(context, accessor, NullLogger<AuditLogWriter>.Instance),
            NullLogger<TeacherService>.Instance);
    }
}
