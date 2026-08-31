using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Delays;
using AlFalah.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.StudentAffairs;

[Route("api/v1/morning-delays")]
public sealed class MorningDelaysController : StudentAffairsControllerBase
{
    public MorningDelaysController(IMediator mediator, ICurrentUserService currentUser) : base(mediator, currentUser) { }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] MorningDelayListQuery query, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.MorningDelayView)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetMorningDelaysQuery(query), cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.MorningDelayView)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetMorningDelayByIdQuery(id), cancellationToken));
    }

    [HttpPost("{id:int}/reason")]
    public async Task<IActionResult> ProvideReason(int id, [FromBody] ProvideMorningDelayReasonRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.MorningDelayManageReason, PermissionNames.GuardianViewLinkedStudents)) return PermissionDenied();
        return Ok(await Mediator.Send(new ProvideMorningDelayReasonCommand(id, request), cancellationToken));
    }

    [HttpPost("{id:int}/correct")]
    public async Task<IActionResult> Correct(int id, [FromBody] CorrectMorningDelayRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.AttendanceOverrideCorrection)) return PermissionDenied();
        return Ok(await Mediator.Send(new CorrectMorningDelayCommand(id, request), cancellationToken));
    }
}
