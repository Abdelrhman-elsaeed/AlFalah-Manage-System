using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Permits;
using AlFalah.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.StudentAffairs;

[Route("api/v1/classroom-entry-permits")]
public sealed class ClassroomEntryPermitsController : StudentAffairsControllerBase
{
    public ClassroomEntryPermitsController(IMediator mediator, ICurrentUserService currentUser) : base(mediator, currentUser) { }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClassroomEntryPermitRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.ClassroomEntryPermitIssue)) return PermissionDenied();
        var response = await Mediator.Send(new CreateClassroomEntryPermitCommand(request), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ClassroomEntryPermitListQuery query, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.ClassroomEntryPermitView)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetClassroomEntryPermitsQuery(query), cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.ClassroomEntryPermitView)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetClassroomEntryPermitByIdQuery(id), cancellationToken));
    }

    [HttpPost("{id:int}/acknowledge")]
    public async Task<IActionResult> Acknowledge(int id, [FromBody] AcknowledgeClassroomEntryPermitRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.ClassroomEntryPermitAcknowledge)) return PermissionDenied();
        return Ok(await Mediator.Send(new AcknowledgeClassroomEntryPermitCommand(id, request), cancellationToken));
    }

    [HttpPost("{id:int}/revoke")]
    public async Task<IActionResult> Revoke(int id, [FromBody] RevokeClassroomEntryPermitRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.ClassroomEntryPermitRevoke)) return PermissionDenied();
        return Ok(await Mediator.Send(new RevokeClassroomEntryPermitCommand(id, request), cancellationToken));
    }
}
