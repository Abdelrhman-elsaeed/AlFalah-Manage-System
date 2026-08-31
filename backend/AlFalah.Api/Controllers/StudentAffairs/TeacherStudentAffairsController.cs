using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Teacher;
using AlFalah.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.StudentAffairs;

[Route("api/v1/teacher/student-affairs")]
public sealed class TeacherStudentAffairsController : StudentAffairsControllerBase
{
    public TeacherStudentAffairsController(IMediator mediator, ICurrentUserService currentUser) : base(mediator, currentUser) { }

    [HttpGet("current-context")]
    public async Task<IActionResult> CurrentContext(CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.TeacherQuickActionView)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetTeacherCurrentContextQuery(), cancellationToken));
    }

    [HttpGet("periods/{entryId:int}/roster")]
    public async Task<IActionResult> PeriodRoster(int entryId, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.TeacherQuickActionView)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetTeacherPeriodRosterQuery(entryId), cancellationToken));
    }

    [HttpGet("top-priority")]
    public async Task<IActionResult> TopPriority(CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.TeacherQuickActionView)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetTeacherTopPriorityQuery(), cancellationToken));
    }
}
