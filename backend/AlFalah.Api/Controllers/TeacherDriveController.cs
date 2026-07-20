using AlFalah.Application.DTOs.EvidenceMatrix;
using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AlFalah.Api.Controllers;

[ApiController]
[Route("api/v1/teacher-drive")]
[Authorize(Policy = "TeacherOneDriveAccess")]
[EnableRateLimiting("teacher-drive")]
public sealed class TeacherDriveController : ControllerBase
{
    private readonly ITeacherMicrosoftAccountService _accounts;
    private readonly IOneDriveBrowserService _browser;
    private readonly IOneDriveUploadService _uploads;
    private readonly IEvidenceSubmissionService _submissions;

    public TeacherDriveController(
        ITeacherMicrosoftAccountService accounts,
        IOneDriveBrowserService browser,
        IOneDriveUploadService uploads,
        IEvidenceSubmissionService submissions)
    {
        _accounts = accounts;
        _browser = browser;
        _uploads = uploads;
        _submissions = submissions;
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken) =>
        Ok(ApiResponse<TeacherDriveStatusDto>.Success(await _accounts.GetStatusAsync(User, cancellationToken)));

    [HttpPost("link-account")]
    public async Task<IActionResult> LinkAccount(CancellationToken cancellationToken) =>
        Ok(ApiResponse<LinkMicrosoftAccountResultDto>.Success(await _accounts.LinkAsync(User, cancellationToken)));

    [HttpGet("items")]
    public async Task<IActionResult> Items([FromQuery] string? parentItemId, [FromQuery] string? search, [FromQuery] string? sortBy,
        [FromQuery] string? sortDirection, [FromQuery] string? pageToken, CancellationToken cancellationToken) =>
        Ok(ApiResponse<DriveItemsPageDto>.Success(await _browser.ListAsync(User, new(parentItemId, search, sortBy, sortDirection, pageToken), cancellationToken)));

    [HttpGet("items/{itemId}")]
    public async Task<IActionResult> Item(string itemId, CancellationToken cancellationToken) =>
        Ok(ApiResponse<DriveItemDto>.Success(await _browser.GetItemAsync(User, itemId, cancellationToken)));

    [HttpGet("items/{itemId}/preview")]
    public async Task<IActionResult> Preview(string itemId, CancellationToken cancellationToken) =>
        Ok(ApiResponse<FilePreviewDto>.Success(await _browser.GetPreviewAsync(User, itemId, cancellationToken)));

    [HttpGet("breadcrumb/{itemId?}")]
    public async Task<IActionResult> Breadcrumb(string? itemId, CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<DriveBreadcrumbDto>>.Success(await _browser.GetBreadcrumbAsync(User, itemId, cancellationToken)));

    [HttpGet("recent-files")]
    public async Task<IActionResult> Recent(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<RecentFileDto>>.Success(await _browser.GetRecentAsync(User, cancellationToken)));

    [HttpGet("evidence-tasks")]
    public async Task<IActionResult> EvidenceTasks(CancellationToken cancellationToken) =>
        Ok(ApiResponse<EvidenceUploadCatalogDto>.Success(await _submissions.GetUploadCatalogAsync(cancellationToken)));

    [HttpPost("uploads")]
    [RequestSizeLimit(262_144_000)]
    public async Task<IActionResult> Upload([FromForm] IFormFile? file, [FromForm] string? parentItemId, [FromForm] int? taskId, CancellationToken cancellationToken)
    {
        if (file is null) return BadRequest(ApiResponse.Fail("لم يتم اختيار ملف."));
        if (!taskId.HasValue) return BadRequest(ApiResponse.Fail("يجب اختيار المهمة قبل رفع الملف."));
        var requestId = Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(requestId)) return BadRequest(ApiResponse.Fail("معرّف طلب الرفع مطلوب."));

        await using var stream = file.OpenReadStream();
        var result = await _uploads.UploadAsync(User,
            new(stream, file.FileName, file.ContentType, file.Length, parentItemId, taskId.Value, requestId), cancellationToken);
        return Ok(ApiResponse<UploadFileResultDto>.Success(result, "تم رفع الملف وتحديث المصفوفة."));
    }

    [HttpDelete("submissions/{submissionId:long}")]
    public async Task<IActionResult> Delete(long submissionId, CancellationToken cancellationToken)
    {
        await _uploads.DeleteAsync(User, submissionId, cancellationToken);
        return Ok(ApiResponse.Success("تم حذف الملف وتحديث حالة المهمة."));
    }
}
