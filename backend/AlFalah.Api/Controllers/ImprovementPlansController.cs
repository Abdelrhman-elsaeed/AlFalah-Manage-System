using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.Common;
using AlFalah.Application.DTOs.ImprovementPlans;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public class ImprovementPlansController : ControllerBase
{
    private readonly IImprovementPlanService _planService;
    private readonly ICurrentUserService _currentUser;

    public ImprovementPlansController(
        IImprovementPlanService planService,
        ICurrentUserService currentUser)
    {
        _planService = planService;
        _currentUser = currentUser;
    }

    // ─── GET Plans for Visit ──────────────────────────────────────────────────

    [HttpGet("visits/{visitId:int}/improvement-plans")]
    [ProducesResponseType(typeof(ApiResponse<List<ImprovementPlanDto>>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    public async Task<IActionResult> GetPlansForVisit(int visitId, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.PlanView))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لعرض خطط التحسين."));

        var result = await _planService.GetPlansForVisitAsync(visitId, cancellationToken);
        return Ok(ApiResponse<List<ImprovementPlanDto>>.Success(result));
    }

    // ─── GET Weak Domains Suggestions ────────────────────────────────────────

    [HttpGet("visits/{visitId:int}/weak-domains-suggestions")]
    [ProducesResponseType(typeof(ApiResponse<List<WeakDomainSuggestionDto>>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    public async Task<IActionResult> GetWeakDomainSuggestions(int visitId, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.PlanView))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لعرض خطط التحسين."));

        var result = await _planService.GetWeakDomainSuggestionsAsync(visitId, cancellationToken);
        return Ok(ApiResponse<List<WeakDomainSuggestionDto>>.Success(result));
    }

    // ─── GET Plan By ID ──────────────────────────────────────────────────────

    [HttpGet("improvement-plans/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ImprovementPlanDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> GetPlanById(int id, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.PlanView))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لعرض خطط التحسين."));

        var result = await _planService.GetPlanByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<ImprovementPlanDto>.Success(result));
    }

    // ─── POST Create Plan ────────────────────────────────────────────────────

    [HttpPost("improvement-plans")]
    [ProducesResponseType(typeof(ApiResponse<ImprovementPlanDto>), 201)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    public async Task<IActionResult> CreatePlan([FromBody] CreatePlanRequestDto request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.PlanCreate))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لإنشاء خطة تحسين."));

        var errors = await ValidationHelper.ValidateAsync(HttpContext.RequestServices, request, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<ImprovementPlanDto>.Fail(errors));

        var result = await _planService.CreatePlanAsync(request, cancellationToken);
        return StatusCode(201, ApiResponse<ImprovementPlanDto>.Success(result, "تم إنشاء خطة التحسين بنجاح."));
    }

    // ─── PUT Edit Plan ───────────────────────────────────────────────────────

    [HttpPut("improvement-plans/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ImprovementPlanDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> UpdatePlan(int id, [FromBody] UpdatePlanRequestDto request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.PlanEdit))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لتعديل خطط التحسين."));

        var errors = await ValidationHelper.ValidateAsync(HttpContext.RequestServices, request, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<ImprovementPlanDto>.Fail(errors));

        var result = await _planService.UpdatePlanAsync(id, request, cancellationToken);
        return Ok(ApiResponse<ImprovementPlanDto>.Success(result, "تم تحديث خطة التحسين بنجاح."));
    }

    // ─── DELETE Soft Delete Plan ─────────────────────────────────────────────

    [HttpDelete("improvement-plans/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> SoftDeletePlan(int id, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.PlanDelete))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لحذف خطط التحسين."));

        await _planService.SoftDeletePlanAsync(id, cancellationToken);
        return Ok(ApiResponse.Success("تم حذف خطة التحسين بنجاح."));
    }

    // ─── POST Add Follow-up ──────────────────────────────────────────────────

    [HttpPost("improvement-plans/{id:int}/follow-ups")]
    [ProducesResponseType(typeof(ApiResponse<PlanFollowUpDto>), 201)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    public async Task<IActionResult> AddFollowUp(int id, [FromBody] CreateFollowUpRequestDto request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.PlanEdit))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لإضافة متابعة."));

        var errors = await ValidationHelper.ValidateAsync(HttpContext.RequestServices, request, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<PlanFollowUpDto>.Fail(errors));

        var result = await _planService.AddFollowUpAsync(id, request, cancellationToken);
        return StatusCode(201, ApiResponse<PlanFollowUpDto>.Success(result, "تم إضافة المتابعة بنجاح."));
    }

    // ─── PUT Edit Follow-up ──────────────────────────────────────────────────

    [HttpPut("follow-ups/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<PlanFollowUpDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> UpdateFollowUp(int id, [FromBody] UpdateFollowUpRequestDto request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.PlanEdit))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لتعديل المتابعة."));

        var errors = await ValidationHelper.ValidateAsync(HttpContext.RequestServices, request, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<PlanFollowUpDto>.Fail(errors));

        var result = await _planService.UpdateFollowUpAsync(id, request, cancellationToken);
        return Ok(ApiResponse<PlanFollowUpDto>.Success(result, "تم تحديث المتابعة بنجاح."));
    }

    // ─── DELETE Soft Delete Follow-up ────────────────────────────────────────

    [HttpDelete("follow-ups/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> SoftDeleteFollowUp(int id, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.PlanDelete))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لحذف المتابعة."));

        await _planService.SoftDeleteFollowUpAsync(id, cancellationToken);
        return Ok(ApiResponse.Success("تم حذف المتابعة بنجاح."));
    }

    // ─── GET Plan Progress ───────────────────────────────────────────────────

    [HttpGet("improvement-plans/{id:int}/progress")]
    [ProducesResponseType(typeof(ApiResponse<PlanProgressDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> GetPlanProgress(int id, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.PlanView))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لعرض تقدم الخطة."));

        var result = await _planService.GetPlanProgressAsync(id, cancellationToken);
        return Ok(ApiResponse<PlanProgressDto>.Success(result));
    }
}
