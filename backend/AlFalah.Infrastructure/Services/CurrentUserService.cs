using System.Security.Claims;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// Extracts current authenticated user context from JWT claims in the HTTP request.
/// Injected as scoped service.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public string? UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User?.FindFirstValue("sub");

    public string? Username => User?.FindFirstValue(ClaimTypes.Name)
                            ?? User?.FindFirstValue("unique_name");

    public int? ActiveSchoolId
    {
        get
        {
            var value = User?.FindFirstValue("active_school_id");
            return int.TryParse(value, out var id) ? id : null;
        }
    }

    public string? PreferredLanguage =>
        User?.FindFirstValue("preferred_language") ?? "ar";

    public bool IsAuthenticated =>
        User?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string roleName) =>
        User?.IsInRole(roleName) ?? false;

    public bool HasPermission(string permissionName) =>
        User?.Claims.Any(c => c.Type == "permission" && c.Value == permissionName) ?? false;

    public IEnumerable<string> GetRoles() =>
        User?.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value) ?? Enumerable.Empty<string>();

    public IEnumerable<string> GetPermissions() =>
        User?.Claims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value) ?? Enumerable.Empty<string>();

    public bool IsGlobalAdmin()
    {
        var roles = GetRoles();
        return roles.Contains(RoleNames.SuperAdmin) || roles.Contains(RoleNames.MainManager);
    }

    public bool IsSchoolScopedRole()
    {
        var roles = GetRoles();
        return roles.Any(r =>
            r == RoleNames.SchoolManager
            || r == RoleNames.Moderator
            || r == RoleNames.Instructor);
    }
}
