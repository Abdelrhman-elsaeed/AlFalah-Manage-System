namespace AlFalah.Domain.Entities;

/// <summary>
/// Audit log entry for all important system events.
/// SchoolId is nullable for global/platform-level actions.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    public int? SchoolId { get; set; }
    public string? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityName { get; set; }
    public string? EntityId { get; set; }

    /// <summary>JSON snapshot of old entity state.</summary>
    public string? OldValues { get; set; }

    /// <summary>JSON snapshot of new entity state.</summary>
    public string? NewValues { get; set; }

    public string? Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    // Navigation (optional, not enforced by FK to keep log independent)
    public School? School { get; set; }
}
