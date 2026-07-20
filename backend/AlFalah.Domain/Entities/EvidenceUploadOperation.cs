using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities;

/// <summary>
/// A lightweight idempotency reservation. It is intentionally separate from a
/// submission so the matrix is never changed until OneDrive confirms success.
/// </summary>
public class EvidenceUploadOperation
{
    public long Id { get; set; }
    public int TeacherId { get; set; }
    public int SchoolId { get; set; }
    public int TaskId { get; set; }
    public int AcademicYearId { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public EvidenceUploadOperationStatus Status { get; set; } = EvidenceUploadOperationStatus.Pending;
    public long? SubmissionId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
