using AlFalah.Application.Common;
using AlFalah.Application.DTOs.Account;
using AlFalah.Application.DTOs.Teachers;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize] // Must be logged in
public class AccountController : ControllerBase
{
    private readonly IAccountService _accountService;
    private readonly ITeacherService _teacherService;
    private readonly ICurrentUserService _currentUser;

    public AccountController(
        IAccountService accountService,
        ITeacherService teacherService,
        ICurrentUserService currentUser)
    {
        _accountService = accountService;
        _teacherService = teacherService;
        _currentUser = currentUser;
    }

    [HttpGet("signature")]
    public async Task<ActionResult<ApiResponse<SignatureDto>>> GetSignature(CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<SignatureDto>.Fail("User not found in context"));

        var response = await _accountService.GetSignatureAsync(userId, cancellationToken);
        return Ok(response);
    }

    [HttpPut("signature")]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateSignature([FromBody] SignatureDto dto, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<bool>.Fail("User not found in context"));

        var response = await _accountService.UpdateSignatureAsync(userId, dto, cancellationToken);
        if (!response.IsSuccess)
            return BadRequest(response);

        return Ok(response);
    }

    // ─── D-74 — Self-only teaching info (Subject + Classes) for the
    //             "مادتي وفصولي" section in account settings. ──────────────
    //
    // Both endpoints are HARD-SELF: a teacher can only read/edit their own
    // teaching info. The manager path is /api/v1/teachers/{userId}/teaching.

    [HttpGet("teaching")]
    [ProducesResponseType(typeof(ApiResponse<TeacherTeachingDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    public async Task<IActionResult> GetMyTeaching(CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<TeacherTeachingDto>.Fail("يجب تسجيل الدخول."));

        // SELF-ONLY — refuse up-front if the caller tries to read anyone else
        // through this endpoint (the manager path is the only one that accepts
        // a different userId).
        var result = await _teacherService.GetTeachingAsync(userId, cancellationToken);
        return Ok(ApiResponse<TeacherTeachingDto>.Success(result));
    }

    [HttpPut("teaching")]
    [ProducesResponseType(typeof(ApiResponse<TeacherTeachingDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    public async Task<IActionResult> UpdateMyTeaching(
        [FromBody] TeacherTeachingUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<TeacherTeachingDto>.Fail("يجب تسجيل الدخول."));

        var errors = await ValidationHelper.ValidateAsync(
            HttpContext.RequestServices, request, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<TeacherTeachingDto>.Fail(errors));

        try
        {
            var result = await _teacherService.UpsertTeachingAsync(userId, request, cancellationToken);
            return Ok(ApiResponse<TeacherTeachingDto>.Success(result, "تم حفظ المادة والفصول بنجاح."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<TeacherTeachingDto>.Fail(ex.Message));
        }
        catch (UnauthorizedSchoolAccessException ex)
        {
            return StatusCode(403, ApiResponse<TeacherTeachingDto>.Fail(ex.Message));
        }
    }
}
