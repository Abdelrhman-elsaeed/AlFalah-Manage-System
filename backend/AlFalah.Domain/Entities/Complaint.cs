using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities;

/// <summary>
/// Phase 8 — an Instructor's complaint / review request (شكوى / طلب مراجعة)
/// submitted AFTER viewing an APPROVED visit report (Phase 5 visibility).
///
/// Key invariants (docs/phases/PHASE-08-COMPLAINTS.md):
///  - Only the visit's OWN Instructor can create a complaint, and only when the
///    visit is Approved AND a ReportViewLog row proves they viewed the report.
///  - <see cref="ModeratorUserId"/> snapshots the visit's CreatedByUserId so the
///    related Moderator's scoped visibility (D-37 pattern) works without joins.
///  - Visibility: School Manager = all complaints in HIS school; related
///    Moderator = ONLY complaints on visits HE created; Instructor = own;
///    SuperAdmin = support/global; **Main Manager = HARD-BLOCKED (403)**.
///  - Status state machine: Open → InReview → Resolved | Rejected → Closed.
///  - A complaint can trigger a Phase 5 visit reopen (reason required, audited);
///    the resubmit recomputes the analysis snapshot on the SAME RubricVersionId.
/// </summary>
public class Complaint
{
    public int Id { get; set; }

    /// <summary>Owning school (copied from the visit at creation).</summary>
    public int SchoolId { get; set; }

    /// <summary>The complained-about visit.</summary>
    public int VisitId { get; set; }

    /// <summary>The submitting Instructor (must equal Visit.InstructorId).</summary>
    public string InstructorUserId { get; set; } = string.Empty;

    /// <summary>The visit's creator (Visit.CreatedByUserId) — snapshot for moderator-scoped visibility.</summary>
    public string ModeratorUserId { get; set; } = string.Empty;

    /// <summary>Short Arabic subject/title (required).</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Complaint body / message (required).</summary>
    public string Body { get; set; } = string.Empty;

    public ComplaintStatus Status { get; set; } = ComplaintStatus.Open;

    /// <summary>Resolution note added by the handler (School Manager / SuperAdmin).</summary>
    public string? ResolutionNote { get; set; }

    /// <summary>Who last handled (status change / reopen) the complaint.</summary>
    public string? HandledByUserId { get; set; }
    public DateTimeOffset? HandledAt { get; set; }

    /// <summary>Set when this complaint triggered a Phase 5 visit reopen (links the reopen to the complaint).</summary>
    public DateTimeOffset? VisitReopenedAt { get; set; }

    /// <summary>The reopen reason captured on this complaint (also written to Visit.ReopenReason with a complaint reference).</summary>
    public string? VisitReopenReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedByUserId { get; set; }

    // Soft delete
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    // Navigation
    public School School { get; set; } = null!;
    public Visit Visit { get; set; } = null!;
    public ApplicationUser Instructor { get; set; } = null!;
    public ApplicationUser Moderator { get; set; } = null!;
    public ApplicationUser? HandledByUser { get; set; }
}
