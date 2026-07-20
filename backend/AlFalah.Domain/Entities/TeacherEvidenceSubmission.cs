using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities;

/// <summary>Audit-friendly metadata for a file successfully sent to OneDrive; file bytes are never stored locally.</summary>
public class TeacherEvidenceSubmission
{
    public long Id { get; set; }
    public int TeacherId { get; set; }
    public int SchoolId { get; set; }
    // Nullable only for legacy files uploaded before the evidence matrix existed.
    // All new uploads are validated server-side and always carry both keys.
    public int? TaskId { get; set; }
    public int? AcademicYearId { get; set; }
    public string DriveId { get; set; } = string.Empty;
    public string DriveItemId { get; set; } = string.Empty;
    public string ParentItemId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? FileExtension { get; set; }
    public string? MimeType { get; set; }
    public long SizeInBytes { get; set; }
    public string? WebUrl { get; set; }
    public string? ETag { get; set; }
    public EvidenceUploadStatus UploadStatus { get; set; }
    public EvidenceReviewStatus ReviewStatus { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public string? ReviewedByUserId { get; set; }
    public string? ReviewNote { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public string? DeletedByUserId { get; set; }
    public bool IsMissingFromDrive { get; set; }
    public DateTimeOffset? MissingFromDriveAtUtc { get; set; }
    public DateTimeOffset UploadedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public InstructorProfile Teacher { get; set; } = null!;
    public School School { get; set; } = null!;
    public EvidenceTask? Task { get; set; }
    public AcademicYear? AcademicYear { get; set; }
}
