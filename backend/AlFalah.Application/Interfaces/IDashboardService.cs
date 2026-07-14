using AlFalah.Application.DTOs.Dashboards;

namespace AlFalah.Application.Interfaces;

/// <summary>
/// Phase 9 — role-based dashboard aggregator. All four methods are
/// server-side, scope-aware, and never trust client filters for security:
/// client-side <see cref="DashboardFilterDto"/> arguments only NARROW within
/// the caller's allowed scope.
///
/// Visibility contract (docs/03 + deviations D-24/D-28/D-36/D-37 + Phase 8
/// Main-Manager-no-complaints rule):
///  - <see cref="GetMainManagerDashboardAsync"/>: global, Main Manager + Super
///    Admin only. NO complaint content / counts-with-detail (Phase 8 rule).
///  - <see cref="GetSchoolManagerDashboardAsync"/>: scoped to caller's
///    <c>ActiveSchoolId</c> (cross-school is impossible — enforced by
///    <c>SchoolScopeGuard</c>).
///  - <see cref="GetModeratorDashboardAsync"/>: scoped to caller's
///    <c>ActiveSchoolId</c> AND <c>Visit.CreatedByUserId == self</c> (D-37).
///    Complaints listed are ONLY those whose snapshotted
///    <c>ModeratorUserId == self</c>.
///  - <see cref="GetInstructorDashboardAsync"/>: scoped to
///    <c>Visit.InstructorId == self</c> AND <c>Visit.Status == Approved</c>
///    (D-36).
/// </summary>
public interface IDashboardService
{
    Task<MainManagerDashboardDto> GetMainManagerDashboardAsync(
        DashboardFilterDto filter,
        CancellationToken cancellationToken = default);

    Task<SchoolManagerDashboardDto> GetSchoolManagerDashboardAsync(
        DashboardFilterDto filter,
        CancellationToken cancellationToken = default);

    Task<ModeratorDashboardDto> GetModeratorDashboardAsync(
        DashboardFilterDto filter,
        CancellationToken cancellationToken = default);

    Task<InstructorDashboardDto> GetInstructorDashboardAsync(
        DashboardFilterDto filter,
        CancellationToken cancellationToken = default);

    // ─── Exports ──────────────────────────────────────────────────────────
    // The scope rules above apply to exports as well — a Moderator export
    // contains ONLY his own visits; a School Manager export contains ONLY
    // his school's visits; a Main Manager export is global but the
    // "complaints" sheet is empty / has only a NO-CONTENT header.

    Task<DashboardExportResult> ExportExcelAsync(
        DashboardRole role,
        DashboardFilterDto filter,
        CancellationToken cancellationToken = default);

    Task<DashboardExportResult> ExportPdfAsync(
        DashboardRole role,
        DashboardFilterDto filter,
        CancellationToken cancellationToken = default);
}

/// <summary>Which role's dashboard to render — controls both metric scope
/// and the export sheet layout.</summary>
public enum DashboardRole
{
    MainManager = 1,
    SchoolManager = 2,
    Moderator = 3,
    Instructor = 4
}

/// <summary>
/// All optional filters from the client. Server-side scope is ALWAYS the
/// authoritative filter; these values only NARROW within that scope.
/// </summary>
public class DashboardFilterDto
{
    public int? AcademicYear { get; set; }
    public string? Semester { get; set; }
    public int? SchoolId { get; set; }
    public string? Subject { get; set; }
    public string? Stage { get; set; }
    public string? ModeratorUserId { get; set; }
    public DateTimeOffset? FromDate { get; set; }
    public DateTimeOffset? ToDate { get; set; }
}

/// <summary>Byte payload for an Excel/PDF export.</summary>
public class DashboardExportResult
{
    public byte[] Bytes { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}
