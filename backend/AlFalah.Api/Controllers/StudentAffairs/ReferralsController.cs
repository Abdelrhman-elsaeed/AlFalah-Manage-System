using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Referrals;
using AlFalah.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.StudentAffairs;

[Route("api/v1/referrals")]
public sealed class ReferralsController : StudentAffairsControllerBase
{
    public ReferralsController(IMediator mediator, ICurrentUserService currentUser) : base(mediator, currentUser) { }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReferralRequestDto request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.ReferralCreate)) return PermissionDenied();
        var response = await Mediator.Send(new CreateReferralCommand(request, idempotencyKey), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ReferralListQuery query, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.ReferralView)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetReferralsQuery(query), cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.ReferralView)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetReferralByIdQuery(id), cancellationToken));
    }

    [HttpPost("{id:int}/assign")]
    public async Task<IActionResult> Assign(int id, [FromBody] AssignReferralRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.ReferralAssign)) return PermissionDenied();
        return Ok(await Mediator.Send(new AssignReferralCommand(id, request), cancellationToken));
    }

    [HttpPost("{id:int}/accept")]
    public async Task<IActionResult> Accept(int id, [FromBody] AcceptReferralRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.ReferralManage)) return PermissionDenied();
        return Ok(await Mediator.Send(new AcceptReferralCommand(id, request), cancellationToken));
    }

    [HttpPost("{id:int}/actions")]
    public async Task<IActionResult> AddAction(int id, [FromBody] AddReferralActionRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.ReferralManage)) return PermissionDenied();
        return Ok(await Mediator.Send(new AddReferralActionCommand(id, request), cancellationToken));
    }

    [HttpPost("{id:int}/resolve")]
    public async Task<IActionResult> Resolve(int id, [FromBody] ResolveReferralRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.ReferralManage)) return PermissionDenied();
        return Ok(await Mediator.Send(new ResolveReferralCommand(id, request), cancellationToken));
    }

    [HttpPost("{id:int}/reopen")]
    public async Task<IActionResult> Reopen(int id, [FromBody] ReopenReferralRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.ReferralManage)) return PermissionDenied();
        return Ok(await Mediator.Send(new ReopenReferralCommand(id, request), cancellationToken));
    }
}
