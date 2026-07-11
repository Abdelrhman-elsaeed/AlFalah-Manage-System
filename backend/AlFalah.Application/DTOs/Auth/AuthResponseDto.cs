namespace AlFalah.Application.DTOs.Auth;

/// <summary>
/// Auth response returned after successful login or token refresh.
/// Contains access token, refresh token, and user context.
/// </summary>
public class AuthResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTimeOffset AccessTokenExpiry { get; set; }
    public DateTimeOffset RefreshTokenExpiry { get; set; }
    public UserTokenInfoDto User { get; set; } = null!;
}

/// <summary>
/// User context embedded in auth response and token claims.
/// </summary>
public class UserTokenInfoDto
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int? ActiveSchoolId { get; set; }
    public string? ActiveSchoolName { get; set; }
    public string PreferredLanguage { get; set; } = "ar";
    public List<string> Roles { get; set; } = new();
    public List<string> Permissions { get; set; } = new();
}
