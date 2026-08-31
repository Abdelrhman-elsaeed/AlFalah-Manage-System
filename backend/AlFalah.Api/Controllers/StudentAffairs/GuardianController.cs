using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Guardian;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.StudentAffairs;

[Route("api/v1/guardian")]
public sealed class GuardianController : StudentAffairsControllerBase
{
    public GuardianController(IMediator mediator, ICurrentUserService currentUser) : base(mediator, currentUser) { }

    [HttpGet("students")]
    public async Task<IActionResult> Students(CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.GuardianViewLinkedStudents)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetGuardianStudentsQuery(), cancellationToken));
    }

    [HttpGet("students/{studentId:int}/summary")]
    public async Task<IActionResult> Summary(int studentId, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.GuardianViewLinkedStudents)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetGuardianStudentSummaryQuery(studentId), cancellationToken));
    }

    [HttpGet("students/{studentId:int}/notifications")]
    public async Task<IActionResult> Notifications(int studentId, [FromQuery] StudentAffairsPageQuery query, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.NotificationViewOwn)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetGuardianStudentNotificationsQuery(studentId, query), cancellationToken));
    }
}
