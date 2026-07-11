namespace AlFalah.Domain.Entities;

/// <summary>
/// Phase 5 — logs every time an Instructor opens an approved visit's report
/// (the instructor "viewed" the visit's full result).
///
/// One row per view (record every view; expose "first viewed / last viewed / count"
/// to managers via the report-view-status endpoint).
///
/// School-scoping note: a row is only inserted when the current user is the
/// visit's Instructor and the visit is <c>Status == Approved</c> — both checks
/// happen in <c>VisitService.GetReportForInstructorAsync</c>.
///
/// Soft-delete: kept like other Phase 4 entities; a deleted row is excluded
/// from the view-status counts via the global query filter.
/// </summary>
public class ReportViewLog
{
    public long Id { get; set; }

    public int VisitId { get; set; }

    /// <summary>The Instructor who opened the report (always equal to <c>Visit.InstructorId</c>; carried here for fast lookup + audit clarity).</summary>
    public string InstructorUserId { get; set; } = string.Empty;

    /// <summary>When the report was opened.</summary>
    public DateTimeOffset ViewedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Best-effort capture of the caller's IP address. Null if the host couldn't determine one (dev / health checks).</summary>
    public string? IpAddress { get; set; }

    // Soft delete
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    // Navigation
    public Visit Visit { get; set; } = null!;
}