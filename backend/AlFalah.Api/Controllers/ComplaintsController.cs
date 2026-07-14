using AlFalah.Application.Common;
using AlFalah.Application.DTOs.Complaints;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

/// <summary>
/// Phase 8 — Complaints. Thin controller; ALL visibility rules (including the
/// Main-Manager hard block) live in <see cref="IComplaintService"/> — the
/// permission gates here are defense-in-depth only (MainManager has NO
/// Complaint.* permission seeded, AND the service 403s him even if one leaks).
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public class ComplaintsController : ControllerBase
{
    private readonly IComplaintService _complaintService;
    private readonly ICurrentUserService _currentUser;

    public ComplaintsController(IComplaintService complaintService, ICurrentUserService currentUser)
    {
        _complaintService = complaintService;
        _currentUser = currentUser;
    }

    // ─── POST submit complaint (Instructor, own approved + viewed visit) ────

    [HttpPost("visits/{visitId:int}/complaints")]
    [ProducesResponseType(typeof(ApiResponse<ComplaintDto>), 201)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> Create(int visitId, [FromBody] CreateComplaintRequestDto request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.ComplaintCreate))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لتقديم شكوى."));

        var errors = await ValidationHelper.ValidateAsync(HttpContext.RequestServices, request, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<ComplaintDto>.Fail(errors));

        var result = await _complaintService.CreateAsync(visitId, request, cancellationToken);
        return StatusCode(201, ApiResponse<ComplaintDto>.Success(result, "تم إرسال الشكوى بنجاح."));
    }

    // ─── GET scoped list (MainManager blocked in service) ──────────────────

    [HttpGet("complaints")]
    [ProducesResponseType(typeof(ApiResponse<List<ComplaintDto>>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    public async Task<IActionResult> List([FromQuery] int? status, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.ComplaintView))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لعرض الشكاوى."));

        var result = await _complaintService.ListAsync(status, cancellationToken);
        return Ok(ApiResponse<List<ComplaintDto>>.Success(result));
    }

    // ─── GET scoped detail (MainManager blocked in service) ────────────────

    [HttpGet("complaints/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ComplaintDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.ComplaintView))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لعرض الشكاوى."));

        var result = await _complaintService.GetByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<ComplaintDto>.Success(result));
    }

    // ─── PUT status change (SM / SuperAdmin) ──────────────────────────────

    [HttpPut("complaints/{id:int}/status")]
    [ProducesResponseType(typeof(ApiResponse<ComplaintDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateComplaintStatusRequestDto request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.ComplaintManage))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لمعالجة الشكاوى."));

        var errors = await ValidationHelper.ValidateAsync(HttpContext.RequestServices, request, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<ComplaintDto>.Fail(errors));

        var result = await _complaintService.UpdateStatusAsync(id, request, cancellationToken);
        return Ok(ApiResponse<ComplaintDto>.Success(result, "تم تحديث حالة الشكوى بنجاح."));
    }

    // ─── POST reopen linked visit (SM / SuperAdmin — Phase 5 reopen reuse) ──

    [HttpPost("complaints/{id:int}/reopen-visit")]
    [ProducesResponseType(typeof(ApiResponse<ComplaintDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> ReopenVisit(int id, [FromBody] ReopenVisitFromComplaintRequestDto request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.ComplaintManage)
            || !_currentUser.HasPermission(PermissionNames.VisitReopen))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لإعادة فتح الزيارة من الشكوى."));

        var errors = await ValidationHelper.ValidateAsync(HttpContext.RequestServices, request, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<ComplaintDto>.Fail(errors));

        var result = await _complaintService.ReopenVisitAsync(id, request, cancellationToken);
        return Ok(ApiResponse<ComplaintDto>.Success(result, "تمت إعادة فتح الزيارة المرتبطة بالشكوى بنجاح."));
    }

    // ─── DELETE soft delete (SM / SuperAdmin) ─────────────────────────────

    [HttpDelete("complaints/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> SoftDelete(int id, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.ComplaintDelete))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لحذف الشكاوى."));

        await _complaintService.SoftDeleteAsync(id, cancellationToken);
        return Ok(ApiResponse.Success("تم حذف الشكوى بنجاح."));
    }
}
