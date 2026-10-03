namespace AlFalah.Domain.Entities.Storage;

public sealed class VisitArchiveArtifact : IStorageRecord
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int VisitId { get; set; }
    public int ApprovalRevision { get; set; }
    public int OperationId { get; set; }
    public int StoredFileVersionId { get; set; }
    public bool IsCurrent { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}
