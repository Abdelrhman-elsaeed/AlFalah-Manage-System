namespace AlFalah.Domain.Entities;

/// <summary>
/// JWT refresh token stored per user.
/// Enables token renewal without re-login.
/// </summary>
public class RefreshToken
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsRevoked { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; set; }
    public string? ReplacedByToken { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
    public bool IsActive => !IsRevoked && !IsExpired;

    // Navigation
    public ApplicationUser User { get; set; } = null!;
}
