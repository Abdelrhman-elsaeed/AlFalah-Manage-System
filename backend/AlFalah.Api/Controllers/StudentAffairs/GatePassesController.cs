using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.GatePasses;
using AlFalah.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.StudentAffairs;

[Route("api/v1/gate-passes")]
public sealed class GatePassesController : StudentAffairsControllerBase
{
    public GatePassesController(IMediator mediator, ICurrentUserService currentUser) : base(mediator, currentUser) { }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGatePassRequestDto request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.GatePassRequest)) return PermissionDenied();
        var response = await Mediator.Send(new CreateGatePassCommand(request, idempotencyKey), cancellationToken);
        return FromResponse(response, StatusCodes.Status201Created);
    }

    [HttpGet("mine")]
    public async Task<IActionResult> Mine([FromQuery] GatePassListQuery query, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.GatePassViewOwn)) return PermissionDenied();
        return FromResponse(await Mediator.Send(new GetMyGatePassesQuery(query), cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] GatePassListQuery query, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.GatePassView)) return PermissionDenied();
        return FromResponse(await Mediator.Send(new GetGatePassesQuery(query), cancellationToken));
    }

    [HttpGet("security-queue")]
    public async Task<IActionResult> SecurityQueue([FromQuery] GatePassListQuery query, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(
                PermissionNames.StudentAffairsDashboardSecurity,
                PermissionNames.GatePassAcknowledgeSecurity,
                PermissionNames.GatePassExecute)) return PermissionDenied();
        return FromResponse(await Mediator.Send(new GetSecurityGatePassQueueQuery(query), cancellationToken));
    }

    [HttpGet("manager-audit")]
    public async Task<IActionResult> ManagerAudit([FromQuery] GatePassListQuery query, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.GatePassViewAudit)) return PermissionDenied();
        return FromResponse(await Mediator.Send(new GetManagerGatePassAuditQuery(query), cancellationToken));
    }

    [HttpGet("security-queue/{id:int}")]
    public async Task<IActionResult> SecurityDetail(int id, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(
                PermissionNames.StudentAffairsDashboardSecurity,
                PermissionNames.GatePassAcknowledgeSecurity,
                PermissionNames.GatePassExecute)) return PermissionDenied();
        return FromResponse(await Mediator.Send(new GetSecurityGatePassByIdQuery(id), cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.GatePassView, PermissionNames.GatePassViewOwn)) return PermissionDenied();
        return FromResponse(await Mediator.Send(new GetGatePassByIdQuery(id), cancellationToken));
    }

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, [FromBody] ApproveGatePassRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.GatePassApprove)) return PermissionDenied();
        return FromResponse(await Mediator.Send(new ApproveGatePassCommand(id, request), cancellationToken));
    }

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectGatePassRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.GatePassReject)) return PermissionDenied();
        return FromResponse(await Mediator.Send(new RejectGatePassCommand(id, request), cancellationToken));
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelGatePassRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.GatePassCancelOwn, PermissionNames.GatePassOverride)) return PermissionDenied();
        return FromResponse(await Mediator.Send(new CancelGatePassCommand(id, request), cancellationToken));
    }

    [HttpPost("{id:int}/teacher-acknowledgement")]
    public async Task<IActionResult> TeacherAcknowledge(int id, [FromBody] AcknowledgeGatePassRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.GatePassAcknowledgeTeacher)) return PermissionDenied();
        return FromResponse(await Mediator.Send(new AcknowledgeGatePassByTeacherCommand(id, request), cancellationToken));
    }

    [HttpPost("{id:int}/security-acknowledgement")]
    public async Task<IActionResult> SecurityAcknowledge(int id, [FromBody] AcknowledgeGatePassRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.GatePassAcknowledgeSecurity)) return PermissionDenied();
        return FromResponse(await Mediator.Send(new AcknowledgeGatePassBySecurityCommand(id, request), cancellationToken));
    }

    [HttpPost("{id:int}/exit")]
    public async Task<IActionResult> Execute(int id, [FromBody] ExecuteGatePassRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.GatePassExecute)) return PermissionDenied();
        return FromResponse(await Mediator.Send(new ExecuteGatePassCommand(id, request), cancellationToken));
    }

    [HttpPost("{id:int}/false-exit-incident")]
    public async Task<IActionResult> RecordFalseExitIncident(int id, [FromBody] RecordFalseExitIncidentRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.GatePassOverride)) return PermissionDenied();
        return FromResponse(await Mediator.Send(new RecordFalseExitIncidentCommand(id, request), cancellationToken));
    }

    [HttpGet("{id:int}/history")]
    public async Task<IActionResult> History(int id, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.GatePassViewAudit, PermissionNames.GatePassViewOwn)) return PermissionDenied();
        return FromResponse(await Mediator.Send(new GetGatePassHistoryQuery(id), cancellationToken));
    }
}
