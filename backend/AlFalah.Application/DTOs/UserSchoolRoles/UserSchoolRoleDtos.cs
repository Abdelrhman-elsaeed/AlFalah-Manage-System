namespace AlFalah.Application.DTOs.UserSchoolRoles;

/// <summary>
/// Request to assign a user to a school with a role.
/// </summary>
public class UserSchoolRoleCreateRequestDto
{
    public string UserId { get; set; } = string.Empty;
    public int SchoolId { get; set; }
    /// <summary>One of: SchoolManager, Moderator, Instructor.</summary>
    public string Role { get; set; } = string.Empty;
}

public class UserSchoolRoleDetailDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}