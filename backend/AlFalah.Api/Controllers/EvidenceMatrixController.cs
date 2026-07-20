using AlFalah.Application.DTOs.EvidenceMatrix;
using AlFalah.Application.Interfaces;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

[ApiController]
[Route("api/v1/evidence-matrix")]
[Authorize]
public sealed class EvidenceMatrixController : ControllerBase
{
    private readonly IEvidenceMatrixService _matrix;

    public EvidenceMatrixController(IEvidenceMatrixService matrix) => _matrix = matrix;

    [HttpGet("academic-years")]
    public async Task<IActionResult> AcademicYears(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<AcademicYearDto>>.Success(await _matrix.GetAcademicYearsAsync(cancellationToken)));

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] EvidenceMatrixFilterDto filter, CancellationToken cancellationToken) =>
        Ok(ApiResponse<EvidenceMatrixDto>.Success(await _matrix.GetAsync(filter, cancellationToken)));

    [HttpGet("cells/{teacherId:int}/{taskId:int}")]
    public async Task<IActionResult> CellFiles(int teacherId, int taskId, [FromQuery] int academicYearId, CancellationToken cancellationToken) =>
        Ok(ApiResponse<EvidenceCellFilesDto>.Success(await _matrix.GetCellFilesAsync(teacherId, taskId, academicYearId, cancellationToken)));

    [HttpPost("submissions/{submissionId:long}/review")]
    public async Task<IActionResult> Review(long submissionId, [FromBody] ReviewEvidenceSubmissionRequest request, CancellationToken cancellationToken)
    {
        await _matrix.ReviewAsync(submissionId, request.ReviewStatus, request.Note, cancellationToken);
        return Ok(ApiResponse.Success("تم تحديث مراجعة الدليل."));
    }

    [HttpGet("export/excel")]
    public async Task<IActionResult> ExportExcel([FromQuery] EvidenceMatrixFilterDto filter, CancellationToken cancellationToken)
    {
        var result = await _matrix.ExportExcelAsync(filter, cancellationToken);
        return File(result.Bytes, result.ContentType, result.FileName);
    }

    [HttpGet("export/pdf")]
    public async Task<IActionResult> ExportPdf([FromQuery] EvidenceMatrixFilterDto filter, CancellationToken cancellationToken)
    {
        var result = await _matrix.ExportPdfAsync(filter, cancellationToken);
        return File(result.Bytes, result.ContentType, result.FileName);
    }
}
