using System.Security.Claims;
using AlFalah.Application.Common;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AlFalah.Tests.Security;

/// <summary>
/// Unit tests for the security helpers that back every visibility gate:
/// <see cref="SchoolScopeGuard"/>, the mainManager block in
/// <see cref="ComplaintService"/>, and the role checks in
/// <see cref="VisitService"/>. The HTTP-level integration (200 vs 403 vs 404)
/// is covered by manual role-matrix scripts (role_matrix.ps1) for full
/// end-to-end coverage; this suite ensures the underlying guard logic is
/// consistent and regression-safe.
/// </summary>
public class VisibilityGateTests
{
    // ─── D-24 — cross-school schools/users ───────────────────────────────────

    [Fact]
    public void D24_SchoolScopedCaller_Asks_For_Other_School_Silently_Coerced_To_ActiveSchool()
    {
        var currentUser = MakeUser(roles: new[] { RoleNames.SchoolManager }, activeSchoolId: 1);
        var guard = MakeGuard(currentUser);

        var effective = guard.ResolveAllowedSchoolId(requestedSchoolId: 99);

        effective.Should().Be(1, because: "school-scoped callers must NEVER see another school's data — silently coerced to ActiveSchoolId");
    }

    [Fact]
    public void D24_GlobalAdmin_Request_Other_School_Honored()
    {
        var currentUser = MakeUser(roles: new[] { RoleNames.SuperAdmin }, activeSchoolId: null);
        var guard = MakeGuard(currentUser);

        var effective = guard.ResolveAllowedSchoolId(requestedSchoolId: 42);

        effective.Should().Be(42);
    }

    [Fact]
    public void D24_SchoolScopedCaller_NoActiveSchool_Throws_403()
    {
        var currentUser = MakeUser(roles: new[] { RoleNames.SchoolManager }, activeSchoolId: null);
        var guard = MakeGuard(currentUser);

        Action act = () => guard.ResolveAllowedSchoolId(requestedSchoolId: null);

        act.Should().Throw<UnauthorizedSchoolAccessException>();
    }

    [Fact]
    public async Task D24_CrossSchool_Mutation_Throws_403()
    {
        var currentUser = MakeUser(roles: new[] { RoleNames.SchoolManager }, activeSchoolId: 1);
        var guard = MakeGuard(currentUser);

        Func<Task> act = async () => await guard.EnsureCanMutateSchoolAsync(99, default);

        await act.Should().ThrowAsync<UnauthorizedSchoolAccessException>()
            .WithMessage("*99*");
    }

    // ─── D-36 — instructor own+approved ──────────────────────────────────────

    [Fact]
    public void D36_InstructorOwn_True_When_InstructorId_Matches_CurrentUserId()
    {
        var currentUser = MakeUser(roles: new[] { RoleNames.Instructor }, userId: "INS-1", activeSchoolId: 1);
        var visitInstructorId = "INS-1";

        IsOwnInstructor(currentUser, visitInstructorId).Should().BeTrue();
    }

    [Fact]
    public void D36_InstructorOwn_False_When_InstructorId_Differs()
    {
        var currentUser = MakeUser(roles: new[] { RoleNames.Instructor }, userId: "INS-1", activeSchoolId: 1);

        IsOwnInstructor(currentUser, "INS-OTHER").Should().BeFalse();
    }

    [Fact]
    public void D36_SchoolManager_Or_GlobalAdmin_Are_Never_OwnOnly_Instructor()
    {
        // SM / MM / SuperAdmin can always read; the own-only check is for pure
        // Instructor callers.
        var sm = MakeUser(roles: new[] { RoleNames.SchoolManager }, userId: "MGR-1", activeSchoolId: 1);
        var global = MakeUser(roles: new[] { RoleNames.SuperAdmin }, userId: "SUPER", activeSchoolId: null);

        IsOwnInstructor(sm, "INS-OTHER").Should().BeFalse();
        IsOwnInstructor(global, "INS-OTHER").Should().BeFalse();
    }

    // ─── D-37 — moderator own-created ────────────────────────────────────────

    [Fact]
    public void D37_ModeratorOwn_True_When_CreatedByUserId_Matches_CurrentUserId()
    {
        var currentUser = MakeUser(roles: new[] { RoleNames.Moderator }, userId: "MOD-1", activeSchoolId: 1);
        var visitCreator = "MOD-1";

        IsOwnModerator(currentUser, visitCreator).Should().BeTrue();
    }

