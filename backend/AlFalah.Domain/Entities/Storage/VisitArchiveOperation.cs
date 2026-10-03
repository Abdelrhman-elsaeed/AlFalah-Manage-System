using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities.Storage;

public sealed class VisitArchiveOperation : IStorageRecord
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int VisitId { get; set; }
    public int ApprovalRevision { get; set; }
    public VisitArchiveStatus Status { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAtUtc { get; set; }
    public DateTimeOffset? LockedAtUtc { get; set; }
    public string? LastErrorCode { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}
