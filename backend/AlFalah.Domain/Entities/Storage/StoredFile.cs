using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities.Storage;

public sealed class StoredFile : IStorageRecord
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int FolderId { get; set; }
    public int? OwnerTeacherId { get; set; }
    public StoredFileSourceKind SourceKind { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int? CurrentVersionId { get; set; }
    public bool NeedsLink { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public string? DeletedByUserId { get; set; }
    public long? LegacySubmissionId { get; set; }
    // Immutable source snapshot, including decisions on legacy unlinked files.
    public string? LegacyProvenanceJson { get; set; }
    public string? LegacyFingerprint { get; set; }
    public string? SharedWriterProvenanceJson { get; set; }
    public string? SharedWriterFingerprint { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = [];
    public StorageFolder Folder { get; set; } = null!;
}
