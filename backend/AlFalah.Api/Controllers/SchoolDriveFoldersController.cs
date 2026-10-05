using AlFalah.Application.Storage;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AlFalah.Api.Controllers;

[ApiController, Authorize]
[Route("api/v1/school-google-drive/folders")]
[EnableRateLimiting("teacher-drive")]
public sealed class SchoolDriveFoldersController(ISchoolDriveFolderService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Browse([FromQuery] SchoolDriveBrowseRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        return Ok(ApiResponse<SchoolDriveFolderPage>.Success(await service.BrowseAsync(request, ct)));
    }
}
