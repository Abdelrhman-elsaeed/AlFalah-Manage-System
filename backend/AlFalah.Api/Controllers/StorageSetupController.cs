using AlFalah.Application.Storage;
using System.Text.Json;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AlFalah.Api.Controllers;

[ApiController, Authorize, EnableRateLimiting("teacher-drive")]
[Route("api/v1/storage/setup")]
public sealed class StorageSetupController(IStorageSetupService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Plan(CancellationToken ct) =>
        Ok(ApiResponse<StorageSetupPlan>.Success(await service.PlanAsync(ct)));

    [HttpPost]
    public async Task<IActionResult> Apply([FromBody] ApplyStorageSetupRequest request, CancellationToken ct) =>
        Ok(ApiResponse<StorageSetupResult>.Success(await service.ApplyAsync(request.Folders, request.Teachers, ct, request.RecoveryKeys)));

    [HttpPost("stream")]
    public async Task ApplyStream([FromBody] ApplyStorageSetupRequest request, CancellationToken ct)
    {
        Response.ContentType = "application/x-ndjson; charset=utf-8";
        Response.Headers.CacheControl = "no-cache, no-transform";
        Response.Headers["X-Accel-Buffering"] = "no";
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        async Task Send(object value)
        {
            await Response.WriteAsync(JsonSerializer.Serialize(value, json) + "\n", ct);
            await Response.Body.FlushAsync(ct);
        }
        try
        {
            var result = await service.ApplyWithProgressAsync(request.Folders, request.Teachers,
                update => Send(new { kind = "progress", progress = update }), ct, request.RecoveryKeys);
            await Send(new { kind = "result", result });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception e)
        {
            var message = e is StorageUnavailableException or StorageConflictException or ArgumentException
                ? e.Message : "تعذّر إكمال إعداد الملفات. أعد المحاولة بعد التحقق من Drive.";
            await Send(new { kind = "error", message });
        }
    }

    [HttpPut("visit-archive")]
    public async Task<IActionResult> Archive([FromBody] SetVisitArchiveRequest request, CancellationToken ct) =>
        Ok(ApiResponse<VisitArchiveActivation>.Success(await service.SetArchiveEnabledAsync(request.Enabled, ct)));

    [HttpGet("visit-archive")]
    public async Task<IActionResult> ArchiveStatus(CancellationToken ct) =>
        Ok(ApiResponse<VisitArchiveActivation>.Success(await service.ArchiveStatusAsync(ct)));
}

public sealed record ApplyStorageSetupRequest(bool Folders, bool Teachers, IReadOnlyList<string>? RecoveryKeys = null);
public sealed record SetVisitArchiveRequest(bool Enabled);
