using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;

namespace AlFalah.Application.DTOs.Users;

/// <summary>
/// Filters for the users list endpoint.
/// </summary>
public class UserListQuery : PagedQuery
{
    public string? Search { get; set; }
    /// <summary>Filter by ApplicationRole name (e.g. SchoolManager, Moderator, Instructor).</summary>
    public string? Role { get; set; }
    /// <summary>Limit to users with an active UserSchoolRole in this school.</summary>
    public int? SchoolId { get; set; }
    public bool? IsActive { get; set; }
}

/// <summary>
/// Users list row.
/// </summary>
public class UserListItemDto
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public List<string> Roles { get; set; } = new();
    public List<UserSchoolBriefDto> Schools { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}

public class UserSchoolBriefDto
{
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

/// <summary>
/// Detailed user view returned by GET /{id}.
///
/// D-74 — extended to surface the teacher-profile fields the front-end
/// uses to pre-fill the edit form:
/// EmployeeNumber, Subject, Stage, and Classes[] (only meaningful when the
/// user has the Instructor role; null/empty for Moderator / SchoolManager).
/// </summary>
public class UserDetailDto
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string PreferredLanguage { get; set; } = "ar";
    public bool IsActive { get; set; }
    public List<string> Roles { get; set; } = new();
    public List<UserSchoolBriefDto> Schools { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }

    // ─── D-74 — Teacher profile fields (populated for Instructors only) ────
    public string? EmployeeNumber { get; set; }
    public string? Subject { get; set; }
    public SchoolStage? Stage { get; set; }
    public List<string> Classes { get; set; } = new();
}

/// <summary>
/// Create-user request. Only school staff roles (including Secretary) are supported here.
/// MainManager and SuperAdmin are out of scope and must be created by a Super Admin via a separate
/// flow (not part of Phase 2).
///
/// D-74 — Teacher-profile fields (EmployeeNumber, Subject, Stage, Classes)
/// are RELEVANT when <see cref="Role"/> == Instructor. The service ignores
/// them for other roles (so a Moderator create payload cannot accidentally
/// populate teacher columns).
/// </summary>
public class UserCreateRequestDto
{
    public string Username { get; set; } = string.Empty;
    /// <summary>Required for non-instructors. Instructor accounts use EmployeeNumber as the initial password.</summary>
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    /// <summary>Required for Instructor creates; mapped into the existing FirstName/LastName storage.</summary>
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string PreferredLanguage { get; set; } = "ar";
    /// <summary>One of: SchoolManager, Secretary, Moderator, Instructor.</summary>
    public string Role { get; set; } = string.Empty;
    /// <summary>Optional initial school assignment.</summary>
    public int? SchoolId { get; set; }

    // D-74 — Teacher-profile fields (relevant for Instructors; ignored otherwise).
    public string? EmployeeNumber { get; set; }
    public string? Subject { get; set; }
    public SchoolStage? Stage { get; set; }
    /// <summary>Class labels taught by the teacher (e.g. ["3/1", "3/2"]).</summary>
    public List<string>? Classes { get; set; }
}

/// <summary>
/// D-74 — Update-user request. Teacher-profile fields apply when the user
/// has the Instructor role; the service ignores them for other roles.
/// </summary>
public class UserUpdateRequestDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    /// <summary>Used by the teacher edit form and split into FirstName/LastName on save.</summary>
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string PreferredLanguage { get; set; } = "ar";
    /// <summary>Teacher school; only applied for Instructor users and always school-scoped.</summary>
    public int? SchoolId { get; set; }

    // D-74 — Teacher-profile fields (relevant for Instructors; ignored otherwise).
    public string? EmployeeNumber { get; set; }
    public string? Subject { get; set; }
    public SchoolStage? Stage { get; set; }
    public List<string>? Classes { get; set; }
}
