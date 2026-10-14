using AlFalah.Application.Duty;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

[Authorize, ApiController, Route("api/v1/school-duty")]
public sealed class SchoolDutyController(IDutyService service) : ControllerBase
{
    [HttpGet("context")]
    public async Task<IActionResult> Context([FromQuery] int? termId, CancellationToken ct) =>
        Ok(ApiResponse<DutyWorkspace>.Success(await service.WorkspaceAsync(termId, ct)));
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)=>Ok(ApiResponse<DutyDashboard>.Success(await service.DashboardAsync(ct)));
    [HttpPost("plans/preview")]
    public async Task<IActionResult> Preview(DutyPreviewRequest request, CancellationToken ct) =>
        Ok(ApiResponse<DutyPreview>.Success(await service.PreviewAsync(request, ct)));
    [HttpPost("plans/publish")]
    public async Task<IActionResult> Publish(DutyPublishRequest request, CancellationToken ct) =>
        Ok(ApiResponse<DutyMutationResult>.Success(await service.PublishAsync(request, ct)));
    [HttpPost("assignments/change-preview")]
    public async Task<IActionResult> ChangePreview(DutyChangeRequest request, CancellationToken ct) =>
        Ok(ApiResponse<DutyPreview>.Success(await service.ChangePreviewAsync(request, ct)));
    [HttpPost("assignments/change")]
    public async Task<IActionResult> Change(DutyChangeRequest request, CancellationToken ct) =>
        Ok(ApiResponse<DutyMutationResult>.Success(await service.ChangeAsync(request, ct)));
    [HttpPost("reconciliation/retry")]
    public async Task<IActionResult> Retry(CancellationToken ct) { await service.RetryAsync(ct); return Ok(ApiResponse<bool>.Success(true)); }
}
