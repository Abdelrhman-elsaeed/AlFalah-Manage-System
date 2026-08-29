using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

/// <summary>
/// Where a manager grants, inspects and withdraws a teacher's evidence folder.
/// Teacher clients never receive DriveId or RootItemId through any other endpoint.
/// </summary>
[ApiController]
[Route("api/v1/teacher-drive-admin")]
[Authorize]
public sealed class TeacherDriveAdminController : ControllerBase
{
    private readonly ICurrentUserService _currentUser;
    private readonly ITeacherDriveMappingService _mappings;

    public TeacherDriveAdminController(ICurrentUserService currentUser, ITeacherDriveMappingService mappings)
    {
        _currentUser = currentUser;
        _mappings = mappings;
    }

    [HttpGet("teachers/{teacherId:int}/folder")]
    public async Task<IActionResult> GetFolder(int teacherId, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.InstructorView))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لعرض مجلدات المعلمين."));
        var mapping = await _mappings.FindForTeacherAsync(teacherId, cancellationToken);
        return Ok(ApiResponse<DriveFolderMappingDto?>.Success(mapping));
    }

    [HttpPut("teachers/{teacherId:int}/folder")]
    public async Task<IActionResult> UpsertFolder(
        int teacherId, [FromBody] UpsertDriveFolderMappingRequest request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.InstructorEdit))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لإعداد مجلدات المعلمين."));
        return Ok(ApiResponse<DriveFolderMappingDto>.Success(
            await _mappings.UpsertAsync(teacherId, request, cancellationToken),
            "تم منح المعلم صلاحية المجلد."));
    }

    [HttpGet("teachers/{teacherId:int}/folders")]
    public async Task<IActionResult> BrowseFolders(
        int teacherId, [FromQuery] BrowseAdminDriveFoldersRequest request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.InstructorEdit))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لإعداد مجلدات المعلمين."));
        return Ok(ApiResponse<AdminDriveFolderPageDto>.Success(
            await _mappings.BrowseFoldersAsync(teacherId, request, cancellationToken)));
    }

    [HttpDelete("teachers/{teacherId:int}/folder")]
    public async Task<IActionResult> RevokeFolder(int teacherId, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.InstructorEdit))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لسحب مجلدات المعلمين."));
        await _mappings.RevokeAsync(teacherId, cancellationToken);
        return Ok(ApiResponse.Success("تم سحب صلاحية المجلد. تبقى الملفات المرفوعة مسجلة في المصفوفة."));
    }
}
