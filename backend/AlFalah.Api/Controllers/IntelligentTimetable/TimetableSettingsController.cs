using AlFalah.Application.Common;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.IntelligentTimetable.Handlers;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.IntelligentTimetable;

[ApiController]
[Authorize]
[Route("api/v1/intelligent-timetable/settings")]
public sealed class TimetableSettingsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public TimetableSettingsController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int? academicYearId,
        [FromQuery] TimetableSemester? semester,
        [FromQuery] int? profileId,
        CancellationToken cancellationToken)
    {
        if (!CanView()) return PermissionDenied();
        var response = await _mediator.Send(
            new GetTimetableSettingsQuery(academicYearId, semester, profileId),
            cancellationToken);
        return response.IsSuccess ? Ok(response) : Failure(response);
    }

    [HttpPost("profiles")]
    public async Task<IActionResult> Create(
        [FromBody] CreateTimetableSetupProfileRequest request,
        CancellationToken cancellationToken)
    {
        var errors = await ValidationHelper.ValidateAsync(HttpContext.RequestServices, request, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<TimetableSetupProfileDto>.Fail(errors));

        var response = await _mediator.Send(new CreateTimetableSetupProfileCommand(request), cancellationToken);
        return response.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, response)
            : Failure(response);
    }

    [HttpPut("profiles/{profileId:int}")]
    public async Task<IActionResult> Update(
        int profileId,
        [FromBody] UpdateTimetableSetupProfileRequest request,
        CancellationToken cancellationToken)
    {
        var errors = await ValidationHelper.ValidateAsync(HttpContext.RequestServices, request, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<TimetableSetupProfileDto>.Fail(errors));

        var response = await _mediator.Send(
            new UpdateTimetableSetupProfileCommand(profileId, request),
            cancellationToken);
        return response.IsSuccess ? Ok(response) : Failure(response);
    }

    private bool CanView() =>
        !_currentUser.IsInRole(RoleNames.Instructor)
        && !_currentUser.IsInRole(RoleNames.Guardian)
        && (_currentUser.HasPermission(PermissionNames.TimetableView)
            || _currentUser.HasPermission(PermissionNames.TimetableManage));

    private IActionResult PermissionDenied() =>
        StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(TimetableSettingsHandlerSupport.PermissionDenied));

    private IActionResult Failure<T>(ApiResponse<T> response)
    {
        var error = response.Errors.FirstOrDefault() ?? response.Message;
        if (error == TimetableSettingsHandlerSupport.PermissionDenied)
            return StatusCode(StatusCodes.Status403Forbidden, response);
        if (error == TimetableSettingsHandlerSupport.NotFound)
            return NotFound(response);
        if (error == TimetableSettingsHandlerSupport.ConcurrencyConflict)
            return Conflict(response);
        return BadRequest(response);
    }
}
