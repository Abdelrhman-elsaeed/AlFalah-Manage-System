using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Dashboards;
using AlFalah.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.StudentAffairs;

[Route("api/v1/student-affairs/dashboard")]
public sealed class StudentAffairsDashboardController : StudentAffairsControllerBase
{
    public StudentAffairsDashboardController(IMediator mediator, ICurrentUserService currentUser) : base(mediator, currentUser) { }

    [HttpGet("teacher")]
    public async Task<IActionResult> Teacher(CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.StudentAffairsDashboardTeacher)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetTeacherStudentAffairsDashboardQuery(), cancellationToken));
    }

    [HttpGet("officer")]
    public async Task<IActionResult> Officer(CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.StudentAffairsDashboardOfficer)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetOfficerStudentAffairsDashboardQuery(), cancellationToken));
    }

    [HttpGet("social-worker")]
    public async Task<IActionResult> SocialWorker(CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.StudentAffairsDashboardSocialWorker)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetSocialWorkerStudentAffairsDashboardQuery(), cancellationToken));
    }

    [HttpGet("security")]
    public async Task<IActionResult> Security(CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.StudentAffairsDashboardSecurity)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetSecurityStudentAffairsDashboardQuery(), cancellationToken));
    }

    [HttpGet("guardian")]
    public async Task<IActionResult> Guardian(CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.StudentAffairsDashboardGuardian)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetGuardianStudentAffairsDashboardQuery(), cancellationToken));
    }

    [HttpGet("school-oversight")]
    public async Task<IActionResult> SchoolOversight(CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.StudentAffairsDashboardSchoolOversight)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetSchoolOversightDashboardQuery(), cancellationToken));
    }
}
