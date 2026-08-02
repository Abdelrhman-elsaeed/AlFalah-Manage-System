using AlFalah.Application.DTOs.Timetables;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/timetables")]
public sealed class SchoolTimetablesController : ControllerBase
{
    private readonly ISchoolTimetableService _service;

    public SchoolTimetablesController(ISchoolTimetableService service) => _service = service;

    [HttpGet("catalog")]
    public async Task<IActionResult> Catalog([FromQuery] int? schoolId, CancellationToken cancellationToken) =>
        Ok(ApiResponse<TimetableCatalogDto>.Success(await _service.GetCatalogAsync(schoolId, cancellationToken)));

    [HttpGet("current")]
    public async Task<IActionResult> Current(
        [FromQuery] int academicYearId,
        [FromQuery] TimetableSemester semester,
        [FromQuery] int? schoolId,
        CancellationToken cancellationToken)
    {
        var timetable = await _service.GetCurrentAsync(academicYearId, semester, schoolId, cancellationToken);
        return Ok(ApiResponse<SchoolTimetableDto?>.Success(timetable));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<SchoolTimetableDto>.Success(await _service.GetByIdAsync(id, cancellationToken)));

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateSchoolTimetableRequest request,
        [FromQuery] int? schoolId,
        CancellationToken cancellationToken)
    {
        var timetable = await _service.CreateAsync(request, schoolId, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = timetable.Id }, ApiResponse<SchoolTimetableDto>.Success(timetable, "تم إنشاء مسودة الجدول."));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Save(
        int id,
        [FromBody] SaveSchoolTimetableRequest request,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<SchoolTimetableDto>.Success(await _service.SaveAsync(id, request, cancellationToken), "تم حفظ الجدول وإنشاء نسخة جديدة."));

    [HttpPost("{id:int}/publish")]
    public async Task<IActionResult> Publish(
        int id,
        [FromBody] TimetableRevisionRequest request,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<SchoolTimetableDto>.Success(await _service.PublishAsync(id, request, cancellationToken), "تم نشر الجدول للمعلمين."));

    [HttpGet("{id:int}/versions")]
    public async Task<IActionResult> Versions(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<TimetableVersionDto>>.Success(await _service.GetVersionsAsync(id, cancellationToken)));

    [HttpPost("{id:int}/versions/{versionNumber:int}/restore")]
    public async Task<IActionResult> Restore(
        int id,
        int versionNumber,
        [FromBody] TimetableRevisionRequest request,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<SchoolTimetableDto>.Success(
            await _service.RestoreAsync(id, versionNumber, request, cancellationToken),
            "تم استرجاع النسخة وإنشاء نسخة حالية جديدة."));

    [HttpPut("editor-grants")]
    public async Task<IActionResult> UpdateGrants(
        [FromBody] UpdateTimetableGrantsRequest request,
        [FromQuery] int? schoolId,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<TimetableModeratorDto>>.Success(
            await _service.UpdateGrantsAsync(request, schoolId, cancellationToken),
            "تم تحديث المشرفين المفوّضين."));

    [HttpPost("{id:int}/import")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Import(
        int id,
        [FromForm] IFormFile file,
        [FromForm] int revision,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse.Fail("اختر ملف Excel صالحًا."));
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse.Fail("الملف يجب أن يكون بصيغة xlsx."));
        await using var stream = file.OpenReadStream();
        var result = await _service.ImportAsync(id, stream, revision, cancellationToken);
        return Ok(ApiResponse<TimetableImportResultDto>.Success(result, "تم استيراد الجدول وإنشاء نسخة جديدة."));
    }

    [HttpGet("{id:int}/import-template")]
    public async Task<IActionResult> ImportTemplate(int id, CancellationToken cancellationToken)
    {
        var file = await _service.BuildImportTemplateAsync(id, cancellationToken);
        return File(file.Bytes, file.ContentType, file.FileName);
    }

    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> Pdf(
        int id,
        [FromQuery] TimetablePdfColorMode colorMode = TimetablePdfColorMode.Color,
        CancellationToken cancellationToken = default)
    {
        var file = await _service.BuildPdfAsync(id, colorMode, cancellationToken);
        return File(file.Bytes, file.ContentType, file.FileName);
    }
}
