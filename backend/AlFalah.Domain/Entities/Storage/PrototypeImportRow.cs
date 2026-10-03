using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities.Storage;

public sealed class PrototypeImportRow : IStorageRecord
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int BatchId { get; set; }
    public int SourceOrdinal { get; set; }
    public string SourceRowSHA256 { get; set; } = string.Empty;
    public string? ReferencePath { get; set; }
    public PrototypeImportStatus Status { get; set; }
    public string? ExceptionNote { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}
