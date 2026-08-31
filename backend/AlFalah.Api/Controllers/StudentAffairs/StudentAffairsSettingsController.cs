using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Settings;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.StudentAffairs;

[Route("api/v1/student-affairs/settings")]
public sealed class StudentAffairsSettingsController : StudentAffairsControllerBase
{
    public StudentAffairsSettingsController(IMediator mediator, ICurrentUserService currentUser) : base(mediator, currentUser) { }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.StudentAffairsSettingsView)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetStudentAffairsSettingsQuery(), cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStudentAffairsSettingsRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.StudentAffairsSettingsManage)) return PermissionDenied();
        var response = await Mediator.Send(new CreateStudentAffairsSettingsCommand(request), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateStudentAffairsSettingsRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.StudentAffairsSettingsManage)) return PermissionDenied();
        return Ok(await Mediator.Send(new UpdateStudentAffairsSettingsCommand(request), cancellationToken));
    }

    [HttpDelete]
    public async Task<IActionResult> Reset([FromBody] ResetStudentAffairsSettingsRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.StudentAffairsSettingsManage)) return PermissionDenied();
        return Ok(await Mediator.Send(new ResetStudentAffairsSettingsCommand(request), cancellationToken));
    }

    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] StudentAffairsPageQuery query, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.StudentAffairsSettingsView)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetStudentAffairsSettingsHistoryQuery(query), cancellationToken));
    }
}
