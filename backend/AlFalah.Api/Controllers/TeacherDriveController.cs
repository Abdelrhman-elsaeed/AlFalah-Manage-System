using AlFalah.Application.DTOs.EvidenceMatrix;
using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AlFalah.Api.Controllers;

/// <summary>
/// The teacher's own evidence folder.
///
/// Authentication is the ordinary application session — there is no second sign-in any more.
/// Being authenticated is not sufficient though: every action resolves the caller to a
/// teacher and to that teacher's folder grant, so a signed-in user with no grant reaches
/// nothing at all.
/// </summary>
[ApiController]
[Route("api/v1/teacher-drive")]
[Authorize]
[EnableRateLimiting("teacher-drive")]
public sealed class TeacherDriveController : ControllerBase
{
    private readonly ITeacherDriveIdentityService _identity;
    private readonly IGoogleDriveBrowserService _browser;
    private readonly IGoogleDriveUploadService _uploads;
    private readonly IEvidenceSubmissionService _submissions;

    public TeacherDriveController(
        ITeacherDriveIdentityService identity,
        IGoogleDriveBrowserService browser,
        IGoogleDriveUploadService uploads,
        IEvidenceSubmissionService submissions)
    {
        _identity = identity;
        _browser = browser;
        _uploads = uploads;
        _submissions = submissions;
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken) =>
        Ok(ApiResponse<TeacherDriveStatusDto>.Success(await _identity.GetStatusAsync(cancellationToken)));

    [HttpGet("items")]
    public async Task<IActionResult> Items(
        [FromQuery] string? parentItemId,
        [FromQuery] string? search,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDirection,
        [FromQuery] string? pageToken,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<DriveItemsPageDto>.Success(
            await _browser.ListAsync(new(parentItemId, search, sortBy, sortDirection, pageToken), cancellationToken)));

    [HttpGet("items/{itemId}")]
    public async Task<IActionResult> Item(string itemId, CancellationToken cancellationToken) =>
        Ok(ApiResponse<DriveItemDto>.Success(await _browser.GetItemAsync(itemId, cancellationToken)));

    /// <summary>
    /// Streams the file's bytes through the API. Drive's own link cannot be used: the files
    /// belong to the school credential and the teacher has no Google session.
    /// </summary>
    [HttpGet("items/{itemId}/content")]
    public async Task<IActionResult> Content(string itemId, CancellationToken cancellationToken)
    {
        var file = await _browser.DownloadAsync(itemId, cancellationToken);
        // enableRangeProcessing is off deliberately: the upstream Drive stream is forward-only.
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpGet("breadcrumb/{itemId?}")]
    public async Task<IActionResult> Breadcrumb(string? itemId, CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<DriveBreadcrumbDto>>.Success(await _browser.GetBreadcrumbAsync(itemId, cancellationToken)));

    [HttpGet("recent-files")]
    public async Task<IActionResult> Recent(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<RecentFileDto>>.Success(await _browser.GetRecentAsync(cancellationToken)));

    [HttpGet("evidence-tasks")]
    public async Task<IActionResult> EvidenceTasks(CancellationToken cancellationToken) =>
        Ok(ApiResponse<EvidenceUploadCatalogDto>.Success(await _submissions.GetUploadCatalogAsync(cancellationToken)));

    [HttpPost("uploads")]
    [RequestSizeLimit(262_144_000)]
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile? file,
        [FromForm] string? parentItemId,
        [FromForm] int? taskId,
        CancellationToken cancellationToken)
    {
        if (file is null) return BadRequest(ApiResponse.Fail("لم يتم اختيار ملف."));
        if (!taskId.HasValue) return BadRequest(ApiResponse.Fail("يجب اختيار المهمة قبل رفع الملف."));
        // The idempotency key is what makes a retried upload safe: without it a flaky network
        // would put the same evidence in Drive twice.
        var requestId = Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(requestId)) return BadRequest(ApiResponse.Fail("معرّف طلب الرفع مطلوب."));

        await using var stream = file.OpenReadStream();
        var result = await _uploads.UploadAsync(
            new(stream, file.FileName, file.ContentType, file.Length, parentItemId, taskId.Value, requestId), cancellationToken);
        return Ok(ApiResponse<UploadFileResultDto>.Success(result, "تم رفع الملف وتحديث المصفوفة."));
    }

    [HttpDelete("submissions/{submissionId:long}")]
    public async Task<IActionResult> Delete(long submissionId, CancellationToken cancellationToken)
    {
        await _uploads.DeleteAsync(submissionId, cancellationToken);
        return Ok(ApiResponse.Success("تم حذف الملف وتحديث حالة المهمة."));
    }
}
