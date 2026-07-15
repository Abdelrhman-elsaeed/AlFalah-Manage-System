using System.Security.Claims;
using AlFalah.Application.DTOs.Dashboards;
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

public sealed class DashboardScopeTests
{
    [Fact]
    public async Task Phase5_Instructor_Dashboard_Coerces_CrossSchool_Filter_To_ActiveSchool()
    {
        await using var context = CreateContext();
        context.AddRange(
            User("INSTRUCTOR-1", "Test", "Instructor"),
            User("MODERATOR-1", "Test", "Moderator"),
            new School { Id = 1, Name = "Active School", City = "Riyadh" },
            new School { Id = 2, Name = "Other School", City = "Riyadh" },
            Visit(10, schoolId: 1),
            Visit(20, schoolId: 2));
        await context.SaveChangesAsync();

        var currentUser = Instructor(activeSchoolId: 1);
        var service = new DashboardService(
            context,
            currentUser,
            new SchoolScopeGuard(context, currentUser, NullLogger<SchoolScopeGuard>.Instance),
            NullLogger<DashboardService>.Instance);

        var result = await service.GetInstructorDashboardAsync(
            new DashboardFilterDto { SchoolId = 2 });

        result.SchoolId.Should().Be(1);
        result.SchoolName.Should().Be("Active School");
        result.ApprovedVisitsCount.Should().Be(1);
        result.PerformanceTrend.Select(point => point.VisitId).Should().Equal(10);
        result.AppliedFilters.SchoolId.Should().Be(1);
    }

    [Theory]
    [InlineData(typeof(MainManagerDashboardDto))]
    [InlineData(typeof(ModeratorDashboardDto))]
    public void Phase5_Restricted_Dashboard_Contracts_Contain_No_Complaint_Content(Type dashboardType)
    {
        dashboardType.GetProperties()
            .Select(property => property.Name)
            .Should().NotContain(name => name.Contains("Complaint", StringComparison.OrdinalIgnoreCase));
    }

    private static AlFalahDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase($"dashboard-scope-{Guid.NewGuid()}")
            .Options;
        return new AlFalahDbContext(options);
    }

    private static ApplicationUser User(string id, string firstName, string lastName) => new()
    {
        Id = id,
        UserName = id,
        NormalizedUserName = id,
        FirstName = firstName,
        LastName = lastName
    };

    private static Visit Visit(int id, int schoolId) => new()
    {
        Id = id,
        SchoolId = schoolId,
        InstructorId = "INSTRUCTOR-1",
        CreatedByUserId = "MODERATOR-1",
        RubricVersionId = 1,
        VisitCategory = VisitCategory.ClassroomOrPeriodic,
        VisitSequence = VisitSequence.First,
        Status = VisitStatus.Approved,
        VisitDate = new DateTimeOffset(2026, id == 10 ? 1 : 2, 15, 0, 0, 0, TimeSpan.Zero),
        Analysis = new VisitAnalysis
        {
            Id = id,
            VisitId = id,
            OverallScore = id == 10 ? 3.25m : 1.25m,
            PerformanceLevelAr = "Test",
            ComputedAt = new DateTimeOffset(2026, id == 10 ? 1 : 2, 15, 0, 0, 0, TimeSpan.Zero)
        }
    };

    private static ICurrentUserService Instructor(int activeSchoolId)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "INSTRUCTOR-1"),
            new Claim(ClaimTypes.Role, RoleNames.Instructor),
            new Claim("active_school_id", activeSchoolId.ToString()),
            new Claim("permission", PermissionNames.DashboardInstructor)
        }, "integration-test"));
        return new CurrentUserService(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        });
    }
}
