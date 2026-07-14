using AlFalah.Application.Common;
using AlFalah.Application.DTOs.Dashboards;
using AlFalah.Application.Interfaces;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

/// <summary>
/// Phase 9 — Role-based dashboard endpoints + scoped Excel/PDF exports.
///
/// Thin controller. Every visibility rule lives in <see cref="IDashboardService"/>:
///  - Main Manager dashboard: global, NO complaint content (Phase 8 rule).
///  - School Manager: scoped to caller's ActiveSchoolId.
///  - Moderator: scoped to ActiveSchoolId + own-visits-only (D-37).
///  - Instructor: scoped to own approved visits (D-36).
///
/// The permission gates here are defense-in-depth — the service also
/// re-asserts the role-appropriate permission at the top of every method.
/// </summary>
[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    // ─── GET /api/v1/dashboard/main-manager ────────────────────────────────

    [HttpGet("main-manager")]
    [ProducesResponseType(typeof(ApiResponse<MainManagerDashboardDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    public async Task<IActionResult> GetMainManagerDashboard(
        [FromQuery] int? academicYear = null,
        [FromQuery] string? semester = null,
        [FromQuery] int? schoolId = null,
        [FromQuery] string? subject = null,
        [FromQuery] string? stage = null,
        [FromQuery] string? moderatorUserId = null,
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var filter = new DashboardFilterDto
        {
            AcademicYear = academicYear,
            Semester = semester,
            SchoolId = schoolId,
            Subject = subject,
            Stage = stage,
            ModeratorUserId = moderatorUserId,
            FromDate = fromDate,
            ToDate = toDate
        };
        var result = await _dashboardService.GetMainManagerDashboardAsync(filter, cancellationToken);
        return Ok(ApiResponse<MainManagerDashboardDto>.Success(result));
    }

    // ─── GET /api/v1/dashboard/school-manager ─────────────────────────────

    [HttpGet("school-manager")]
    [ProducesResponseType(typeof(ApiResponse<SchoolManagerDashboardDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    public async Task<IActionResult> GetSchoolManagerDashboard(
        [FromQuery] int? academicYear = null,
        [FromQuery] string? semester = null,
        [FromQuery] int? schoolId = null,
        [FromQuery] string? subject = null,
        [FromQuery] string? stage = null,
        [FromQuery] string? moderatorUserId = null,
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var filter = new DashboardFilterDto
        {
            AcademicYear = academicYear,
            Semester = semester,
            SchoolId = schoolId,
            Subject = subject,
            Stage = stage,
            ModeratorUserId = moderatorUserId,
            FromDate = fromDate,
            ToDate = toDate
        };
        var result = await _dashboardService.GetSchoolManagerDashboardAsync(filter, cancellationToken);
        return Ok(ApiResponse<SchoolManagerDashboardDto>.Success(result));
    }

    // ─── GET /api/v1/dashboard/moderator ───────────────────────────────────

    [HttpGet("moderator")]
    [ProducesResponseType(typeof(ApiResponse<ModeratorDashboardDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    public async Task<IActionResult> GetModeratorDashboard(
        [FromQuery] int? academicYear = null,
        [FromQuery] string? semester = null,
        [FromQuery] int? schoolId = null,
        [FromQuery] string? subject = null,
        [FromQuery] string? stage = null,
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var filter = new DashboardFilterDto
        {
            AcademicYear = academicYear,
            Semester = semester,
            SchoolId = schoolId,
            Subject = subject,
            Stage = stage,
            FromDate = fromDate,
            ToDate = toDate
        };
        var result = await _dashboardService.GetModeratorDashboardAsync(filter, cancellationToken);
        return Ok(ApiResponse<ModeratorDashboardDto>.Success(result));
    }

    // ─── GET /api/v1/dashboard/instructor ──────────────────────────────────

    [HttpGet("instructor")]
    [ProducesResponseType(typeof(ApiResponse<InstructorDashboardDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    public async Task<IActionResult> GetInstructorDashboard(
        [FromQuery] int? academicYear = null,
        [FromQuery] string? semester = null,
        [FromQuery] int? schoolId = null,
        [FromQuery] string? subject = null,
        [FromQuery] string? stage = null,
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var filter = new DashboardFilterDto
        {
            AcademicYear = academicYear,
            Semester = semester,
            SchoolId = schoolId,
            Subject = subject,
            Stage = stage,
            FromDate = fromDate,
            ToDate = toDate
        };
        var result = await _dashboardService.GetInstructorDashboardAsync(filter, cancellationToken);
        return Ok(ApiResponse<InstructorDashboardDto>.Success(result));
    }

    // ─── GET /api/v1/dashboard/export/excel?role=... ──────────────────────
    // The same scope rules as the corresponding dashboard endpoint apply.

    [HttpGet("export/excel")]
    [ProducesResponseType(200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    public async Task<IActionResult> ExportExcel(
        [FromQuery] DashboardRole role,
        [FromQuery] int? academicYear = null,
        [FromQuery] string? semester = null,
        [FromQuery] int? schoolId = null,
        [FromQuery] string? subject = null,
        [FromQuery] string? stage = null,
        [FromQuery] string? moderatorUserId = null,
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var filter = new DashboardFilterDto
        {
            AcademicYear = academicYear,
            Semester = semester,
            SchoolId = schoolId,
            Subject = subject,
            Stage = stage,
            ModeratorUserId = moderatorUserId,
            FromDate = fromDate,
            ToDate = toDate
        };
        var result = await _dashboardService.ExportExcelAsync(role, filter, cancellationToken);
        return File(result.Bytes, result.ContentType, result.FileName);
    }

    // ─── GET /api/v1/dashboard/export/pdf?role=... ────────────────────────

    [HttpGet("export/pdf")]
    [ProducesResponseType(200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    public async Task<IActionResult> ExportPdf(
        [FromQuery] DashboardRole role,
        [FromQuery] int? academicYear = null,
        [FromQuery] string? semester = null,
        [FromQuery] int? schoolId = null,
        [FromQuery] string? subject = null,
        [FromQuery] string? stage = null,
        [FromQuery] string? moderatorUserId = null,
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var filter = new DashboardFilterDto
        {
            AcademicYear = academicYear,
            Semester = semester,
            SchoolId = schoolId,
            Subject = subject,
            Stage = stage,
            ModeratorUserId = moderatorUserId,
            FromDate = fromDate,
            ToDate = toDate
        };
        var result = await _dashboardService.ExportPdfAsync(role, filter, cancellationToken);
        return File(result.Bytes, result.ContentType, result.FileName);
    }
}
