using AlFalah.Application.Common;
using AlFalah.Application.DTOs.Visits;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Services;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

/// <summary>
/// Visits &amp; scoring endpoints (Phase 4 + Phase 5 + Phase 6 Stage 1 + D-41).
///
/// Permissions:
///  - Visit.View   : GET list / detail / analysis / report / view-status
///  - Visit.Create : POST (create draft)
///  - Visit.Edit   : PUT (update draft) / POST submit
///  - Visit.Delete : DELETE (soft delete, Draft only)
///  - Visit.Approve: POST approve / reject
///  - Visit.Reopen : POST reopen
///
/// School-scoping is enforced inside <see cref="VisitService"/> via
/// <c>SchoolScopeGuard</c>; this controller never sees schoolId from the
/// client except for the list endpoint where it's coerced.
///
/// D-41 endpoints (PDF / ZIP export) intentionally have NO permission gate
/// at the controller — authorization is data-driven inside the service,
/// mirroring the existing /report endpoint pattern.
/// </summary>
[ApiController]
[Route("api/v1/visits")]
[Authorize]
public class VisitsController : ControllerBase
{
    private readonly IVisitService _visitService;
    private readonly ICurrentUserService _currentUser;
    private readonly IPdfReportService _pdfReportService;
    private readonly IVisitsBulkExportService _bulkExportService;

    public VisitsController(
        IVisitService visitService,
        ICurrentUserService currentUser,
        IPdfReportService pdfReportService,
        IVisitsBulkExportService bulkExportService)
    {
        _visitService = visitService;
        _currentUser = currentUser;
        _pdfReportService = pdfReportService;
        _bulkExportService = bulkExportService;
    }

