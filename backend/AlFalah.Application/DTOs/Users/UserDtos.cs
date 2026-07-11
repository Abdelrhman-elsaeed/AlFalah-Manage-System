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
}

/// <summary>
/// Create-user request. Only Instructor/Moderator/SchoolManager per Phase 2 scope.
/// MainManager and SuperAdmin are out of scope and must be created by a Super Admin via a separate
/// flow (not part of Phase 2).
/// </summary>
public class UserCreateRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string PreferredLanguage { get; set; } = "ar";
    /// <summary>One of: SchoolManager, Moderator, Instructor.</summary>
    public string Role { get; set; } = string.Empty;
    /// <summary>Optional initial school assignment.</summary>
    public int? SchoolId { get; set; }
}

public class UserUpdateRequestDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string PreferredLanguage { get; set; } = "ar";
}