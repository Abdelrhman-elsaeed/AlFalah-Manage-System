using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities.Storage;

public sealed class PrototypeImportRow : IStorageRecord
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int BatchId { get; set; }
    public int SourceOrdinal { get; set; }
    public string SourceRowSHA256 { get; set; } = string.Empty;
    public string SourceRowKey { get; set; } = string.Empty;
    public string SourceJson { get; set; } = "{}";
    public string Classification { get; set; } = "Missing";
    public int? RequirementId { get; set; }
    public string? ResponsibleUserId { get; set; }
    public string? ResolutionReason { get; set; }
    public int? StoredFileId { get; set; }
    public int? UploadOperationId { get; set; }
    public string? BytesSHA256 { get; set; }
    public string? ReferencePath { get; set; }
    public PrototypeImportStatus Status { get; set; }
    public string? ExceptionNote { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}
