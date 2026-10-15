using AlFalah.Application.Storage;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AlFalah.Api.Controllers;

[ApiController, Authorize, Route("api/v1/storage/map"), EnableRateLimiting("teacher-drive")]
public sealed class StorageMapController(IStorageMapService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Read(CancellationToken ct) => Ok(ApiResponse<StorageMapDto>.Success(await service.ReadAsync(ct)));
}
