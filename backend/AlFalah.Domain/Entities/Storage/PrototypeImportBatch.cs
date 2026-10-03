using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities.Storage;

public sealed class PrototypeImportBatch : IStorageRecord
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int AcademicYearId { get; set; }
    public string SourceSHA256 { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public PrototypeImportStatus Status { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}
