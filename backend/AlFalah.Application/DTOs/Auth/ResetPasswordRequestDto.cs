namespace AlFalah.Application.DTOs.Auth;

/// <summary>
/// Request to complete the password reset using the token delivered
/// via the forgot-password flow.
/// </summary>
public class ResetPasswordRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}