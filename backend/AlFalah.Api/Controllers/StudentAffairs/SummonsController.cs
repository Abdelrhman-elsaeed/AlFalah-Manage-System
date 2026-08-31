using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Summons;
using AlFalah.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.StudentAffairs;

[Route("api/v1/summons")]
public sealed class SummonsController : StudentAffairsControllerBase
{
    public SummonsController(IMediator mediator, ICurrentUserService currentUser) : base(mediator, currentUser) { }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSummonRequestDto request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.SummonCreate)) return PermissionDenied();
        var response = await Mediator.Send(new CreateSummonCommand(request, idempotencyKey), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] SummonListQuery query, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.SummonView)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetSummonsQuery(query), cancellationToken));
    }

    [HttpGet("mine")]
    public async Task<IActionResult> Mine([FromQuery] SummonListQuery query, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.GuardianViewLinkedStudents)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetMySummonsQuery(query), cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.SummonView, PermissionNames.GuardianViewLinkedStudents)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetSummonByIdQuery(id), cancellationToken));
    }

    [HttpPost("{id:int}/schedule")]
    public async Task<IActionResult> Schedule(int id, [FromBody] ScheduleSummonRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.SummonSchedule)) return PermissionDenied();
        return Ok(await Mediator.Send(new ScheduleSummonCommand(id, request), cancellationToken));
    }

    [HttpPost("{id:int}/attend")]
    public async Task<IActionResult> Attend(int id, [FromBody] AttendSummonRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.SummonMarkAttended)) return PermissionDenied();
        return Ok(await Mediator.Send(new AttendSummonCommand(id, request), cancellationToken));
    }

    [HttpPost("{id:int}/start-observation")]
    public async Task<IActionResult> StartObservation(int id, [FromBody] StartSummonObservationRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.SummonStartObservation)) return PermissionDenied();
        return Ok(await Mediator.Send(new StartSummonObservationCommand(id, request), cancellationToken));
    }

    [HttpPost("{id:int}/mark-improved")]
    public async Task<IActionResult> MarkImproved(int id, [FromBody] MarkSummonImprovedRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.SummonMarkImproved)) return PermissionDenied();
        return Ok(await Mediator.Send(new MarkSummonImprovedCommand(id, request), cancellationToken));
    }

    [HttpPost("{id:int}/automation-impact-review")]
    public async Task<IActionResult> ReviewAutomationImpact(int id, [FromBody] ReviewSummonAutomationImpactRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.SummonReviewAutomationImpact)) return PermissionDenied();
        return Ok(await Mediator.Send(new ReviewSummonAutomationImpactCommand(id, request), cancellationToken));
    }

    [HttpGet("{id:int}/history")]
    public async Task<IActionResult> History(int id, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.SummonViewHistory)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetSummonHistoryQuery(id), cancellationToken));
    }
}
