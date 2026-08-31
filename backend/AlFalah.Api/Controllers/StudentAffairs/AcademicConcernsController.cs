using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Behaviors;
using AlFalah.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.StudentAffairs;

[Route("api/v1/academic-concerns")]
public sealed class AcademicConcernsController : StudentAffairsControllerBase
{
    public AcademicConcernsController(IMediator mediator, ICurrentUserService currentUser) : base(mediator, currentUser) { }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAcademicConcernRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.AcademicConcernCreate)) return PermissionDenied();
        var response = await Mediator.Send(new CreateAcademicConcernCommand(request), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AcademicConcernListQuery query, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.AcademicConcernView)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetAcademicConcernsQuery(query), cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.AcademicConcernView)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetAcademicConcernByIdQuery(id), cancellationToken));
    }

    [HttpPost("{id:int}/dispatch-decision")]
    public async Task<IActionResult> DispatchDecision(int id, [FromBody] DispatchDecisionRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.AcademicConcernManage)) return PermissionDenied();
        return Ok(await Mediator.Send(new DecideAcademicConcernDispatchCommand(id, request), cancellationToken));
    }

    [HttpPost("{id:int}/correct")]
    public async Task<IActionResult> Correct(int id, [FromBody] CorrectAcademicConcernRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.AcademicConcernManage)) return PermissionDenied();
        return Ok(await Mediator.Send(new CorrectAcademicConcernCommand(id, request), cancellationToken));
    }
}
