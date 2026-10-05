using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities.Storage;

public sealed class PrototypeImportBatch : IStorageRecord
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int AcademicYearId { get; set; }
    public string SourceSHA256 { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string SourceVersion { get; set; } = "1";
    public int TemplateVersion { get; set; } = 1;
    public string? ReviewedDigest { get; set; }
    public string? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public string? ReviewReason { get; set; }
    public string? CommittedByUserId { get; set; }
    public DateTimeOffset? CommittedAtUtc { get; set; }
    public PrototypeImportStatus Status { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}
