namespace AlFalah.Application.DTOs.Auth;

/// <summary>
/// Request for Main Manager global login (no school selection).
/// </summary>
public class MainManagerLoginRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
