namespace AlFalah.Application.DTOs.Auth;

/// <summary>Changes the password of the currently authenticated user.</summary>
public class ChangePasswordRequestDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
