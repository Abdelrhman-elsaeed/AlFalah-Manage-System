namespace AlFalah.Domain.Entities;

/// <summary>Verified Entra identity linked to an instructor profile.</summary>
public class TeacherMicrosoftAccount
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public string? TenantId { get; set; }
    public string? ObjectId { get; set; }
    public string MicrosoftEmail { get; set; } = string.Empty;
    public string NormalizedMicrosoftEmail { get; set; } = string.Empty;
    public bool IsLinked { get; set; }
    public DateTimeOffset LinkedAtUtc { get; set; }
    public DateTimeOffset? LastLoginAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public InstructorProfile Teacher { get; set; } = null!;
}
