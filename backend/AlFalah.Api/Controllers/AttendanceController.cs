using AlFalah.Application.Common;
using AlFalah.Application.DTOs.Attendance;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

[ApiController]
[Route("api/v1/attendance")]
[Authorize]
public class AttendanceController : ControllerBase
{
    private readonly IAttendanceService _attendanceService;
    private readonly IAttendancePdfService _attendancePdfService;
    private readonly ICurrentUserService _currentUser;

    public AttendanceController(IAttendanceService attendanceService, ICurrentUserService currentUser, IAttendancePdfService attendancePdfService)
    {
        _attendanceService = attendanceService;
        _currentUser = currentUser;
        _attendancePdfService = attendancePdfService;
    }

    [HttpGet("sheet")]
    public async Task<IActionResult> GetSheet(
        [FromQuery] DateOnly date,
        [FromQuery] int? schoolId,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.AttendanceManage))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية إدارة الحضور."));

        var result = await _attendanceService.GetSheetAsync(date, schoolId, cancellationToken);
        return Ok(ApiResponse<AttendanceSheetDto>.Success(result));
    }

    [HttpPut("sheet")]
    public async Task<IActionResult> SaveSheet(
        [FromBody] SaveAttendanceSheetRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.AttendanceManage))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية إدارة الحضور."));

        var errors = await ValidationHelper.ValidateAsync(HttpContext.RequestServices, request, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<AttendanceSheetDto>.Fail(errors));

        var result = await _attendanceService.SaveSheetAsync(request, cancellationToken);
        return Ok(ApiResponse<AttendanceSheetDto>.Success(result, "تم حفظ سجل الحضور."));
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyAttendance(
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int? schoolId,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.AttendanceView))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية عرض الحضور."));

        var result = await _attendanceService.GetMyAttendanceAsync(fromDate, toDate, schoolId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<MyAttendanceItemDto>>.Success(result));
    }

    [HttpGet("records")]
    public async Task<IActionResult> GetRecords(
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] string? name,
        [FromQuery] int? schoolId,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.AttendanceManage))
            return StatusCode(403, ApiResponse.Fail("Ù„ÙŠØ³ Ù„Ø¯ÙŠÙƒ ØµÙ„Ø§Ø­ÙŠØ© Ø¹Ø±Ø¶ Ø³Ø¬Ù„ Ø§Ù„Ø­Ø¶ÙˆØ±."));

        var result = await _attendanceService.GetRecordsAsync(fromDate, toDate, name, schoolId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AttendanceRecordItemDto>>.Success(result));
    }

    [HttpGet("records/pdf")]
    public async Task<IActionResult> ExportRecordsPdf(
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] string? name,
        [FromQuery] int? schoolId,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.AttendanceManage))
            return StatusCode(403, ApiResponse.Fail("Ù„ÙŠØ³ Ù„Ø¯ÙŠÙƒ ØµÙ„Ø§Ø­ÙŠØ© ØªØµØ¯ÙŠØ± Ø³Ø¬Ù„ Ø§Ù„Ø­Ø¶ÙˆØ±."));

        var records = await _attendanceService.GetRecordsAsync(fromDate, toDate, name, schoolId, cancellationToken);
        var bytes = await _attendancePdfService.RenderAsync(records, cancellationToken);
        return File(bytes, "application/pdf", $"attendance-records-{DateTime.UtcNow:yyyyMMdd-HHmmss}.pdf");
    }
}
