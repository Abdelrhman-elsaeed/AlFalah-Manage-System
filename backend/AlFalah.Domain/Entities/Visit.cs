using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities;

/// <summary>
/// A classroom visit (زيارة صفية) created by a Moderator or School Manager
/// against an Instructor (the evaluated teacher) within a single school.
///
/// Key invariants (Phase 4 + Phase 5):
///  - School-scoping is enforced at the service layer via <c>SchoolScopeGuard</c>.
///  - <see cref="RubricVersionId"/> is SNAPSHOTTED at creation from the currently
///    active version — historical visits keep pointing at the rubric version
///    that was in effect when they were created, so old reports stay accurate
///    after future rubric edits.
///  - On create, 25 <see cref="VisitScore"/> rows are pre-generated (one per
///    standard in the snapshot's RubricVersion) with null scores.
///  - Status state-machine (Phase 5):
///      Draft → PendingApproval (on Submit, persists analysis snapshot).
///      PendingApproval → Approved (School Manager approves).
///      PendingApproval → RejectedForChanges (returns to creator for edits).
///      PendingApproval → Approved (School Manager direct-edit + approve).
///      Approved → Reopened (requires ReopenReason; edits allowed again).
///      Reopened → PendingApproval (on resubmit; recomputes a NEW analysis snapshot
///          on the SAME RubricVersionId — historical visits do NOT switch to a
///          newer rubric version).
///  - Analysis is recomputed each time the visit is (re-)submitted; the
///    <see cref="VisitAnalysis"/> row is replaced atomically (1:1 with the visit).
/// </summary>
public class Visit
{
    public int Id { get; set; }

    /// <summary>Owning school (the visit is always about ONE school's classroom).</summary>
    public int SchoolId { get; set; }

    /// <summary>The evaluated teacher (user Id). Must have an active UserSchoolRole in <see cref="SchoolId"/> with role = Instructor.</summary>
    public string InstructorId { get; set; } = string.Empty;

    /// <summary>The Moderator/School Manager who created the visit.</summary>
    public string CreatedByUserId { get; set; } = string.Empty;

    /// <summary>
    /// SNAPSHOT of the active RubricVersion at creation time. Historical visits
    /// keep this id so their 25 standards stay bound to the rubric that was
    /// live when the visit was created.
    /// </summary>
    public int RubricVersionId { get; set; }

    public VisitCategory VisitCategory { get; set; }
    public VisitSequence VisitSequence { get; set; }
    public VisitStatus Status { get; set; } = VisitStatus.Draft;

    /// <summary>The actual classroom visit date (when the moderator attended the class).</summary>
    public DateTimeOffset VisitDate { get; set; }

    /// <summary>Subject taught (Arabic text, optional).</summary>
    public string? Subject { get; set; }

    /// <summary>Grade/class label (Arabic text, optional).</summary>
    public string? GradeClass { get; set; }

    /// <summary>Free-form notes (optional).</summary>
    public string? Notes { get; set; }

    /// <summary>Set when the visit transitions Draft → PendingApproval.</summary>
    public DateTimeOffset? SubmittedAt { get; set; }

    // ─── Phase 5: Approval workflow ─────────────────────────────────────────

    /// <summary>School Manager / Super Admin who approved the visit. Null until first approval.</summary>
    public string? ApprovedByUserId { get; set; }

    /// <summary>Timestamp of the most recent approval (set on every approve, including re-approval after reopen).</summary>
    public DateTimeOffset? ApprovedAt { get; set; }

    /// <summary>
    /// Reason the School Manager returned the visit to the creator
    /// (PendingApproval → RejectedForChanges). Required when the visit is
    /// rejected; surfaced to the Moderator as a banner on the visit detail.
    /// </summary>
    public string? RejectionReason { get; set; }

    /// <summary>
    /// Reason the School Manager reopened an already-approved visit
    /// (Approved → Reopened). Required for reopen; surfaced to the creator.
    /// </summary>
    public string? ReopenReason { get; set; }

    /// <summary>School Manager / Super Admin who reopened the visit (Approved → Reopened).</summary>
    public string? ReopenedByUserId { get; set; }

    /// <summary>Timestamp of the most recent reopen.</summary>
    public DateTimeOffset? ReopenedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Soft delete
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    // Navigation
    public School School { get; set; } = null!;
    public ApplicationUser Instructor { get; set; } = null!;
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public ApplicationUser? DeletedByUser { get; set; }
    public ApplicationUser? ApprovedByUser { get; set; }
    public ApplicationUser? ReopenedByUser { get; set; }
    public RubricVersion RubricVersion { get; set; } = null!;
    public ICollection<VisitScore> Scores { get; set; } = new List<VisitScore>();
    public VisitAnalysis? Analysis { get; set; }
    public ICollection<ReportViewLog> ViewLogs { get; set; } = new List<ReportViewLog>();
}