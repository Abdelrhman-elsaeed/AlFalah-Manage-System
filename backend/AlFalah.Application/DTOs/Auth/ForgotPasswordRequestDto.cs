namespace AlFalah.Application.DTOs.Auth;

/// <summary>
/// Request to initiate the password reset flow.
/// Always returns success regardless of whether the email/user exists
/// (prevents user enumeration).
/// </summary>
public class ForgotPasswordRequestDto
{
    public string Username { get; set; } = string.Empty;
}