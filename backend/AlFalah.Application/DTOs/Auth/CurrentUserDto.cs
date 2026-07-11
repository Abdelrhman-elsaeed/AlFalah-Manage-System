namespace AlFalah.Application.DTOs.Auth;

/// <summary>
/// Current user details returned from GET /api/v1/auth/me.
/// </summary>
public class CurrentUserDto
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string PreferredLanguage { get; set; } = "ar";
    public int? ActiveSchoolId { get; set; }
    public string? ActiveSchoolName { get; set; }
    public List<string> Roles { get; set; } = new();
    public List<string> Permissions { get; set; } = new();
}
