using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.IntelligentTimetable.Handlers;
using AlFalah.Shared.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.IntelligentTimetable;

[ApiController, Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/v1/intelligent-timetable/review")]
public sealed class TimetableReviewController(IMediator mediator) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => Respond(await mediator.Send(new GetReviewTimetablesQuery(), ct));
    [HttpGet("{timetableId:int}/evaluate")] public async Task<IActionResult> Evaluate(int timetableId, CancellationToken ct) => Respond(await mediator.Send(new EvaluateTimetableQuery(timetableId), ct));
    [HttpGet("{timetableId:int}/findings/{findingId:int}/repairs")] public async Task<IActionResult> Repairs(int timetableId, int findingId, CancellationToken ct) =>
        Respond(await mediator.Send(new GetRepairProposalsQuery(timetableId, findingId), ct));
    [HttpPost("findings/{findingId:int}/override")] public async Task<IActionResult> Override(int findingId, OverrideSoftViolationRequest request, CancellationToken ct) =>
        Respond(await mediator.Send(new OverrideSoftViolationCommand(findingId, request.Reason), ct));
    [HttpPost("{timetableId:int}/apply-repair")] public async Task<IActionResult> Apply(int timetableId, RepairProposalDto proposal, CancellationToken ct) =>
        Respond(await mediator.Send(new ApplyRepairProposalCommand(timetableId, proposal), ct));
    private IActionResult Respond<T>(ApiResponse<T> response) => response.IsSuccess ? Ok(response) : StatusCode(
        (response.Errors.FirstOrDefault() ?? response.Message) switch {
            TimetableSettingsHandlerSupport.PermissionDenied => 403, TimetableSettingsHandlerSupport.NotFound => 404,
            TimetableSettingsHandlerSupport.ConcurrencyConflict => 409, _ => 400 }, response);
}
