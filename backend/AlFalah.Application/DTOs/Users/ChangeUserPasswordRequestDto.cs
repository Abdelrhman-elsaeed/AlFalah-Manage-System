namespace AlFalah.Application.DTOs.Users;

/// <summary>Sets a new password for a user within the caller's allowed scope.</summary>
public class ChangeUserPasswordRequestDto
{
    public string NewPassword { get; set; } = string.Empty;
}
