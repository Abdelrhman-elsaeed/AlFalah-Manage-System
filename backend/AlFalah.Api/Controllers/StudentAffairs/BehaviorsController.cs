using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Behaviors;
using AlFalah.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.StudentAffairs;

[Route("api/v1/behaviors")]
public sealed class BehaviorsController : StudentAffairsControllerBase
{
    public BehaviorsController(IMediator mediator, ICurrentUserService currentUser) : base(mediator, currentUser) { }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBehaviorIncidentRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.BehaviorCreate)) return PermissionDenied();
        var response = await Mediator.Send(new CreateBehaviorIncidentCommand(request), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] BehaviorListQuery query, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.BehaviorView)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetBehaviorIncidentsQuery(query), cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.BehaviorView)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetBehaviorIncidentByIdQuery(id), cancellationToken));
    }

    [HttpPost("{id:int}/classify")]
    public async Task<IActionResult> Classify(int id, [FromBody] ClassifyBehaviorRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.BehaviorManage)) return PermissionDenied();
        return Ok(await Mediator.Send(new ClassifyBehaviorIncidentCommand(id, request), cancellationToken));
    }

    [HttpPost("{id:int}/dispatch-decision")]
    public async Task<IActionResult> DispatchDecision(int id, [FromBody] DispatchDecisionRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.BehaviorManage)) return PermissionDenied();
        return Ok(await Mediator.Send(new DecideBehaviorDispatchCommand(id, request), cancellationToken));
    }

    [HttpPost("{id:int}/refer")]
    public async Task<IActionResult> Refer(int id, [FromBody] ReferBehaviorRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.ReferralCreate)) return PermissionDenied();
        return Ok(await Mediator.Send(new ReferBehaviorIncidentCommand(id, request), cancellationToken));
    }

    [HttpPost("{id:int}/correct")]
    public async Task<IActionResult> Correct(int id, [FromBody] CorrectBehaviorRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.BehaviorManage)) return PermissionDenied();
        return Ok(await Mediator.Send(new CorrectBehaviorIncidentCommand(id, request), cancellationToken));
    }
}
