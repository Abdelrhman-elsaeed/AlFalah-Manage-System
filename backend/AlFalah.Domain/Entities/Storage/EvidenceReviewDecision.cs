using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities.Storage;

public sealed class EvidenceReviewDecision : IStorageRecord
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int EvidenceLinkId { get; set; }
    public int StoredFileId { get; set; }
    public int VersionId { get; set; }
    public EvidenceReviewStatus Decision { get; set; }
    public string? ReviewedByUserId { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public bool IsLegacyImported { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = [];
    public EvidenceLink EvidenceLink { get; set; } = null!;
    public StoredFileVersion Version { get; set; } = null!;
}
