using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Students;
using AlFalah.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.StudentAffairs;

[Route("api/v1/students")]
public sealed class StudentsController : StudentAffairsControllerBase
{
    public StudentsController(IMediator mediator, ICurrentUserService currentUser) : base(mediator, currentUser) { }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] StudentListQuery query, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.StudentManage, PermissionNames.StudentView)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetStudentsQuery(query), cancellationToken));
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] StudentStatsQuery query, CancellationToken cancellationToken)
    {
        if (CurrentUser.IsInRole(RoleNames.Secretary)) return PermissionDenied();
        if (!HasAnyPermission(PermissionNames.StudentManage, PermissionNames.StudentView, PermissionNames.AttendanceViewStudents, PermissionNames.ReferralView)
            && !CurrentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            && !CurrentUser.IsInRole(RoleNames.SocialWorker)
            && !CurrentUser.IsInRole(RoleNames.SchoolManager)
            && !CurrentUser.IsInRole(RoleNames.MainManager)
            && !CurrentUser.IsInRole(RoleNames.SuperAdmin))
        {
            return PermissionDenied();
        }
        return Ok(await Mediator.Send(new GetStudentsStatsQuery(query), cancellationToken));
    }

    [HttpGet("{studentId:int}")]
    public async Task<IActionResult> GetById(int studentId, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.StudentManage, PermissionNames.StudentView, PermissionNames.GuardianViewLinkedStudents)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetStudentByIdQuery(studentId), cancellationToken));
    }

    [HttpGet("{studentId:int}/analytics-profile")]
    public async Task<IActionResult> GetAnalyticsProfile(int studentId, CancellationToken cancellationToken)
    {
        if (CurrentUser.IsInRole(RoleNames.Secretary)) return PermissionDenied();
        if (!HasAnyPermission(PermissionNames.StudentManage, PermissionNames.StudentView, PermissionNames.AttendanceViewStudents, PermissionNames.ReferralView)
            && !CurrentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            && !CurrentUser.IsInRole(RoleNames.SocialWorker)
            && !CurrentUser.IsInRole(RoleNames.SchoolManager)
            && !CurrentUser.IsInRole(RoleNames.MainManager)
            && !CurrentUser.IsInRole(RoleNames.SuperAdmin))
        {
            return PermissionDenied();
        }
        return Ok(await Mediator.Send(new GetStudentAnalyticsProfileQuery(studentId), cancellationToken));
    }


    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStudentRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.StudentManage, PermissionNames.StudentCreate)) return PermissionDenied();
        var response = await Mediator.Send(new CreateStudentCommand(request), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPatch("{studentId:int}")]
    public async Task<IActionResult> Update(int studentId, [FromBody] UpdateStudentRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.StudentManage, PermissionNames.StudentEdit)) return PermissionDenied();
        return Ok(await Mediator.Send(new UpdateStudentCommand(studentId, request), cancellationToken));
    }

    [HttpDelete("{studentId:int}")]
    public async Task<IActionResult> Delete(int studentId, [FromBody] DeleteStudentRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.StudentManage, PermissionNames.StudentArchive)) return PermissionDenied();
        return Ok(await Mediator.Send(new DeleteStudentCommand(studentId, request), cancellationToken));
    }

    [HttpGet("{studentId:int}/timeline")]
    public async Task<IActionResult> Timeline(int studentId, [FromQuery] StudentTimelineQuery query, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.StudentView, PermissionNames.GuardianViewLinkedStudents)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetStudentTimelineQuery(studentId, query), cancellationToken));
    }

    [HttpPost("{studentId:int}/enrollments")]
    public async Task<IActionResult> Enroll(int studentId, [FromBody] CreateStudentEnrollmentRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.StudentEnrollmentManage)) return PermissionDenied();
        var response = await Mediator.Send(new CreateStudentEnrollmentCommand(studentId, request), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPatch("{studentId:int}/enrollments/{enrollmentId:int}")]
    public async Task<IActionResult> UpdateEnrollment(int studentId, int enrollmentId, [FromBody] UpdateStudentEnrollmentRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.StudentEnrollmentManage)) return PermissionDenied();
        return Ok(await Mediator.Send(new UpdateStudentEnrollmentCommand(studentId, enrollmentId, request), cancellationToken));
    }

    [HttpGet("{studentId:int}/guardians")]
    public async Task<IActionResult> Guardians(int studentId, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(
                PermissionNames.GuardianView,
                PermissionNames.StudentView,
                PermissionNames.SummonView,
                PermissionNames.SummonSchedule,
                PermissionNames.SummonMarkAttended,
                PermissionNames.GuardianViewLinkedStudents))
            return PermissionDenied();
        return Ok(await Mediator.Send(new GetStudentGuardiansQuery(studentId), cancellationToken));
    }

    [HttpPost("{studentId:int}/guardians")]
    public async Task<IActionResult> LinkGuardian(int studentId, [FromBody] LinkStudentGuardianRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.GuardianLinkStudent)) return PermissionDenied();
        var response = await Mediator.Send(new LinkStudentGuardianCommand(studentId, request), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpDelete("{studentId:int}/guardians/{linkId:int}")]
    public async Task<IActionResult> RevokeGuardian(int studentId, int linkId, [FromBody] RevokeStudentGuardianRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.GuardianLinkStudent)) return PermissionDenied();
        return Ok(await Mediator.Send(new RevokeStudentGuardianCommand(studentId, linkId, request), cancellationToken));
    }
}
