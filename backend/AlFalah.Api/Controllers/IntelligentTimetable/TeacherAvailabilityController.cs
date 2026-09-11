using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.IntelligentTimetable.Handlers;
using AlFalah.Shared.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.IntelligentTimetable;

[ApiController]
[Authorize]
[Route("api/v1/intelligent-timetable/settings/profiles/{setupId:int}/teachers")]
public sealed class TeacherAvailabilityController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(int setupId, CancellationToken ct) =>
        Respond(await mediator.Send(new GetAvailabilityTeachersQuery(setupId), ct));
    [HttpGet("{teacherId:int}")]
    public async Task<IActionResult> Get(int setupId, int teacherId, CancellationToken ct) =>
        Respond(await mediator.Send(new GetTeacherAvailabilityQuery(setupId, teacherId), ct));
    [HttpPut("{teacherId:int}")]
    public async Task<IActionResult> Update(int setupId, int teacherId, UpdateTeacherProfileRequest request, CancellationToken ct) =>
        Respond(await mediator.Send(new UpdateTeacherProfileCommand(setupId, teacherId, request), ct));

    private IActionResult Respond<T>(ApiResponse<T> response) => response.IsSuccess ? Ok(response) : StatusCode(
        (response.Errors.FirstOrDefault() ?? response.Message) switch {
            TimetableSettingsHandlerSupport.PermissionDenied => 403,
            TimetableSettingsHandlerSupport.NotFound => 404,
            TimetableSettingsHandlerSupport.ConcurrencyConflict => 409,
            _ => 400
        }, response);
}
