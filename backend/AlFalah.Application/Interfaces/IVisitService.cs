using AlFalah.Application.DTOs.Visits;
using AlFalah.Shared.Models;

namespace AlFalah.Application.Interfaces;

/// <summary>
/// Visit management + analysis + approval service.
///
/// Phase 4 invariants:
///  - School-scoping is enforced for every read/write (via <c>SchoolScopeGuard</c>).
///  - On create: <c>RubricVersionId</c> is SNAPSHOTTED from the currently active version,
///    and exactly 25 <c>VisitScore</c> rows are pre-generated with null scores.
///  - On draft save: partial scores are accepted (each must be 0..4 or null).
///  - On submit: ALL 25 standards must be scored (no nulls); on success the visit
///    transitions Draft → PendingApproval, <c>SubmittedAt</c> is set, and the
///    <c>VisitAnalysis</c> snapshot is computed + persisted ONCE.
///
/// Phase 5 additions (state machine + instructor visibility + audit):
///  - Approval state machine: Draft → PendingApproval → Approved | RejectedForChanges.
///    Approved → Reopened (reason required) → PendingApproval (recompute analysis).
///  - Approve / Reject / Reopen require the School Manager of the visit's school
///    (or SuperAdmin / MainManager); cross-school access rejected with 403.
///  - Edit-after-submit is allowed ONLY when status is Draft | RejectedForChanges
///    (by the visit creator / School Manager) OR when status is PendingApproval
///    (School Manager direct-edit path).
///  - Recompute-on-reopen: on Approved → Reopened → resubmit, a NEW VisitAnalysis
///    is computed on the SAME RubricVersionId and the previous snapshot row is
///    replaced atomically (the visit ↔ analysis 1:1 holds).
///  - Every approve / reject / edit-after-reject / reopen / resubmit-after-reopen
///    writes an <c>AuditLog</c> row (Action, EntityName=Visit, EntityId,
///    OldValues/NewValues JSON, Reason, UserId, SchoolId).
///  - Instructor visibility: <see cref="GetInstructorReportAsync"/> returns the
///    full result (scores + analysis) ONLY when status == Approved AND
///    Visit.InstructorId == current user; creates a <c>ReportViewLog</c> row.
///  - <see cref="GetReportViewStatusAsync"/> aggregates first/last/count of
///    report views for the manager/moderator detail view.
/// </summary>
public interface IVisitService
{
    Task<VisitDetailDto> CreateAsync(CreateVisitRequestDto request, CancellationToken cancellationToken = default);
    Task<VisitDetailDto> UpdateAsync(int id, UpdateVisitRequestDto request, CancellationToken cancellationToken = default);
    Task<VisitDetailDto> SubmitAsync(int id, CancellationToken cancellationToken = default);
    Task SoftDeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<VisitDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<VisitAnalysisDto?> GetAnalysisAsync(int id, CancellationToken cancellationToken = default);

    Task<PagedResult<VisitListItemDto>> ListAsync(VisitListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Instructor-facing feed of the caller's approved reports only. The caller
    /// identity and <c>Approved</c> status are imposed server-side; neither can
    /// be widened by query parameters.
    /// </summary>
    Task<PagedResult<VisitListItemDto>> ListInstructorApprovedReportsAsync(
        VisitListQuery query,
        CancellationToken cancellationToken = default);

    // ─── Phase 5: approval / reopen / instructor visibility ─────────────────

    /// <summary>
    /// PendingApproval → Approved. Caller MUST be the visit's school manager
    /// (or SuperAdmin / MainManager); cross-school access rejected with 403.
    /// Writes an AuditLog row.
    /// </summary>
    Task<VisitDetailDto> ApproveAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// PendingApproval → RejectedForChanges. <paramref name="reason"/> is required
    /// (non-empty) and is surfaced to the creator so they can fix + resubmit.
    /// Writes an AuditLog row.
    /// </summary>
    Task<VisitDetailDto> RejectAsync(int id, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approved → Reopened. <paramref name="reason"/> is required. The visit
    /// becomes editable again; the analysis snapshot is NOT deleted here — it
    /// stays in place until the next resubmit recomputes a new one.
    /// Writes an AuditLog row.
    /// </summary>
    Task<VisitDetailDto> ReopenAsync(int id, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the full result (scores + analysis snapshot) for an Instructor.
    /// Hard-gated: status MUST be Approved AND Visit.InstructorId MUST equal the
    /// caller's user id. Records a <c>ReportViewLog</c> on success.
    /// </summary>
    Task<InstructorReportDto> GetInstructorReportAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggregated report-view status for the manager / moderator detail view:
    /// first viewed at, last viewed at, total view count.
    /// School-scope enforced; instructors cannot call this (their own view count
    /// is implicit / not surfaced here).
    /// </summary>
    Task<ReportViewStatusDto> GetReportViewStatusAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Phase 6 / Stage 1 + D-41 — assembles the immutable <see cref="AlFalah.Application.DTOs.Reports.VisitReportDto"/>
    /// for the visit's PDF report. Authorization mirrors the existing gates
    /// (D-24 / D-28 / D-36 / D-37) and is enforced inside the service:
    ///  - Instructor: only OWN visit (D-36 close).
    ///  - School Manager: only visits in HIS school.
    ///  - Moderator: only visits HE created (D-37).
    ///  - SuperAdmin / MainManager: global.
    /// D-41 RELAXATION: the "Status MUST be Approved" gate has been lifted.
    /// Non-Approved visits can now produce a PDF that carries a clear
    /// "مسودة — غير معتمدة" watermark so it cannot be mistaken for an
    /// official report. All other visibility gates (school-scope / moderator
    /// own-only / instructor own-only) remain unchanged.
    /// Records a <c>ReportViewLog</c> on success (PDF download = a view).
    /// </summary>
    Task<AlFalah.Application.DTOs.Reports.VisitReportDto> GetVisitReportAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// D-41 / Task 6 — bulk export. Reuses the same scoped query as the list
    /// endpoint (school-scope + moderator own-only + global admin bypass) so
    /// the caller only ever receives visits they are allowed to see. Returns
    /// the visit ids in scope (the controller will iterate them and stream a
    /// ZIP of individual PDFs).
    /// </summary>
    Task<List<int>> ListScopedVisitIdsForExportAsync(VisitListQuery query, CancellationToken cancellationToken = default);
}
