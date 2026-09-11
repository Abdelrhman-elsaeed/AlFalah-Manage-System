using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.IntelligentTimetable.Handlers;
using AlFalah.Shared.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.IntelligentTimetable;

[ApiController, Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/v1/intelligent-timetable/substitutions")]
public sealed class TimetableSubstitutionsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Respond(await mediator.Send(new GetSubstitutionTimetablesQuery(), ct));
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Daily(int id, [FromQuery] DateOnly date, CancellationToken ct) => Respond(await mediator.Send(new GetDailySubstitutionsQuery(id, date), ct));
    [HttpGet("{id:int}/candidates")]
    public async Task<IActionResult> Candidates(int id, [FromQuery] DateOnly date, [FromQuery] int sourceEntryId, [FromQuery] string mode, CancellationToken ct) =>
        Respond(await mediator.Send(new GetSwapCandidatesQuery(id, date, sourceEntryId, mode), ct));
    [HttpPost("{id:int}/execute")]
    public async Task<IActionResult> Execute(int id, ExecuteSwapRequest request, CancellationToken ct) => Respond(await mediator.Send(new ExecuteSwapCommand(id, request), ct));
    private IActionResult Respond<T>(ApiResponse<T> response) => response.IsSuccess ? Ok(response) : StatusCode(
        (response.Errors.FirstOrDefault() ?? response.Message) switch {
            TimetableSettingsHandlerSupport.PermissionDenied => 403, TimetableSettingsHandlerSupport.NotFound => 404,
            TimetableSettingsHandlerSupport.ConcurrencyConflict => 409, _ => 400 }, response);
}