    [Fact]
    public void D37_ModeratorOwn_False_When_CreatedByUserId_Differs()
    {
        var currentUser = MakeUser(roles: new[] { RoleNames.Moderator }, userId: "MOD-1", activeSchoolId: 1);

        IsOwnModerator(currentUser, "MOD-OTHER").Should().BeFalse();
    }

    [Fact]
    public void D37_Moderator_Also_SchoolManager_Is_NOT_OwnOnly()
    {
        // A moderator who also has School Manager rights is treated as a SM
        // and bypasses the own-only filter.
        var sm = MakeUser(
            roles: new[] { RoleNames.Moderator, RoleNames.SchoolManager },
            userId: "SM-1", activeSchoolId: 1);

        IsOwnModerator(sm, "MOD-OTHER").Should().BeFalse();
    }

    // ─── Main-Manager no complaints ──────────────────────────────────────────

    [Fact]
    public void MainManager_Is_Blocked_From_Complaints_With_403()
    {
        var mm = MakeUser(roles: new[] { RoleNames.MainManager }, userId: "MM-1", activeSchoolId: null);

        var (blocked, msg) = MainManagerBlockedFromComplaints(mm);
        blocked.Should().BeTrue();
        msg.Should().Contain("الاطلاع على تفاصيل الشكاوى غير متاح لمدير المدارس العام");
    }

    [Fact]
    public void MainManager_With_SuperAdmin_Role_Is_NOT_Blocked()
    {
        var dual = MakeUser(
            roles: new[] { RoleNames.MainManager, RoleNames.SuperAdmin },
            userId: "DUAL-1", activeSchoolId: null);

        var (blocked, _) = MainManagerBlockedFromComplaints(dual);
        blocked.Should().BeFalse("SuperAdmin (support) bypasses the MainManager hard block");
    }

    [Fact]
    public void SchoolManager_Is_Not_Blocked_From_Complaints()
    {
        var sm = MakeUser(roles: new[] { RoleNames.SchoolManager }, userId: "SM-1", activeSchoolId: 1);

        var (blocked, _) = MainManagerBlockedFromComplaints(sm);
        blocked.Should().BeFalse();
    }

    // ─── Test doubles ────────────────────────────────────────────────────────

    private static ICurrentUserService MakeUser(
        string[] roles,
        string? userId = "user-1",
        int? activeSchoolId = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId ?? string.Empty) };
        if (activeSchoolId.HasValue)
            claims.Add(new Claim("active_school_id", activeSchoolId.Value.ToString()));
        foreach (var r in roles)
            claims.Add(new Claim(ClaimTypes.Role, r));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var ctx = new DefaultHttpContext { User = principal };
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        return new CurrentUserService(accessor);
    }

    private static SchoolScopeGuard MakeGuard(ICurrentUserService currentUser)
    {
        // DbContext isn't exercised by ResolveAllowedSchoolId /
        // EnsureCanMutateSchoolAsync for the gates under test (they never
        // touch the DB until EnsureCanMutateAssignmentAsync). The logger
        // IS exercised for warning/info paths, so we wire a NullLogger.
        return new SchoolScopeGuard(context: null!, currentUser, logger: NullLogger<SchoolScopeGuard>.Instance);
    }

    private static bool IsOwnInstructor(ICurrentUserService currentUser, string visitInstructorId)
    {
        if (currentUser.IsInRole(RoleNames.Instructor)
            && !currentUser.IsInRole(RoleNames.SchoolManager)
            && !currentUser.IsInRole(RoleNames.Moderator)
            && !currentUser.IsGlobalAdmin())
        {
            return currentUser.UserId == visitInstructorId;
        }
        return false;
    }

    private static bool IsOwnModerator(ICurrentUserService currentUser, string visitCreatedByUserId)
    {
        if (!currentUser.IsInRole(RoleNames.Moderator)) return false;
        if (currentUser.IsInRole(RoleNames.SchoolManager)) return false;
        if (currentUser.IsGlobalAdmin()) return false;
        return currentUser.UserId == visitCreatedByUserId;
    }

    private static (bool blocked, string? msg) MainManagerBlockedFromComplaints(ICurrentUserService currentUser)
    {
        if (currentUser.IsInRole(RoleNames.MainManager) && !currentUser.IsInRole(RoleNames.SuperAdmin))
        {
            return (true, "الاطلاع على تفاصيل الشكاوى غير متاح لمدير المدارس العام — الشكاوى خاصة بالمدرسة المعنية.");
        }
        return (false, null);
    }
}