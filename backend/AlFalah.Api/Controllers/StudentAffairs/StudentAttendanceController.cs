using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Attendance;
using AlFalah.Application.StudentAffairs.Attendance;
using AlFalah.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.StudentAffairs;

[Route("api/v1/student-attendance")]
public sealed class StudentAttendanceController : StudentAffairsControllerBase
{
    public StudentAttendanceController(IMediator mediator, ICurrentUserService currentUser) : base(mediator, currentUser) { }

    [HttpGet("sheet")]
    public async Task<IActionResult> Sheet([FromQuery] DateOnly date, [FromQuery] int classroomId, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.AttendanceViewStudents, PermissionNames.AttendanceManageStudents)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetStudentAttendanceSheetQuery(date, classroomId), cancellationToken));
    }

    [HttpPut("sheet")]
    public async Task<IActionResult> SubmitSheet(
        [FromBody] SubmitAbsentRosterRequestDto request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.AttendanceManageStudents)) return PermissionDenied();
        return Ok(await Mediator.Send(new SubmitAbsentRosterCommand(request, idempotencyKey), cancellationToken));
    }

    [HttpPatch("{attendanceId:int}")]
    public async Task<IActionResult> Correct(int attendanceId, [FromBody] CorrectStudentAttendanceRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.AttendanceOverrideCorrection)) return PermissionDenied();
        return Ok(await Mediator.Send(new CorrectStudentAttendanceCommand(attendanceId, request), cancellationToken));
    }

    [HttpGet("records")]
    public async Task<IActionResult> Records([FromQuery] StudentAttendanceRecordsQuery query, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.AttendanceViewStudents)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetStudentAttendanceRecordsQuery(query), cancellationToken));
    }

    [HttpGet("students/{studentId:int}")]
    public async Task<IActionResult> StudentHistory(int studentId, [FromQuery] int? academicTermId, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.AttendanceViewStudents, PermissionNames.GuardianViewLinkedStudents)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetStudentAttendanceHistoryQuery(studentId, academicTermId), cancellationToken));
    }

    [HttpPost("{attendanceId:int}/excuses")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubmitExcuse(
        int attendanceId,
        [FromForm] SubmitAbsenceExcuseRequestDto request,
        [FromForm] IFormFile attachment,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.AttendanceSubmitExcuse)) return PermissionDenied();
        await using var content = attachment.OpenReadStream();
        var command = new SubmitAbsenceExcuseCommand(
            attendanceId,
            request,
            idempotencyKey,
            content,
            attachment.FileName,
            attachment.ContentType,
            attachment.Length);
        var response = await Mediator.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status202Accepted, response);
    }

    [HttpGet("{attendanceId:int}/excuses")]
    public async Task<IActionResult> Excuses(int attendanceId, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.AttendanceViewStudents, PermissionNames.AttendanceSubmitExcuse)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetAbsenceExcusesQuery(attendanceId), cancellationToken));
    }

    [HttpGet("excuses/{excuseId:int}/attachments/{attachmentId:int}")]
    public async Task<IActionResult> DownloadAttachment(int excuseId, int attachmentId, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.AttendanceViewStudents, PermissionNames.AttendanceSubmitExcuse)) return PermissionDenied();
        var file = await Mediator.Send(new DownloadAbsenceExcuseAttachmentQuery(excuseId, attachmentId), cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpPost("excuses/{excuseId:int}/accept")]
    public async Task<IActionResult> AcceptExcuse(int excuseId, [FromBody] ReviewAbsenceExcuseRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.AttendanceReviewExcuse)) return PermissionDenied();
        return Ok(await Mediator.Send(new AcceptAbsenceExcuseCommand(excuseId, request), cancellationToken));
    }

    [HttpPost("excuses/{excuseId:int}/reject")]
    public async Task<IActionResult> RejectExcuse(int excuseId, [FromBody] RejectAbsenceExcuseRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.AttendanceReviewExcuse)) return PermissionDenied();
        return Ok(await Mediator.Send(new RejectAbsenceExcuseCommand(excuseId, request), cancellationToken));
    }

    [HttpPost("noor/exports")]
    public async Task<IActionResult> ExportNoor(
        [FromQuery] DateOnly weekStartsOn,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.NoorExport)) return PermissionDenied();
        var response = await Mediator.Send(
            new ExportNoorAbsenceCorrectionsCommand(weekStartsOn, idempotencyKey),
            cancellationToken);
        if (!response.IsSuccess || response.Data is null) return BadRequest(response);
        Response.Headers.Append("X-Noor-Batch-Id", response.Data.BatchId.ToString());
        Response.Headers.Append("X-Noor-Row-Count", response.Data.RowCount.ToString());
        return File(response.Data.Content, response.Data.ContentType, response.Data.FileName);
    }
}
