namespace AlFalah.Domain.Entities.Storage;

// Reserved before any provider mutation. ProviderItemId is pre-generated for creates.
public sealed class StorageOperation : IStorageRecord
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public string ActorUserId { get; set; } = "";
    public string RequestKey { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public string Action { get; set; } = "Upload";
    public string Status { get; set; } = "Pending";
    public int? FolderId { get; set; }
    public int? StoredFileId { get; set; }
    public int? VersionId { get; set; }
    public int? OwnerTeacherId { get; set; }
    public string DriveId { get; set; } = "";
    public string ProviderItemId { get; set; } = "";
    public string ParentItemId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string MimeType { get; set; } = "";
    public long Size { get; set; }
    public string? SHA256 { get; set; }
    public int? LegacyTaskId { get; set; }
    public long? LegacyOperationId { get; set; }
    public long? LegacySubmissionId { get; set; }
    public string? ErrorCode { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}
