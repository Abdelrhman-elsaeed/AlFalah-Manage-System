using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Messaging;
using AlFalah.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.StudentAffairs;

[Route("api/v1/office-hours")]
public sealed class OfficeHoursController : StudentAffairsControllerBase
{
    public OfficeHoursController(IMediator mediator, ICurrentUserService currentUser) : base(mediator, currentUser) { }

    [HttpGet("me/eligible")]
    public async Task<IActionResult> Eligible(CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.OfficeHoursManageOwn)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetEligibleOfficeHoursQuery(), cancellationToken));
    }

    [HttpGet("me")]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.OfficeHoursManageOwn)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetMyOfficeHoursQuery(), cancellationToken));
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMine([FromBody] UpdateMyOfficeHoursRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.OfficeHoursManageOwn)) return PermissionDenied();
        return Ok(await Mediator.Send(new UpdateMyOfficeHoursCommand(request), cancellationToken));
    }

    [HttpGet("teachers/{instructorId:int}")]
    public async Task<IActionResult> Teacher(int instructorId, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.OfficeHoursView)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetTeacherOfficeHoursQuery(instructorId), cancellationToken));
    }

    [HttpPut("teachers/{instructorId:int}")]
    public async Task<IActionResult> Override(int instructorId, [FromBody] OverrideTeacherOfficeHoursRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.OfficeHoursManageSchool)) return PermissionDenied();
        return Ok(await Mediator.Send(new OverrideTeacherOfficeHoursCommand(instructorId, request), cancellationToken));
    }
}