    // ─── GET list ─────────────────────────────────────────────────────────────

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<VisitListItemDto>>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? status = null,
        [FromQuery] string? instructorId = null,
        [FromQuery] int? visitCategory = null,
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDesc = false,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.HasPermission(PermissionNames.VisitView))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لعرض الزيارات."));

        var query = new VisitListQuery
        {
            Page = page,
            PageSize = pageSize,
            Status = status,
            InstructorId = instructorId,
            VisitCategory = visitCategory,
            FromDate = fromDate,
            ToDate = toDate,
            SortBy = sortBy,
            SortDesc = sortDesc
        };

        var errors = await ValidationHelper.ValidateAsync(HttpContext.RequestServices, query, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<PagedResult<VisitListItemDto>>.Fail(errors));

        var result = await _visitService.ListAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<VisitListItemDto>>.Success(result));
    }

    // ─── GET detail ──────────────────────────────────────────────────────────

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<VisitDetailDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.VisitView))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لعرض الزيارات."));

        var result = await _visitService.GetByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<VisitDetailDto>.Success(result));
    }

    // ─── POST create ─────────────────────────────────────────────────────────

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<VisitDetailDto>), 201)]
    [ProducesResponseType(typeof(ApiResponse<VisitDetailDto>), 400)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    public async Task<IActionResult> Create([FromBody] CreateVisitRequestDto request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.VisitCreate))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لإنشاء زيارة."));

        var errors = await ValidationHelper.ValidateAsync(HttpContext.RequestServices, request, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<VisitDetailDto>.Fail(errors));

        var result = await _visitService.CreateAsync(request, cancellationToken);
        return StatusCode(201, ApiResponse<VisitDetailDto>.Success(result, "تم إنشاء مسودة الزيارة بنجاح."));
    }

    // ─── PUT update draft ─────────────────────────────────────────────────────

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<VisitDetailDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<VisitDetailDto>), 400)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateVisitRequestDto request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.VisitEdit))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لتعديل الزيارات."));

        var errors = await ValidationHelper.ValidateAsync(HttpContext.RequestServices, request, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<VisitDetailDto>.Fail(errors));

        var result = await _visitService.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponse<VisitDetailDto>.Success(result, "تم حفظ مسودة الزيارة."));
    }

    // ─── POST submit ─────────────────────────────────────────────────────────

    [HttpPost("{id:int}/submit")]
    [ProducesResponseType(typeof(ApiResponse<VisitDetailDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<VisitDetailDto>), 400)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> Submit(int id, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.VisitEdit))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لتسليم الزيارات."));

        var result = await _visitService.SubmitAsync(id, cancellationToken);
        return Ok(ApiResponse<VisitDetailDto>.Success(result, "تم إرسال الزيارة للاعتماد."));
    }

    // ─── DELETE soft delete ──────────────────────────────────────────────────

    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> SoftDelete(int id, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.VisitDelete))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لحذف الزيارات."));

        await _visitService.SoftDeleteAsync(id, cancellationToken);
        return Ok(ApiResponse.Success("تم حذف الزيارة."));
    }

    // ─── GET analysis snapshot ───────────────────────────────────────────────

    [HttpGet("{id:int}/analysis")]
    [ProducesResponseType(typeof(ApiResponse<VisitAnalysisDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> GetAnalysis(int id, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.VisitView))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لعرض الزيارات."));

        var result = await _visitService.GetAnalysisAsync(id, cancellationToken);
        if (result == null)
            return NotFound(ApiResponse.Fail("لا يوجد تحليل محفوظ لهذه الزيارة. أرسلها أولاً."));

        return Ok(ApiResponse<VisitAnalysisDto>.Success(result));
    }

    // ─── Phase 5: Approval workflow endpoints ───────────────────────────────

    /// <summary>POST /api/v1/visits/{id}/approve — PendingApproval → Approved.</summary>
    [HttpPost("{id:int}/approve")]
    [ProducesResponseType(typeof(ApiResponse<VisitDetailDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<VisitDetailDto>), 400)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> Approve(int id, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.VisitApprove))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لاعتماد الزيارات."));

        var result = await _visitService.ApproveAsync(id, cancellationToken);
        return Ok(ApiResponse<VisitDetailDto>.Success(result, "تم اعتماد الزيارة."));
    }

    /// <summary>POST /api/v1/visits/{id}/reject — PendingApproval → RejectedForChanges (reason required).</summary>
    [HttpPost("{id:int}/reject")]
    [ProducesResponseType(typeof(ApiResponse<VisitDetailDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<VisitDetailDto>), 400)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectVisitRequestDto request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.VisitApprove))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لرفض الزيارات."));

        var errors = await ValidationHelper.ValidateAsync(HttpContext.RequestServices, request, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<VisitDetailDto>.Fail(errors));

        var result = await _visitService.RejectAsync(id, request.Reason, cancellationToken);
        return Ok(ApiResponse<VisitDetailDto>.Success(result, "تم رفض الزيارة وإعادتها إلى المنشئ للتعديل."));
    }

    /// <summary>POST /api/v1/visits/{id}/reopen — Approved → Reopened (reason required).</summary>
    [HttpPost("{id:int}/reopen")]
    [ProducesResponseType(typeof(ApiResponse<VisitDetailDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<VisitDetailDto>), 400)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> Reopen(int id, [FromBody] ReopenVisitRequestDto request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.VisitReopen))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لإعادة فتح الزيارات."));

        var errors = await ValidationHelper.ValidateAsync(HttpContext.RequestServices, request, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<VisitDetailDto>.Fail(errors));

        var result = await _visitService.ReopenAsync(id, request.Reason, cancellationToken);
        return Ok(ApiResponse<VisitDetailDto>.Success(result, "تم إعادة فتح الزيارة."));
    }

    // ─── Phase 5: Instructor visibility + view-status ────────────────────────

    /// <summary>
    /// GET /api/v1/visits/{id}/report — Instructor-only report.
    /// Returns 403 unless status == Approved AND the current user is the visit's instructor.
    /// On success records a <c>ReportViewLog</c> row.
    /// </summary>
    [HttpGet("{id:int}/report")]
    [ProducesResponseType(typeof(ApiResponse<InstructorReportDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> GetInstructorReport(int id, CancellationToken cancellationToken)
    {
        // No permission gate here — authorization is data-driven (status == Approved + instructor match).
        // Instructors always carry Visit.View (D-27) which lets them list their visits; the gate
        // is inside the service.
        try
        {
            var result = await _visitService.GetInstructorReportAsync(id, cancellationToken);
            return Ok(ApiResponse<InstructorReportDto>.Success(result));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse.Fail(ex.Message));
        }
        catch (UnauthorizedSchoolAccessException ex)
        {
            return StatusCode(403, ApiResponse.Fail(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// GET /api/v1/visits/{id}/view-status — aggregated report-view status
    /// (first viewed / last viewed / count) for the manager / moderator.
    /// </summary>
    [HttpGet("{id:int}/view-status")]
    [ProducesResponseType(typeof(ApiResponse<ReportViewStatusDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> GetReportViewStatus(int id, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.VisitView))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لعرض الزيارات."));

        var result = await _visitService.GetReportViewStatusAsync(id, cancellationToken);
        return Ok(ApiResponse<ReportViewStatusDto>.Success(result));
    }

    // ─── Phase 6 / Stage 1: PDF report download ───────────────────────────────

    /// <summary>
    /// GET /api/v1/visits/{id}/report/pdf — server-side Arabic PDF download.
    /// Returns <c>application/pdf</c> bytes (NOT wrapped in ApiResponse).
    ///
    /// D-41 RELAXATION: the "Status MUST be Approved" gate has been lifted.
    /// PDFs are now allowed for any visit. Non-Approved visits carry a clear
    /// Arabic watermark ("مسودة — غير معتمدة") so the document cannot be
    /// mistaken for an official report.
    ///
    /// Permissions + visibility enforced inside <see cref="VisitService.GetVisitReportAsync"/>:
    ///  - Instructor: only OWN visit (D-36).
    ///  - School Manager: only visits in HIS school.
    ///  - Moderator: only visits HE created (D-37).
    ///  - SuperAdmin / MainManager: global.
    /// Cross-school / non-owner → 403/404 with Arabic ApiResponse.
    /// </summary>
    [HttpGet("{id:int}/report/pdf")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> DownloadReportPdf(int id, CancellationToken cancellationToken)
    {
        // Authorization is data-driven inside the service; we mirror the
        // /report endpoint pattern (no permission gate at the controller —
        // Visit.View is granted to all roles but the service enforces
        // per-role visibility).
        try
        {
            var reportDto = await _visitService.GetVisitReportAsync(id, cancellationToken);
            var pdfBytes = await _pdfReportService.RenderAsync(reportDto, cancellationToken);

            // D-41 / Task 7 — filename pattern "{teacher} - {year} - {visitType}.pdf".
            var fileName = PdfReportService.BuildPdfFilename(
                reportDto.InstructorFullName,
                reportDto.VisitDate,
                reportDto.VisitCategoryLabelAr);
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse.Fail(ex.Message));
        }
        catch (UnauthorizedSchoolAccessException ex)
        {
            return StatusCode(403, ApiResponse.Fail(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    // ─── D-41 / Task 6: bulk ZIP export ──────────────────────────────────────

    /// <summary>
    /// GET /api/v1/visits/export/zip — bulk export every visit currently
    /// visible to the caller (respecting school-scope / moderator own-only
    /// / global admin bypass). Generates one PDF per visit (with watermark
    /// on non-Approved) and packs them into a single ZIP returned as
    /// <c>application/zip</c>.
    ///
    /// Each entry inside the ZIP follows the filename pattern:
    ///   "{teacher} - {year} - {visitType}.pdf"
    /// (sanitized; duplicates disambiguated by appending the visit id).
    ///
    /// The ZIP itself is named:
    ///   "زيارات-{school}-{yyyy-MM-dd}.zip"
    /// (or "زيارات-{date}.zip" for global admins with no active school).
    /// </summary>
    [HttpGet("export/zip")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    public async Task<IActionResult> ExportVisitsZip(
        [FromQuery] int? status = null,
        [FromQuery] string? instructorId = null,
        [FromQuery] int? visitCategory = null,
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default)
    {
        // Visit.View is sufficient — the service enforces scope.
        if (!_currentUser.HasPermission(PermissionNames.VisitView))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لعرض الزيارات."));

        var query = new VisitListQuery
        {
            Status = status,
            InstructorId = instructorId,
            VisitCategory = visitCategory,
            FromDate = fromDate,
            ToDate = toDate
        };

        try
        {
            var result = await _bulkExportService.ExportVisitsZipAsync(query, cancellationToken);
            return File(result.ZipBytes, "application/zip", result.FileName);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse.Fail(ex.Message));
        }
        catch (UnauthorizedSchoolAccessException ex)
        {
            return StatusCode(403, ApiResponse.Fail(ex.Message));
        }
    }
}