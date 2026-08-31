using System.Reflection;
using System.Security.Claims;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data.Seeders;
using AlFalah.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AlFalah.Tests.Security;

public sealed class Phase2IdentityPermissionTests
{
    [Theory]
    [InlineData(RoleNames.Guardian)]
    [InlineData(RoleNames.StudentAffairsOfficer)]
    [InlineData(RoleNames.SocialWorker)]
    [InlineData(RoleNames.SecurityGuard)]
    public void New_roles_are_school_scoped(string role)
    {
        var currentUser = CurrentUser(role, activeSchoolId: 1);

        currentUser.IsSchoolScopedRole().Should().BeTrue();
        currentUser.IsGlobalAdmin().Should().BeFalse();
    }

    [Theory]
    [InlineData(RoleNames.SuperAdmin)]
    [InlineData(RoleNames.MainManager)]
    public void Global_roles_remain_global(string role)
    {
        var currentUser = CurrentUser(role, activeSchoolId: null);

        currentUser.IsGlobalAdmin().Should().BeTrue();
        currentUser.IsSchoolScopedRole().Should().BeFalse();
    }

    [Fact]
    public void Canonical_map_enforces_phase2_locked_defaults()
    {
        var map = GetRolePermissionMap();

        map.Keys.Should().Contain(new[]
        {
            RoleNames.Guardian,
            RoleNames.StudentAffairsOfficer,
            RoleNames.SocialWorker,
            RoleNames.SecurityGuard
        });

        map[RoleNames.SecurityGuard].Should().BeEquivalentTo(new[]
        {
            PermissionNames.GatePassView,
            PermissionNames.GatePassAcknowledgeSecurity,
            PermissionNames.GatePassExecute,
            PermissionNames.StudentAffairsDashboardSecurity
        });

        map[RoleNames.Guardian].Should().Contain(new[]
        {
            PermissionNames.GuardianViewLinkedStudents,
            PermissionNames.AttendanceSubmitExcuse,
            PermissionNames.GatePassViewOwn,
            PermissionNames.GatePassRequest,
            PermissionNames.StudentAffairsDashboardGuardian
        });
        map[RoleNames.Guardian].Should().NotContain(new[]
        {
            PermissionNames.StudentView,
            PermissionNames.GatePassView,
            PermissionNames.SummonView
        });

        map[RoleNames.Secretary].Should().Contain(new[]
        {
            PermissionNames.AttendanceViewStudents,
            PermissionNames.AttendanceManageStudents
        });
        map[RoleNames.Secretary].Should().NotContain(PermissionNames.AttendanceReviewExcuse);

        map[RoleNames.StudentAffairsOfficer].Should().Contain(new[]
        {
            PermissionNames.StudentAffairsSettingsView,
            PermissionNames.StudentAffairsSettingsManage,
            PermissionNames.NotificationApproveDispatch,
            PermissionNames.NotificationSuppressDispatch,
            PermissionNames.SummonReviewAutomationImpact
        });
        map[RoleNames.StudentAffairsOfficer].Should().NotContain(PermissionNames.GatePassExecute);

        map[RoleNames.SocialWorker].Should().Contain(new[]
        {
            PermissionNames.ReferralManage,
            PermissionNames.ReferralViewConfidential,
            PermissionNames.SummonSchedule,
            PermissionNames.SummonMarkAttended,
            PermissionNames.SummonStartObservation,
            PermissionNames.SummonMarkImproved
        });
        map[RoleNames.SocialWorker].Should().NotContain(PermissionNames.GatePassApprove);

        map[RoleNames.SchoolManager].Should().Contain(PermissionNames.GatePassOverride);
        map[RoleNames.SchoolManager].Should().NotContain(PermissionNames.ReferralViewConfidential);

        map[RoleNames.Moderator].Should().NotContain(new[]
        {
            PermissionNames.StudentView,
            PermissionNames.GatePassView,
            PermissionNames.StudentAffairsDashboardSocialWorker
        });
    }

    private static Dictionary<string, IEnumerable<string>> GetRolePermissionMap()
    {
        var method = typeof(DatabaseSeeder).GetMethod(
            "GetRolePermissionMap",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull();
        return method!.Invoke(null, null)
            .Should().BeAssignableTo<Dictionary<string, IEnumerable<string>>>().Subject;
    }

    private static CurrentUserService CurrentUser(string role, int? activeSchoolId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "phase2-user"),
            new(ClaimTypes.Role, role)
        };

        if (activeSchoolId.HasValue)
            claims.Add(new Claim("active_school_id", activeSchoolId.Value.ToString()));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        return new CurrentUserService(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        });
    }
}
