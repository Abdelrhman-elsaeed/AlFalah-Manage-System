using AlFalah.Application.Common;
using AlFalah.Application.DTOs.Schools;
using AlFalah.Application.Interfaces;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

/// <summary>
/// Schools CRUD + lifecycle endpoints (Phase 2).
/// </summary>
[ApiController]
[Route("api/v1/schools")]
[Authorize]
public class SchoolsController : ControllerBase
{
    private readonly ISchoolService _schoolService;
    private readonly ICurrentUserService _currentUser;

    public SchoolsController(ISchoolService schoolService, ICurrentUserService currentUser)
    {
        _schoolService = schoolService;
        _currentUser = currentUser;
    }

    // ─── List (paged) ──────────────────────────────────────────────────────

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<SchoolListItemDto>>), 200)]
    public async Task<IActionResult> List([FromQuery] SchoolListQuery query, CancellationToken cancellationToken)
    {
        var errors = await ValidationHelper.ValidateAsync(HttpContext.RequestServices, query, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<PagedResult<SchoolListItemDto>>.Fail(errors));

        var result = await _schoolService.GetPagedAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<SchoolListItemDto>>.Success(result));
    }

    // ─── Detail ────────────────────────────────────────────────────────────

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<SchoolDetailDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _schoolService.GetByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<SchoolDetailDto>.Success(result));
    }

    // ─── Create ────────────────────────────────────────────────────────────

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<SchoolDetailDto>), 201)]
    [ProducesResponseType(typeof(ApiResponse<SchoolDetailDto>), 400)]
    public async Task<IActionResult> Create([FromBody] SchoolCreateRequestDto request, CancellationToken cancellationToken)
    {
        var errors = await ValidationHelper.ValidateAsync(HttpContext.RequestServices, request, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<SchoolDetailDto>.Fail(errors));

        var result = await _schoolService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<SchoolDetailDto>.Success(result, "تم إنشاء المدرسة بنجاح."));
    }

    // ─── Update ────────────────────────────────────────────────────────────

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<SchoolDetailDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<SchoolDetailDto>), 400)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> Update(int id, [FromBody] SchoolUpdateRequestDto request, CancellationToken cancellationToken)
    {
        var errors = await ValidationHelper.ValidateAsync(HttpContext.RequestServices, request, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<SchoolDetailDto>.Fail(errors));

        var result = await _schoolService.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponse<SchoolDetailDto>.Success(result, "تم تحديث المدرسة بنجاح."));
    }

    // ─── Soft delete ───────────────────────────────────────────────────────

    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _schoolService.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse.Success("تم حذف المدرسة بنجاح."));
    }

    // ─── Assign Manager ────────────────────────────────────────────────────

    [HttpPost("{id:int}/assign-manager")]
    [ProducesResponseType(typeof(ApiResponse<SchoolDetailDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<SchoolDetailDto>), 400)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> AssignManager(int id, [FromBody] AssignSchoolManagerRequestDto request, CancellationToken cancellationToken)
    {
        var errors = await ValidationHelper.ValidateAsync(HttpContext.RequestServices, request, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<SchoolDetailDto>.Fail(errors));

        var result = await _schoolService.AssignManagerAsync(id, request, cancellationToken);
        return Ok(ApiResponse<SchoolDetailDto>.Success(result, "تم تعيين مدير المدرسة بنجاح."));
    }

    // ─── Activate / Deactivate ─────────────────────────────────────────────

    [HttpPost("{id:int}/activate")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> Activate(int id, CancellationToken cancellationToken)
    {
        await _schoolService.ActivateAsync(id, cancellationToken);
        return Ok(ApiResponse.Success("تم تفعيل المدرسة بنجاح."));
    }

    [HttpPost("{id:int}/deactivate")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        await _schoolService.DeactivateAsync(id, cancellationToken);
        return Ok(ApiResponse.Success("تم تعطيل المدرسة بنجاح."));
    }
}