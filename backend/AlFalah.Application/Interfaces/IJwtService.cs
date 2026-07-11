namespace AlFalah.Application.Interfaces;

/// <summary>
/// JWT token generation and validation service.
/// </summary>
public interface IJwtService
{
    /// <summary>Generate an access token with the specified claims.</summary>
    string GenerateAccessToken(string userId, string username, IEnumerable<string> roles,
        IEnumerable<string> permissions, int? activeSchoolId, string preferredLanguage);

    /// <summary>Generate a secure random refresh token.</summary>
    string GenerateRefreshToken();

    /// <summary>Extract user ID from an expired/valid token for refresh flow.</summary>
    string? GetUserIdFromToken(string token);
}
