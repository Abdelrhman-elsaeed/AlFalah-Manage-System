using AlFalah.Application.Common;
using AlFalah.Application.DTOs.StudentAnalyzer;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AlFalah.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/student-analyzer")]
public sealed class StudentAnalyzerController : ControllerBase
{
    private readonly IStudentAnalyzerService _service;

    public StudentAnalyzerController(IStudentAnalyzerService service) => _service = service;

    [HttpGet("capabilities")]
    public async Task<IActionResult> Capabilities(CancellationToken cancellationToken) =>
        Ok(ApiResponse<StudentAnalyzerCapabilitiesDto>.Success(
            await _service.GetCapabilitiesAsync(cancellationToken)));

    [HttpGet("delegates")]
    public async Task<IActionResult> Delegates(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<StudentAnalyzerDelegateDto>>.Success(
            await _service.GetDelegatesAsync(cancellationToken)));

    [HttpPut("delegates")]
    public async Task<IActionResult> UpdateDelegates(
        [FromBody] UpdateStudentAnalyzerGrantsRequest request,
        CancellationToken cancellationToken)
    {
        var errors = await ValidationHelper.ValidateAsync(HttpContext.RequestServices, request, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<IReadOnlyList<StudentAnalyzerDelegateDto>>.Fail(errors));
        var result = await _service.UpdateDelegatesAsync(request, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<StudentAnalyzerDelegateDto>>.Success(result, "تم تحديث المستخدمين المفوّضين."));
    }

    [HttpGet("settings")]
    public async Task<IActionResult> Settings(CancellationToken cancellationToken) =>
        Ok(ApiResponse<StudentAnalyzerSettingsDto>.Success(
            await _service.GetSettingsAsync(cancellationToken)));

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings(
        [FromBody] UpdateStudentAnalyzerSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var errors = await ValidationHelper.ValidateAsync(HttpContext.RequestServices, request, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<StudentAnalyzerSettingsDto>.Fail(errors));
        var result = await _service.UpdateSettingsAsync(request, cancellationToken);
        return Ok(ApiResponse<StudentAnalyzerSettingsDto>.Success(result, "تم حفظ إعدادات مزود الذكاء الاصطناعي بأمان."));
    }

    [HttpGet("models")]
    public async Task<IActionResult> Models(
        [FromQuery] StudentAnalyzerProvider provider,
        [FromHeader(Name = "X-Provider-Api-Key")] string? providerApiKey,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<StudentAnalyzerModelDto>>.Success(
            await _service.GetModelsAsync(provider, providerApiKey, cancellationToken)));

    [HttpPost("files")]
    [RequestSizeLimit(50L * 1024 * 1024 + 1024 * 1024)]
    public async Task<IActionResult> UploadFile(
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<StudentAnalyzerStoredFileDto>.Fail("اختر ملفًا صالحًا."));
        await using var stream = file.OpenReadStream();
        var result = await _service.UploadFileAsync(new StudentAnalyzerUpload(
            file.FileName,
            file.ContentType,
            file.Length,
            stream), cancellationToken);
        return StatusCode(201, ApiResponse<StudentAnalyzerStoredFileDto>.Success(result, "تم حفظ الملف وبدأت تهيئته للتحليل."));
    }

    [HttpGet("files")]
    public async Task<IActionResult> Files(
        [FromQuery] StudentAnalyzerFileQuery query,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<PagedResult<StudentAnalyzerFileListItemDto>>.Success(
            await _service.GetFilesAsync(query, cancellationToken)));

    [HttpGet("files/{id:int}/content")]
    public async Task<IActionResult> FileContent(int id, CancellationToken cancellationToken)
    {
        var file = await _service.GetFileContentAsync(id, cancellationToken);
        return File(file.Bytes, file.ContentType, file.FileName);
    }

    [HttpDelete("files/{id:int}")]
    public async Task<IActionResult> DeleteFile(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteFileAsync(id, cancellationToken);
        return Ok(ApiResponse.Success("تم حذف الملف وكل التحليلات المرتبطة به."));
    }

    [HttpPost("analyses")]
    [EnableRateLimiting("student-analyzer-ai")]
    public async Task<IActionResult> Analyze(
        [FromBody] AnalyzeStudentRequest request,
        CancellationToken cancellationToken)
    {
        var errors = await ValidationHelper.ValidateAsync(HttpContext.RequestServices, request, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<StudentAnalyzerAnalysisDto>.Fail(errors));
        var result = await _service.AnalyzeAsync(request, cancellationToken);
        return StatusCode(201, ApiResponse<StudentAnalyzerAnalysisDto>.Success(result, "تم إنشاء التحليل وحفظ التقرير."));
    }

    [HttpGet("analyses")]
    public async Task<IActionResult> Analyses(
        [FromQuery] StudentAnalyzerReportQuery query,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<PagedResult<StudentAnalyzerReportListItemDto>>.Success(
            await _service.GetReportsAsync(query, cancellationToken)));

    [HttpGet("analyses/{id:int}")]
    public async Task<IActionResult> Analysis(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<StudentAnalyzerAnalysisDto>.Success(
            await _service.GetReportAsync(id, cancellationToken)));

    [HttpDelete("analyses/{id:int}")]
    public async Task<IActionResult> DeleteAnalysis(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteReportAsync(id, cancellationToken);
        return Ok(ApiResponse.Success("تم حذف تقرير التحليل."));
    }
}
