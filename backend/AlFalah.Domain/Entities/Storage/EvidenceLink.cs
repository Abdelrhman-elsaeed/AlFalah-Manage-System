using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities.Storage;

public sealed class EvidenceLink : IStorageRecord
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int AcademicYearId { get; set; }
    public int StoredFileId { get; set; }
    public int RequirementId { get; set; }
    public int? TeacherId { get; set; }
    public int VersionId { get; set; }
    public EvidenceLinkStatus Status { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? SubmittedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = [];
    public StoredFile StoredFile { get; set; } = null!;
    public StoredFileVersion Version { get; set; } = null!;
    public EvidenceRequirement Requirement { get; set; } = null!;
}
