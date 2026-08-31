using AlFalah.Domain.Enums;

namespace AlFalah.Application.Interfaces;

/// <summary>
/// Provides current authenticated user context extracted from the HTTP request/JWT.
/// </summary>
public interface ICurrentUserService
{
    string? UserId { get; }
    string? Username { get; }
    int? ActiveSchoolId { get; }
    string? PreferredLanguage { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string roleName);
    bool HasPermission(string permissionName);
    IEnumerable<string> GetRoles();
    IEnumerable<string> GetPermissions();

    /// <summary>
    /// True when the caller is a Super Admin or Main Manager — these roles have
    /// global scope across all schools.
    /// </summary>
    bool IsGlobalAdmin();

    /// <summary>
    /// True when the caller's role is school-scoped. Their data is always forced
    /// through their token's ActiveSchoolId; any client-supplied schoolId that
    /// differs is rejected.
    /// </summary>
    bool IsSchoolScopedRole();
}
