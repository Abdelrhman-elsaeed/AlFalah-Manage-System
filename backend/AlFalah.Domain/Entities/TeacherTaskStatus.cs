using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities;

/// <summary>
/// Materialized per-teacher/per-task summary. It is the authoritative source
/// for the matrix and is recalculated whenever a related submission changes.
/// </summary>
public class TeacherTaskStatus
{
    public long Id { get; set; }
    public int TeacherId { get; set; }
    public int SchoolId { get; set; }
    public int TaskId { get; set; }
    public int AcademicYearId { get; set; }
    public int ActiveFilesCount { get; set; }
    public EvidenceCellStatus CellStatus { get; set; } = EvidenceCellStatus.NotUploaded;
    public DateTimeOffset? LastSubmissionAtUtc { get; set; }
    public DateTimeOffset? LastReviewedAtUtc { get; set; }
    public string? LastReviewedByUserId { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public InstructorProfile Teacher { get; set; } = null!;
    public School School { get; set; } = null!;
    public EvidenceTask Task { get; set; } = null!;
    public AcademicYear AcademicYear { get; set; } = null!;
}
