using AlFalah.Application.Duty;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

[Authorize,ApiController,AlFalah.Api.Filters.ApiResponseValidation,Route("api/v1/school-duty")]
public sealed class DutyDocumentController(IDutyDocumentService service) : ControllerBase
{
    [HttpGet("assignments/{id:int}/documents")]
    public async Task<IActionResult> Documents(int id,CancellationToken ct)=>Ok(ApiResponse<DutyDocumentCollection>.Success(await service.ListAsync(id,ct)));
    [HttpGet("documents/{id:int}/pdf")]
    public async Task<IActionResult> Pdf(int id,[FromQuery] bool archived,CancellationToken ct)=>Content(await service.ContentAsync(id,archived,ct));
    [HttpGet("plans/roster.pdf")]
    public async Task<IActionResult> Roster([FromQuery]int termId,[FromQuery]int revision,CancellationToken ct)=>Content(await service.RosterAsync(termId,revision,ct));
    [HttpPost("documents/{id:int}/archive/retry")]
    public async Task<IActionResult> Retry(int id,CancellationToken ct){await service.RetryAsync(id,ct);return Accepted(ApiResponse<bool>.Success(true));}
    [HttpPut("archive/enablement")]
    public async Task<IActionResult> Enable([FromBody]DutyArchiveEnablement request,CancellationToken ct)=>Ok(ApiResponse<DutyArchiveReadiness>.Success(await service.EnableAsync(request.Enabled,ct)));
    private IActionResult Content(DutyPdf pdf)
    {
        Response.Headers.CacheControl="private, no-store";Response.Headers["X-Content-Type-Options"]="nosniff";
        Response.Headers["Content-Security-Policy"]="sandbox";Response.Headers["X-Document-SHA256"]=pdf.Hash;
        return File(pdf.Bytes,"application/pdf",pdf.FileName);
    }
}
public sealed record DutyArchiveEnablement(bool Enabled);
