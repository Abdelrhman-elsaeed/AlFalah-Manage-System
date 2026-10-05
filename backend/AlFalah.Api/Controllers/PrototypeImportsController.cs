using AlFalah.Application.Storage;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AlFalah.Api.Controllers;

[ApiController, Authorize, Route("api/v1/storage/imports"), EnableRateLimiting("teacher-drive")]
public sealed class PrototypeImportsController(IPrototypeImportService service) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => Ok(ApiResponse<IReadOnlyList<ImportBatchDto>>.Success(await service.ListAsync(ct)));
    [HttpPost("preview"), RequestSizeLimit(PrototypeImportParser.MaxBytes + 1048576), RequestFormLimits(MultipartBodyLengthLimit = PrototypeImportParser.MaxBytes)]
    public async Task<IActionResult> Preview([FromForm] IFormFile file, [FromForm] int academicYearId, [FromForm] int templateVersion, [FromForm] string sourceVersion, CancellationToken ct)
    {
        await using var content = file.OpenReadStream();
        return Ok(ApiResponse<ImportBatchDto>.Success(await service.PreviewAsync(new(academicYearId, templateVersion, sourceVersion, file.FileName, content), ct)));
    }
    [HttpGet("{id:int}")] public async Task<IActionResult> Get(int id, CancellationToken ct) => Ok(ApiResponse<ImportBatchDto>.Success(await service.GetAsync(id, ct)));
    [HttpGet("{id:int}/rows")] public async Task<IActionResult> Rows(int id, [FromQuery] int page = 1, [FromQuery] string? classification = null, CancellationToken ct = default) =>
        Ok(ApiResponse<StoragePage<ImportRowDto>>.Success(await service.RowsAsync(id, page, classification, ct)));
    [HttpPatch("{id:int}/rows/{row:int}")] public async Task<IActionResult> Resolve(int id, int row, ImportResolveRequest request, CancellationToken ct) => Ok(ApiResponse<ImportBatchDto>.Success(await service.ResolveAsync(id, row, request, ct)));
    [HttpPost("{id:int}/review")] public async Task<IActionResult> Review(int id, ImportReviewRequest request, CancellationToken ct) => Ok(ApiResponse<ImportBatchDto>.Success(await service.ReviewAsync(id, request, ct)));
    [HttpPost("{id:int}/commit")] public async Task<IActionResult> Commit(int id, ImportReviewRequest request, CancellationToken ct) => Ok(ApiResponse<ImportBatchDto>.Success(await service.CommitAsync(id, request, ct)));
    [HttpGet("{id:int}/exceptions.csv")] public async Task<IActionResult> Export(int id, CancellationToken ct) => File(await service.ExportAsync(id, ct), "text/csv; charset=utf-8", $"import-{id}-exceptions.csv");
    [HttpPost("{id:int}/rows/{row:int}/bytes"), RequestSizeLimit(ValidatedStorageUpload.MaxFileBytes + 1048576), RequestFormLimits(MultipartBodyLengthLimit = ValidatedStorageUpload.MaxFileBytes)]
    public async Task<IActionResult> Bytes(int id, int row, [FromForm] IFormFile file, [FromForm] string rowVersion, [FromForm] string reason, CancellationToken ct)
    {
        await using var content = file.OpenReadStream();
        return Ok(ApiResponse<ImportRowDto>.Success(await service.BytesAsync(id, row, new(content, file.FileName, file.Length, rowVersion, reason), ct)));
    }
    [HttpPost("{id:int}/rows/{row:int}/reconcile")] public async Task<IActionResult> Reconcile(int id, int row, CancellationToken ct) => Ok(ApiResponse<ImportRowDto>.Success(await service.ReconcileAsync(id, row, ct)));
}
