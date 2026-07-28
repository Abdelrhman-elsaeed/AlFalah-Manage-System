using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

[ApiController]
[Route("api/v1/school-microsoft-drive")]
[Authorize]
public sealed class SchoolMicrosoftDriveController : ControllerBase
{
    private readonly ISchoolMicrosoftDriveService _service;
    public SchoolMicrosoftDriveController(ISchoolMicrosoftDriveService service) => _service = service;
    [HttpGet] public async Task<IActionResult> Get(CancellationToken cancellationToken) => Ok(ApiResponse<SchoolMicrosoftDriveSettingsDto>.Success(await _service.GetForCurrentSchoolAsync(cancellationToken)));
    [HttpPut] public async Task<IActionResult> Configure([FromBody] ConfigureSchoolMicrosoftDriveRequest request, CancellationToken cancellationToken) => Ok(ApiResponse<SchoolMicrosoftDriveSettingsDto>.Success(await _service.ConfigureForCurrentSchoolAsync(request, cancellationToken), "تم إعداد حساب Microsoft الخاص بالمدرسة."));
}
