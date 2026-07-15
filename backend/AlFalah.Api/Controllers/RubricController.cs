using AlFalah.Application.Common;
using AlFalah.Application.DTOs.Rubric;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

/// <summary>
/// Rubric management endpoints (Phase 3).
/// Rubric is GLOBAL — not school-scoped (D-21).
/// Permissions: Rubric.View for reads; Rubric.Manage for writes.
/// </summary>
[ApiController]
[Route("api/v1/rubric")]
[Authorize(Roles = RoleNames.SuperAdmin + "," + RoleNames.MainManager)]
public class RubricController : ControllerBase
{
    private readonly IRubricService _rubricService;
    private readonly ICurrentUserService _currentUser;

    public RubricController(IRubricService rubricService, ICurrentUserService currentUser)
    {
        _rubricService = rubricService;
        _currentUser = currentUser;
    }

    // ─── GET active version ────────────────────────────────────────────────────

    /// <summary>Returns the full tree (domains + standards) for the active rubric version.</summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(ApiResponse<RubricVersionDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.RubricView))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لعرض أداة التقييم."));

        var result = await _rubricService.GetActiveVersionAsync(cancellationToken);
        return Ok(ApiResponse<RubricVersionDto>.Success(result));
    }

    // ─── GET versions list ─────────────────────────────────────────────────────

    /// <summary>Returns the list of all rubric versions (lightweight, no standards inline).</summary>
    [HttpGet("versions")]
    [ProducesResponseType(typeof(ApiResponse<List<RubricVersionListDto>>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    public async Task<IActionResult> GetVersions(CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.RubricView))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لعرض أداة التقييم."));

        var result = await _rubricService.GetVersionsAsync(cancellationToken);
        return Ok(ApiResponse<List<RubricVersionListDto>>.Success(result));
    }

    // ─── GET version by id ─────────────────────────────────────────────────────

    /// <summary>Returns the full tree for a specific rubric version.</summary>
    [HttpGet("versions/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<RubricVersionDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> GetVersionById(int id, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.RubricView))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لعرض أداة التقييم."));

        var result = await _rubricService.GetVersionByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<RubricVersionDto>.Success(result));
    }

    // ─── POST create new version ───────────────────────────────────────────────

    /// <summary>
    /// Creates a new rubric version from the provided tree (copy-on-write).
    /// Deactivates the previous active version; historical data is preserved.
    /// </summary>
    [HttpPost("versions")]
    [ProducesResponseType(typeof(ApiResponse<RubricVersionDto>), 201)]
    [ProducesResponseType(typeof(ApiResponse<RubricVersionDto>), 400)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    public async Task<IActionResult> CreateVersion(
        [FromBody] CreateRubricVersionDto request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.RubricManage))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لإدارة أداة التقييم."));

        var errors = await ValidationHelper.ValidateAsync(HttpContext.RequestServices, request, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<RubricVersionDto>.Fail(errors));

        var result = await _rubricService.CreateNewVersionAsync(request, cancellationToken);
        return CreatedAtAction(
            nameof(GetVersionById),
            new { id = result.Id },
            ApiResponse<RubricVersionDto>.Success(result, "تم إنشاء إصدار جديد من أداة التقييم بنجاح."));
    }

    // ─── POST activate version ─────────────────────────────────────────────────

    /// <summary>Activates a specific rubric version and deactivates all others.</summary>
    [HttpPost("versions/{id:int}/activate")]
    [ProducesResponseType(typeof(ApiResponse<RubricVersionDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> ActivateVersion(int id, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.RubricManage))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لإدارة أداة التقييم."));

        var result = await _rubricService.ActivateVersionAsync(id, cancellationToken);
        return Ok(ApiResponse<RubricVersionDto>.Success(result, "تم تفعيل إصدار أداة التقييم بنجاح."));
    }

    // ─── GET score scale ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns the global score scale (0–4 labels + performance-level thresholds).
    /// Values are verbatim from docs/09-RUBRIC-AND-EVALUATION.md.
    /// Phase 4 MUST use this endpoint as the source of truth for analysis.
    /// </summary>
    [HttpGet("score-scale")]
    [ProducesResponseType(typeof(ApiResponse<ScoreScaleDto>), 200)]
    public IActionResult GetScoreScale()
    {
        var result = _rubricService.GetScoreScale();
        return Ok(ApiResponse<ScoreScaleDto>.Success(result));
    }
}
