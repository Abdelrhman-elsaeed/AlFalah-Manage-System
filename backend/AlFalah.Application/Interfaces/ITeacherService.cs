using AlFalah.Application.DTOs.Teachers;
using AlFalah.Shared.Models;

namespace AlFalah.Application.Interfaces;

/// <summary>
/// D-71 — Teachers Management + Teacher Profile service.
///
/// "Teachers" are ApplicationUsers with the Instructor role + an active
/// UserSchoolRole + (optionally) an InstructorProfile. No parallel Teacher
/// table is created; this service joins the existing entities.
///
/// Visibility:
///  - MainManager / SuperAdmin: global (all schools).
///  - SchoolManager: his ActiveSchoolId only.
///  - Moderator: his ActiveSchoolId; visit-list + progress for a teacher
///    are further restricted to visits HE created (D-37).
///  - Instructor: N/A (this is an admin / supervisor feature).
///
/// Cross-school access → 403/404 via SchoolScopeGuard (D-24/D-28).
/// </summary>
public interface ITeacherService
{
    /// <summary>Scoped, paginated list of teachers.</summary>
    Task<PagedResult<TeacherListItemDto>> ListAsync(TeacherListQuery query, CancellationToken cancellationToken = default);

    /// <summary>Profile header for a single teacher (with scope check).</summary>
    Task<TeacherProfileDto> GetProfileAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// In-scope visits for the given teacher — used to populate the
    /// "الزيارات الصفية" table on the profile page. D-37 is enforced here
    /// for moderator callers.
    /// </summary>
    Task<List<TeacherVisitSummaryDto>> GetVisitsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Per-visit domain averages + the dynamic axis labels — feeds the
    /// RADAR chart on the profile page (one polygon per visit, axes =
    /// the domains of the visit's snapshot rubric version).
    /// </summary>
    Task<TeacherProgressDto> GetProgressAsync(string userId, CancellationToken cancellationToken = default);

    // ─── D-74 — Teaching info (Subject + Classes) for visit-form auto-fill ──
    //
    // The auto-fill endpoints share the same payload shape so the
    // visit-form can use either endpoint based on caller role:
    //  - Instructors use /api/v1/account/teaching (self-only)
    //  - Managers use  /api/v1/teachers/{userId}/teaching (in-scope)

    /// <summary>
    /// Returns the teacher's Subject + Stage + Classes. School-scoped via
    /// <see cref="ResolveTeacherInScopeAsync"/> — throws on cross-school or
    /// when the user is not an Instructor.
    /// </summary>
    Task<TeacherTeachingDto> GetTeachingAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the teacher's Subject + Stage + Classes. School-scoped —
    /// managers can only edit in-scope teachers (cross-school → 403).
    /// </summary>
    Task<TeacherTeachingDto> UpsertTeachingAsync(
        string userId,
        TeacherTeachingUpsertRequest request,
        CancellationToken cancellationToken = default);
}