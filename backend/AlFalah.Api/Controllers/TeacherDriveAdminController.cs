using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

/// <summary>Local-administration endpoints; teacher clients never receive DriveId or RootItemId.</summary>
[ApiController]
[Route("api/v1/teacher-drive-admin")]
[Authorize]
public sealed class TeacherDriveAdminController : ControllerBase
{
    private readonly ICurrentUserService _currentUser;
    private readonly ITeacherDriveMappingService _mappings;
    private readonly ITeacherMicrosoftAccountService _accounts;
    public TeacherDriveAdminController(ICurrentUserService currentUser, ITeacherDriveMappingService mappings, ITeacherMicrosoftAccountService accounts)
    { _currentUser = currentUser; _mappings = mappings; _accounts = accounts; }

    [HttpPut("teachers/{teacherId:int}/folder")]
    public async Task<IActionResult> UpsertFolder(int teacherId, [FromBody] UpsertDriveFolderMappingRequest request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.InstructorEdit)) return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لإعداد مجلدات المعلمين."));
        return Ok(ApiResponse<DriveFolderMappingDto>.Success(await _mappings.UpsertAsync(teacherId, request, cancellationToken)));
    }

    [HttpPut("teachers/{teacherId:int}/microsoft-email")]
    public async Task<IActionResult> ConfigureEmail(int teacherId, [FromBody] ConfigureMicrosoftEmailRequest request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.InstructorEdit)) return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لإعداد حسابات المعلمين."));
        return Ok(ApiResponse<TeacherMicrosoftAccountAdminDto>.Success(await _accounts.ConfigureExpectedEmailAsync(teacherId, request.MicrosoftEmail, cancellationToken)));
    }
}

public sealed record ConfigureMicrosoftEmailRequest(string MicrosoftEmail);
