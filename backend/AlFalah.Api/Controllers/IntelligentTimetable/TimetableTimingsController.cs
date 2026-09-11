using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.IntelligentTimetable.Handlers;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.IntelligentTimetable;

[ApiController]
[Authorize]
[Route("api/v1/intelligent-timetable/timings")]
public sealed class TimetableTimingsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int academicYearId, [FromQuery] TimetableSemester semester, CancellationToken ct) =>
        Respond(await mediator.Send(new GetBellSchedulesQuery(academicYearId, semester), ct));
    [HttpPost]
    public async Task<IActionResult> Create(SaveBellScheduleRequest request, CancellationToken ct) =>
        Respond(await mediator.Send(new SaveBellScheduleCommand(null, request), ct), 201);
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SaveBellScheduleRequest request, CancellationToken ct) =>
        Respond(await mediator.Send(new SaveBellScheduleCommand(id, request), ct));
    [HttpPut("profiles/{profileId:int}/selection")]
    public async Task<IActionResult> Select(int profileId, SelectBellScheduleRequest request, CancellationToken ct) =>
        Respond(await mediator.Send(new SelectBellScheduleCommand(profileId, request), ct));

    private IActionResult Respond<T>(ApiResponse<T> response, int success = 200)
    {
        if (response.IsSuccess) return StatusCode(success, response);
        var message = response.Errors.FirstOrDefault() ?? response.Message;
        return StatusCode(message switch {
            TimetableSettingsHandlerSupport.PermissionDenied => 403,
            TimetableSettingsHandlerSupport.NotFound => 404,
            TimetableSettingsHandlerSupport.ConcurrencyConflict => 409,
            _ => 400
        }, response);
    }
}
