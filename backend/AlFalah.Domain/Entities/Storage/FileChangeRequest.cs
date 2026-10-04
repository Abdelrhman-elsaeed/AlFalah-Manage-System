namespace AlFalah.Domain.Entities.Storage;

public sealed class FileChangeRequest : IStorageRecord
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int StoredFileId { get; set; }
    public int OriginalVersionId { get; set; }
    public int? CandidateVersionId { get; set; }
    public string Kind { get; set; } = "Replace";
    public string Status { get; set; } = "Pending";
    public string Reason { get; set; } = "";
    public string RequestedByUserId { get; set; } = "";
    public bool ReplaceBeforeReview { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}

public sealed class FileChangeDecision : IStorageRecord
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int FileChangeRequestId { get; set; }
    public string Decision { get; set; } = "";
    public string ReviewedByUserId { get; set; } = "";
    public string ReviewerName { get; set; } = "";
    public string? Note { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}
