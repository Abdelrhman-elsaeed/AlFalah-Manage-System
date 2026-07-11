namespace AlFalah.Domain.Entities;

/// <summary>
/// Stores user signature image/drawn data for use in official PDF reports.
/// </summary>
public class UserSignature
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? SignatureImageUrl { get; set; }
    public string? SignatureDrawnData { get; set; }  // Base64 drawn signature
    public string? DisplayName { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public ApplicationUser User { get; set; } = null!;
}
