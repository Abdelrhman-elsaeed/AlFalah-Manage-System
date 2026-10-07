using AlFalah.Application.Storage;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AlFalah.Api.Controllers;

[ApiController, Authorize, EnableRateLimiting("teacher-drive")]
[Route("api/v1/storage/visits")]
public sealed class VisitArchiveController(IVisitArchiveService service) : ControllerBase
{
    [HttpGet("operations-status")]
    public async Task<IActionResult> OperationsStatus(CancellationToken ct) =>
        Ok(ApiResponse<VisitArchiveOperationsDto>.Success(await service.OperationsStatusAsync(ct)));
    [HttpGet("teachers")]
    public async Task<IActionResult> Teachers(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<VisitArchiveTeacherDto>>.Success(await service.TeachersAsync(ct)));
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] VisitArchiveQuery query, CancellationToken ct) =>
        Ok(ApiResponse<StoragePage<VisitArchiveDto>>.Success(await service.ListAsync(query, ct)));
    [HttpGet("{visitId:int}/archive")]
    public async Task<IActionResult> Get(int visitId, CancellationToken ct) =>
        Ok(ApiResponse<VisitArchiveDto>.Success(await service.GetAsync(visitId, ct)));
    [HttpPost("{visitId:int}/archive/retry")]
    public async Task<IActionResult> Retry(int visitId, RetryVisitArchiveRequest request, CancellationToken ct) =>
        Accepted(ApiResponse<VisitArchiveDto>.Success(await service.RetryAsync(visitId, request, ct)));
    [HttpGet("{visitId:int}/archive/{revision:int}/content")]
    public async Task<IActionResult> Content(int visitId, int revision, [FromQuery] int? versionId, CancellationToken ct)
    {
        var content = await service.ContentAsync(visitId, revision, versionId, ct);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "sandbox";
        return File(content.Content, "application/pdf", content.FileName);
    }
}
