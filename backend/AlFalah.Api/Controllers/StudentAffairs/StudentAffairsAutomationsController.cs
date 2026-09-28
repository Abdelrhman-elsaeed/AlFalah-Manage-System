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
        return FeatureNotImplemented("W7 Automations and Notifications");
    }

    [HttpGet("triggers")]
    public async Task<IActionResult> Triggers([FromQuery] StudentAffairsPageQuery query, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.AutomationView)) return PermissionDenied();
        return FeatureNotImplemented("W7 Automations and Notifications");
    }

    [HttpGet("failures")]
    public async Task<IActionResult> Failures([FromQuery] StudentAffairsPageQuery query, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.AutomationView)) return PermissionDenied();
        return FeatureNotImplemented("W7 Automations and Notifications");
    }

    [HttpPost("failures/{id:long}/retry")]
    public async Task<IActionResult> Retry(long id, [FromBody] RetryAutomationFailureRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.AutomationRetry)) return PermissionDenied();
        return FeatureNotImplemented("W7 Automations and Notifications");
    }
}
