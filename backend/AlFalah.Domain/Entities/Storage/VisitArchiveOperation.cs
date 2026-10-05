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
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public DateTimeOffset? LastAttemptAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public DateTimeOffset ApprovedAtUtc { get; set; }
    public string ApprovalSource { get; set; } = "";
    public string? SnapshotJson { get; set; }
    public string? SnapshotSHA256 { get; set; }
    public byte[]? PdfBytes { get; set; }
    public string? PdfSHA256 { get; set; }
    public string? ProviderItemId { get; set; }
    public string? UploadIdentity { get; set; }
    public string? DriveId { get; set; }
    public string? ArchiveFolderItemId { get; set; }
    public string? SchoolRootItemId { get; set; }
    public int? ArchiveFolderId { get; set; }
    public bool UploadStarted { get; set; }
    public int RecoveryGeneration { get; set; }
    public string? LastErrorCode { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}
