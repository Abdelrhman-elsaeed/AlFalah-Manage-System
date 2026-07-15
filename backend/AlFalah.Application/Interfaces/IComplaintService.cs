using AlFalah.Application.DTOs.Complaints;

namespace AlFalah.Application.Interfaces;

/// <summary>
/// Phase 8 — complaints workflow. ALL visibility is enforced server-side:
///  - Create: ONLY the visit's own Instructor, ONLY when the visit is Approved
///    AND a ReportViewLog proves the instructor viewed the report (D-36).
///  - List/Detail: School Manager = his school; Instructor = own; SuperAdmin =
///    global support. **Main Manager = HARD 403** and **Moderator = HARD 403**
///    (D-75), even if a Complaint.* permission were ever leaked.
///  - Status transitions: Open → InReview → Resolved | Rejected → Closed.
///  - Reopen-from-complaint reuses Phase 5 <c>IVisitService.ReopenAsync</c>
///    (reason required, audited; resubmit recomputes the analysis snapshot on
///    the SAME RubricVersionId).
///  - Every mutation writes an AuditLog row.
/// </summary>
public interface IComplaintService
{
    Task<ComplaintDto> CreateAsync(int visitId, CreateComplaintRequestDto request, CancellationToken cancellationToken = default);
    Task<List<ComplaintDto>> ListAsync(int? status, CancellationToken cancellationToken = default);
    Task<ComplaintDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ComplaintDto> UpdateStatusAsync(int id, UpdateComplaintStatusRequestDto request, CancellationToken cancellationToken = default);
    Task<ComplaintDto> ReopenVisitAsync(int id, ReopenVisitFromComplaintRequestDto request, CancellationToken cancellationToken = default);
    Task SoftDeleteAsync(int id, CancellationToken cancellationToken = default);
}
