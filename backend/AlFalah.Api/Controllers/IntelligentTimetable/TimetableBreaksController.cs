using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.IntelligentTimetable.Handlers;
using AlFalah.Shared.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.IntelligentTimetable;

[ApiController]
[Authorize]
[Route("api/v1/intelligent-timetable/timings/{templateId:int}/breaks")]
public sealed class TimetableBreaksController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(int templateId, CancellationToken ct) =>
        Respond(await mediator.Send(new GetScheduleBreaksQuery(templateId), ct));

    // Timetable.Manage / scoped editor grants are enforced in the command handler, as for timings.
    [HttpPut]
    public async Task<IActionResult> Save(int templateId, SaveScheduleBreaksRequest request, CancellationToken ct) =>
        Respond(await mediator.Send(new SaveScheduleBreaksCommand(templateId, request), ct));

    [HttpGet("effective/{day:int}")]
    public async Task<IActionResult> Effective(int templateId, int day, CancellationToken ct) =>
        Respond(await mediator.Send(new GetEffectiveScheduleQuery(templateId, day), ct));

    private IActionResult Respond<T>(ApiResponse<T> response) => response.IsSuccess ? Ok(response) : StatusCode(
        (response.Errors.FirstOrDefault() ?? response.Message) switch {
            TimetableSettingsHandlerSupport.PermissionDenied => 403,
            TimetableSettingsHandlerSupport.NotFound => 404,
            TimetableSettingsHandlerSupport.ConcurrencyConflict => 409,
            _ => 400
        }, response);
}
