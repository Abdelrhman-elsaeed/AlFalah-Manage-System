using AlFalah.Application.Storage;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AlFalah.Api.Controllers;

[ApiController, Authorize, Route("api/v1/storage"), EnableRateLimiting("teacher-drive")]
public sealed class StorageEvidenceController(IRequirementCatalogService catalog, IEvidenceLinkService links,
    IEvidenceReviewService reviews, IFileChangeRequestService changes) : ControllerBase
{
    [HttpGet("academic-years")]
    public async Task<IActionResult> Years(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<AlFalah.Application.DTOs.EvidenceMatrix.AcademicYearDto>>.Success(await catalog.YearsAsync(ct)));
    [HttpGet("evidence-teachers")]
    public async Task<IActionResult> Teachers(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<EvidenceTeacherDto>>.Success(await catalog.TeachersAsync(ct)));
    [HttpGet("requirement-catalog")]
    public async Task<IActionResult> Catalog([FromQuery] int academicYearId, [FromQuery] string? search, CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<RequirementDto>>.Success(await catalog.ListAsync(academicYearId, search, ct)));
    [HttpPost("requirements/initialize")]
    public async Task<IActionResult> Initialize([FromQuery] int academicYearId, CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<RequirementDto>>.Success(await catalog.InitializeAsync(academicYearId, ct)));
    [HttpPatch("requirements/{id:int}")]
    public async Task<IActionResult> Configure(int id, ConfigureRequirementRequest request, CancellationToken ct) =>
        Ok(ApiResponse<RequirementDto>.Success(await catalog.ConfigureAsync(id, request, ct)));
    [HttpPost("files/{id:int}/links")]
    public async Task<IActionResult> Create(int id, CreateEvidenceLinkRequest request, CancellationToken ct) =>
        Ok(ApiResponse<EvidenceLinkDto>.Success(await links.CreateAsync(id, request, ct)));
    [HttpGet("files/{id:int}/links")]
    public async Task<IActionResult> FileLinks(int id, CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<EvidenceLinkDto>>.Success(await links.FileLinksAsync(id, ct: ct)));
    [HttpGet("me/files/{id:int}/links")]
    public async Task<IActionResult> OwnLinks(int id, CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<EvidenceLinkDto>>.Success(await links.FileLinksAsync(id, true, ct)));
    [HttpGet("requirements/{id:int}/links")]
    public async Task<IActionResult> RequirementLinks(int id, [FromQuery] EvidenceQueueRequest request, CancellationToken ct) =>
        Ok(ApiResponse<StoragePage<EvidenceLinkDto>>.Success(await links.QueueAsync(request with { RequirementId = id }, ct)));
    [HttpGet("review-queue")]
    public async Task<IActionResult> Queue([FromQuery] EvidenceQueueRequest request, CancellationToken ct) =>
        Ok(ApiResponse<StoragePage<EvidenceLinkDto>>.Success(await links.QueueAsync(request, ct)));
    [HttpGet("evidence-counts")]
    public async Task<IActionResult> Counts([FromQuery] int academicYearId, [FromQuery] bool own, CancellationToken ct) =>
        Ok(ApiResponse<EvidenceCountsDto>.Success(await links.CountsAsync(academicYearId, own, ct)));
    [HttpPost("links/{id:int}/submit")]
    public async Task<IActionResult> Submit(int id, SubmitEvidenceLinkRequest request, CancellationToken ct) =>
        Ok(ApiResponse<EvidenceLinkDto>.Success(await links.SubmitAsync(id, request, ct)));
    [HttpPost("links/{id:int}/review")]
    public async Task<IActionResult> Review(int id, ReviewEvidenceLinkRequest request, CancellationToken ct) =>
        Ok(ApiResponse<EvidenceLinkDto>.Success(await reviews.ReviewAsync(id, request, ct)));
    [HttpPost("files/{id:int}/change-requests")]
    public async Task<IActionResult> RequestChange(int id, CreateFileChangeRequest request, CancellationToken ct) =>
        Ok(ApiResponse<FileChangeDto>.Success(await changes.CreateAsync(id, request, ct)));
    [HttpGet("files/{id:int}/history")]
    public async Task<IActionResult> FileHistory(int id, CancellationToken ct) =>
        Ok(ApiResponse<StorageFileDetailsDto>.Success(await changes.HistoryAsync(id, ct)));
    [HttpGet("change-queue")]
    public async Task<IActionResult> ChangeQueue([FromQuery] int academicYearId, [FromQuery] int page = 1, [FromQuery] string? status = "Pending", CancellationToken ct = default) =>
        Ok(ApiResponse<StoragePage<ChangeQueueItemDto>>.Success(await changes.QueueAsync(academicYearId, page, status, ct)));
    [HttpGet("files/{id:int}/change-requests")]
    public async Task<IActionResult> ChangeHistory(int id, CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<FileChangeDto>>.Success(await changes.ListAsync(id, ct)));
    [HttpPost("change-requests/{id:int}/review")]
    public async Task<IActionResult> DecideChange(int id, ReviewFileChangeRequest request, CancellationToken ct) =>
        Ok(ApiResponse<FileChangeDto>.Success(await changes.ReviewAsync(id, request, ct)));
    [HttpGet("files/{id:int}/versions/{version:int}/content")]
    public async Task<IActionResult> VersionContent(int id, int version, CancellationToken ct)
    {
        var content = await changes.VersionContentAsync(id, version, ct);
        Response.Headers.CacheControl = "private, no-store"; Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "sandbox";
        return File(content.Content, content.ContentType, content.FileName);
    }
}
