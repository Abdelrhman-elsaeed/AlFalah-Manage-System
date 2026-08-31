using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Automations;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.StudentAffairs;

[Route("api/v1/student-affairs/automations")]
public sealed class StudentAffairsAutomationsController : StudentAffairsControllerBase
{
    public StudentAffairsAutomationsController(IMediator mediator, ICurrentUserService currentUser) : base(mediator, currentUser) { }

    [HttpGet("rules")]
    public async Task<IActionResult> Rules(CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.AutomationView)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetAutomationRulesQuery(), cancellationToken));
    }

    [HttpGet("triggers")]
    public async Task<IActionResult> Triggers([FromQuery] StudentAffairsPageQuery query, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.AutomationView)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetAutomationTriggersQuery(query), cancellationToken));
    }

    [HttpGet("failures")]
    public async Task<IActionResult> Failures([FromQuery] StudentAffairsPageQuery query, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.AutomationView)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetAutomationFailuresQuery(query), cancellationToken));
    }

    [HttpPost("failures/{id:long}/retry")]
    public async Task<IActionResult> Retry(long id, [FromBody] RetryAutomationFailureRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.AutomationRetry)) return PermissionDenied();
        var response = await Mediator.Send(new RetryAutomationFailureCommand(id, request), cancellationToken);
        return StatusCode(StatusCodes.Status202Accepted, response);
    }
}
