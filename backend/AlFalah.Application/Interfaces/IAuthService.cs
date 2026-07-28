using AlFalah.Application.DTOs.Auth;

namespace AlFalah.Application.Interfaces;

/// <summary>
/// Authentication service for login, token refresh, and logout.
/// </summary>
public interface IAuthService
{
    /// <summary>Login for school-scoped users (selects school first).</summary>
    Task<AuthResponseDto> SchoolLoginAsync(SchoolLoginRequestDto request, string? ipAddress, string? userAgent);

    /// <summary>Login for Main Manager (global scope, no school selection).</summary>
    Task<AuthResponseDto> MainManagerLoginAsync(MainManagerLoginRequestDto request, string? ipAddress, string? userAgent);

    /// <summary>Refresh JWT using a valid refresh token.</summary>
    Task<AuthResponseDto> RefreshTokenAsync(string refreshToken, string? ipAddress, string? userAgent);

    /// <summary>Revoke a refresh token (logout).</summary>
    Task LogoutAsync(string refreshToken);

    /// <summary>Get current user info from token claims.</summary>
    Task<CurrentUserDto> GetCurrentUserAsync(string userId);

    /// <summary>
    /// Initiate the password reset flow. Returns the reset token in development
    /// environments so the client can complete the flow without an email gateway.
    /// In production the token is sent out-of-band and this method returns null.
    /// </summary>
    Task<string?> ForgotPasswordAsync(string username);

    /// <summary>
    /// Complete the password reset using the token issued by ForgotPasswordAsync.
    /// </summary>
    Task ResetPasswordAsync(string username, string token, string newPassword);

    /// <summary>Changes the currently authenticated user's password.</summary>
    Task ChangePasswordAsync(string userId, string currentPassword, string newPassword);
}
