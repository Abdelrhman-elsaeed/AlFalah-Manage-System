using AlFalah.Application.Storage;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AlFalah.Api.Controllers;

[ApiController,Authorize,Route("api/v1/storage/drive"),EnableRateLimiting("teacher-drive")]
public sealed class StorageDriveBrowserController(IStorageDriveBrowserService service):ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Browse([FromQuery]string? handle,[FromQuery]string? pageToken,CancellationToken ct)=>
        Ok(ApiResponse<DriveBrowserPage>.Success(await service.BrowseAsync(handle,pageToken,ct)));
    [HttpGet("content")]
    public async Task<IActionResult> Content([FromQuery]string handle,CancellationToken ct)
    {
        var file=await service.ContentAsync(handle,ct);Response.Headers.CacheControl="private, no-store";
        Response.Headers["X-Content-Type-Options"]="nosniff";Response.Headers["Content-Security-Policy"]="sandbox";
        return File(file.Content,file.ContentType,file.FileName);
    }
    [HttpPost("impact")]
    public async Task<IActionResult> Impact(DriveImpactRequest request,CancellationToken ct)=>
        Ok(ApiResponse<DriveImpactDto>.Success(await service.ImpactAsync(request,ct)));
    [HttpPost("mutations")]
    public async Task<IActionResult> Mutate(DriveMutationRequest request,CancellationToken ct)=>
        Ok(ApiResponse<DriveMutationDto>.Success(await service.MutateAsync(request,ct)));
    [HttpGet("operations")]
    public async Task<IActionResult> Pending(CancellationToken ct)=>
        Ok(ApiResponse<IReadOnlyList<DriveMutationDto>>.Success(await service.PendingAsync(ct)));
    [HttpGet("deleted")]
    public async Task<IActionResult> Deleted(CancellationToken ct)=>
        Ok(ApiResponse<IReadOnlyList<DriveMutationDto>>.Success(await service.DeletedAsync(ct)));
    [HttpPost("deleted/{id:int}/restore")]
    public async Task<IActionResult> Restore(int id,CancellationToken ct)=>
        Ok(ApiResponse<DriveMutationDto>.Success(await service.RestoreAsync(id,ct)));
    [HttpPost("operations/{id:int}/reconcile")]
    public async Task<IActionResult> Reconcile(int id,CancellationToken ct)=>
        Ok(ApiResponse<DriveMutationDto>.Success(await service.ReconcileAsync(id,ct)));
}
