using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Classrooms;
using AlFalah.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.StudentAffairs;

[Route("api/v1/classrooms")]
public sealed class ClassroomsController : StudentAffairsControllerBase
{
    public ClassroomsController(IMediator mediator, ICurrentUserService currentUser) : base(mediator, currentUser) { }

    [HttpGet("academic-years")]
    public async Task<IActionResult> AcademicYears(CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.StudentEnrollmentManage, PermissionNames.ClassroomManage)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetClassroomAcademicYearsQuery(), cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ClassroomListQuery query, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(
            PermissionNames.StudentManage,
            PermissionNames.StudentView,
            PermissionNames.StudentEnrollmentManage,
            PermissionNames.ClassroomManage,
            PermissionNames.TeacherQuickActionView,
            PermissionNames.AttendanceViewStudents,
            PermissionNames.AttendanceManageStudents)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetClassroomsQuery(query), cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClassroomRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.StudentEnrollmentManage, PermissionNames.ClassroomManage)) return PermissionDenied();
        var response = await Mediator.Send(new CreateClassroomCommand(request), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPatch("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateClassroomRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.StudentEnrollmentManage, PermissionNames.ClassroomManage)) return PermissionDenied();
        return Ok(await Mediator.Send(new UpdateClassroomCommand(id, request), cancellationToken));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, [FromBody] DeleteClassroomRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.StudentEnrollmentManage, PermissionNames.ClassroomManage)) return PermissionDenied();
        return Ok(await Mediator.Send(new DeleteClassroomCommand(id, request), cancellationToken));
    }

    [HttpGet("{id:int}/students")]
    public async Task<IActionResult> Students(int id, [FromQuery] int? academicTermId, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.StudentView, PermissionNames.TeacherQuickActionView)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetClassroomStudentsQuery(id, academicTermId), cancellationToken));
    }
}
