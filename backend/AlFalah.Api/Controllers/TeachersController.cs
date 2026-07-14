using AlFalah.Application.Common;
using AlFalah.Application.DTOs.Teachers;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

/// <summary>
/// D-71 — Teachers Management + Teacher Profile (additive, no new phase).
///
/// Routes mirror the desktop reference's two screens:
///  - GET    /api/v1/teachers                  → list (إدارة المعلمين)         — Users.View
///  - GET    /api/v1/teachers/{userId}         → profile header                — Users.View
///  - GET    /api/v1/teachers/{userId}/visits  → profile visits table (D-37)   — Visits.View
///  - GET    /api/v1/teachers/{userId}/progress → profile radar (D-37)         — Visits.View
///
/// Create / update / soft-delete a teacher REUSE the existing
/// <c>POST /api/v1/users</c> + <c>PUT /api/v1/users/{id}</c> +
/// <c>POST /api/v1/users/{id}/deactivate</c> endpoints with role=Instructor —
/// no parallel Teacher write endpoints are introduced.
///
/// School-scoping + D-37 are enforced inside <see cref="TeacherService"/>;
/// cross-school → <see cref="UnauthorizedSchoolAccessException"/> → 403.
/// MainManager is NOT given any Complaint.* permission and this feature does
/// not surface complaints either — D-24/D-28/D-36/D-37 + MainManager-no-
/// complaints stay intact.
/// </summary>
[ApiController]
[Route("api/v1/teachers")]
[Authorize]
public class TeachersController : ControllerBase
{
    private readonly ITeacherService _teacherService;
    private readonly ICurrentUserService _currentUser;

    public TeachersController(ITeacherService teacherService, ICurrentUserService currentUser)
    {
        _teacherService = teacherService;
        _currentUser = currentUser;
    }

    // ─── GET list ─────────────────────────────────────────────────────────────

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<TeacherListItemDto>>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.HasPermission(PermissionNames.UserView))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لعرض المعلمين."));

        var query = new TeacherListQuery
        {
            Page = page,
            PageSize = pageSize,
            Search = search
        };

        var errors = await ValidationHelper.ValidateAsync(HttpContext.RequestServices, query, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<PagedResult<TeacherListItemDto>>.Fail(errors));

        var result = await _teacherService.ListAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<TeacherListItemDto>>.Success(result));
    }

    // ─── GET profile header ──────────────────────────────────────────────────

    [HttpGet("{userId}")]
    [ProducesResponseType(typeof(ApiResponse<TeacherProfileDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> GetProfile(string userId, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.UserView))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لعرض ملف المعلم."));

        try
        {
            var result = await _teacherService.GetProfileAsync(userId, cancellationToken);
            return Ok(ApiResponse<TeacherProfileDto>.Success(result));
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

    // ─── GET profile visits ──────────────────────────────────────────────────

    [HttpGet("{userId}/visits")]
    [ProducesResponseType(typeof(ApiResponse<List<TeacherVisitSummaryDto>>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> GetVisits(string userId, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.VisitView))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لعرض زيارات المعلم."));

        try
        {
            var result = await _teacherService.GetVisitsAsync(userId, cancellationToken);
            return Ok(ApiResponse<List<TeacherVisitSummaryDto>>.Success(result));
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

    // ─── GET profile progress (radar) ────────────────────────────────────────

    [HttpGet("{userId}/progress")]
    [ProducesResponseType(typeof(ApiResponse<TeacherProgressDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> GetProgress(string userId, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.VisitView))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لعرض بيانات تقدم المعلم."));

        try
        {
            var result = await _teacherService.GetProgressAsync(userId, cancellationToken);
            return Ok(ApiResponse<TeacherProgressDto>.Success(result));
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

    // ─── D-74 — Manager-scoped teaching info (Subject + Classes) ────────────
    //
    // GET  /api/v1/teachers/{userId}/teaching   → in-scope teacher's teaching info
    // PUT  /api/v1/teachers/{userId}/teaching   → manager sets a teacher's
    //                                              Subject + Classes (Users.Edit scoped)
    //
    // Self-only edits live at /api/v1/account/teaching — see AccountController.

    [HttpGet("{userId}/teaching")]
    [ProducesResponseType(typeof(ApiResponse<TeacherTeachingDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> GetTeaching(string userId, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.UserView))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لعرض بيانات المعلم."));

        try
        {
            var result = await _teacherService.GetTeachingAsync(userId, cancellationToken);
            return Ok(ApiResponse<TeacherTeachingDto>.Success(result));
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

    [HttpPut("{userId}/teaching")]
    [ProducesResponseType(typeof(ApiResponse<TeacherTeachingDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> UpdateTeaching(
        string userId,
        [FromBody] TeacherTeachingUpsertRequest request,
        CancellationToken cancellationToken)
    {
        // Manager permission gate — matches the docs/03 users-edit permission
        // (only roles allowed to edit users can set a teacher's teaching info).
        if (!_currentUser.HasPermission(PermissionNames.UserEdit))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لتعديل بيانات المعلمين."));

        var errors = await ValidationHelper.ValidateAsync(
            HttpContext.RequestServices, request, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<TeacherTeachingDto>.Fail(errors));

        try
        {
            var result = await _teacherService.UpsertTeachingAsync(userId, request, cancellationToken);
            return Ok(ApiResponse<TeacherTeachingDto>.Success(result, "تم حفظ المادة والفصول للمعلم."));
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
}