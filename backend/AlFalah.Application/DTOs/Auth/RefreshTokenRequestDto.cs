namespace AlFalah.Application.DTOs.Auth;

/// <summary>
/// Request to refresh access token using a valid refresh token.
/// </summary>
public class RefreshTokenRequestDto
{
    public string RefreshToken { get; set; } = string.Empty;
}
