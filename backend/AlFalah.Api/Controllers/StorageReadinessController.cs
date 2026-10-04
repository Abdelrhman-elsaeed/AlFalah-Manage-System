using AlFalah.Application.Storage;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AlFalah.Api.Controllers;

[ApiController, Authorize, Route("api/v1/storage"), EnableRateLimiting("teacher-drive")]
public sealed class StorageReadinessController(IReadinessService service, IReadinessExportService exports) : ControllerBase
{
    [HttpGet("templates")]
    public async Task<IActionResult> Versions(CancellationToken ct) => Ok(ApiResponse<IReadOnlyList<EvaluationVersionDto>>.Success(await service.VersionsAsync(ct)));
    [HttpGet("evaluation-members")]
    public async Task<IActionResult> Members(CancellationToken ct) => Ok(ApiResponse<IReadOnlyList<EvaluationMemberDto>>.Success(await service.MembersAsync(ct)));
    [HttpGet("digital-index")]
    public async Task<IActionResult> Index([FromQuery] ReadinessFilter filter, CancellationToken ct) => Ok(ApiResponse<StoragePage<DigitalIndexFileDto>>.Success(await service.IndexAsync(filter, ct)));
    [HttpGet("templates/{version:int}")]
    public async Task<IActionResult> Template(int version, CancellationToken ct) => Ok(ApiResponse<EvaluationTemplateSnapshot>.Success(await service.TemplateAsync(version, ct)));
    [HttpPost("self-evaluation/initialize")]
    public async Task<IActionResult> Initialize(InitializeEvaluationRequest request, CancellationToken ct)
    { await service.InitializeAsync(request, ct); return Ok(ApiResponse<object>.Success(new { initialized = true })); }
    [HttpGet("requirements")]
    public async Task<IActionResult> Requirements([FromQuery] ReadinessFilter filter, CancellationToken ct) =>
        Ok(ApiResponse<StoragePage<ReadinessRequirementDto>>.Success(await service.RequirementsAsync(filter, ct: ct)));
    [HttpGet("readiness")]
    public async Task<IActionResult> Readiness([FromQuery] ReadinessFilter filter, CancellationToken ct) => Ok(ApiResponse<ReadinessDto>.Success(await service.ReadinessAsync(filter, ct)));
    [HttpGet("gaps")]
    public async Task<IActionResult> Gaps([FromQuery] ReadinessFilter filter, CancellationToken ct) =>
        Ok(ApiResponse<StoragePage<ReadinessRequirementDto>>.Success(await service.RequirementsAsync(filter, true, ct)));
    [HttpPatch("requirements/{id:int}/follow-up")]
    public async Task<IActionResult> Configure(int id, ConfigureFollowUpRequest request, CancellationToken ct) => Ok(ApiResponse<ReadinessRequirementDto>.Success(await service.ConfigureAsync(id, request, ct)));
    [HttpGet("requirements/{id:int}/follow-up-history")]
    public async Task<IActionResult> FollowUpHistory(int id, CancellationToken ct) => Ok(ApiResponse<IReadOnlyList<FollowUpHistoryDto>>.Success(await service.FollowUpHistoryAsync(id, ct)));
    [HttpGet("manual-evaluations")]
    public async Task<IActionResult> Manuals([FromQuery] int academicYearId, [FromQuery] int templateVersion, CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<ManualEvaluationDto>>.Success(await service.ManualsAsync(academicYearId, templateVersion, ct)));
    [HttpPost("manual-evaluations")]
    public async Task<IActionResult> SaveManual(SaveManualEvaluationRequest request, CancellationToken ct) => Ok(ApiResponse<ManualEvaluationDto>.Success(await service.SaveManualAsync(request, ct)));
    [HttpGet("manual-evaluations/{id:int}/history")]
    public async Task<IActionResult> ManualHistory(int id, CancellationToken ct) => Ok(ApiResponse<IReadOnlyList<ManualEvaluationHistoryDto>>.Success(await service.ManualHistoryAsync(id, ct)));
    [HttpGet("exports/{format}")]
    public async Task<IActionResult> Export(string format, [FromQuery] ReadinessFilter filter, CancellationToken ct)
    {
        var result = await exports.ExportAsync(format, filter, ct);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(result.Content, result.ContentType, result.FileName);
    }
}
