using AlFalah.Application.Duty;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

[Authorize, ApiController, AlFalah.Api.Filters.ApiResponseValidation, Route("api/v1/school-duty")]
public sealed class DutyExecutionController(IDutyExecutionService service) : ControllerBase
{
    [HttpGet("assignments/{id:int}/execution")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) => Ok(ApiResponse<DutyExecution>.Success(await service.GetAsync(id, ct)));
    [HttpGet("students")]
    public async Task<IActionResult> Students([FromQuery] DutyRosterQuery query, CancellationToken ct) => Ok(ApiResponse<DutyRoster>.Success(await service.RosterAsync(query, ct)));
    [HttpPut("assignments/{id:int}/report/draft")]
    public async Task<IActionResult> Draft(int id, DutyDraftRequest request, CancellationToken ct) => Ok(ApiResponse<DutyExecution>.Success(await service.SaveDraftAsync(id, request, ct)));
    [HttpPost("assignments/{id:int}/acknowledge")]
    public async Task<IActionResult> Acknowledge(int id, DutySignRequest request, CancellationToken ct) => Ok(ApiResponse<DutyExecution>.Success(await service.AcknowledgeAsync(id, request, ct)));
    [HttpPost("assignments/{id:int}/report/submit")]
    public async Task<IActionResult> Submit(int id, DutySignRequest request, CancellationToken ct) => Ok(ApiResponse<DutyExecution>.Success(await service.SubmitAsync(id, request, ct)));
    [HttpPost("assignments/{id:int}/report/correction")]
    public async Task<IActionResult> Correct(int id, DutyCorrectionRequest request, CancellationToken ct) => Ok(ApiResponse<DutyExecution>.Success(await service.CorrectReportAsync(id, request, ct)));
    [HttpPost("assignments/{id:int}/dismissal-lateness")]
    public async Task<IActionResult> Late(int id, DutyLateRequest request, CancellationToken ct) => Ok(ApiResponse<DutyLateDto>.Success(await service.ConfirmLateAsync(id, request, ct)));
    [HttpPost("assignments/{id:int}/timing-exception")]
    public async Task<IActionResult> Timing(int id, DutyTimingException request, CancellationToken ct) => Ok(ApiResponse<DutyExecution>.Success(await service.TimingExceptionAsync(id, request, ct)));
    [HttpPost("dismissal-lateness/{id:int}/correction")]
    public async Task<IActionResult> CorrectLate(int id, DutyLateCorrection request, CancellationToken ct) => Ok(ApiResponse<DutyLateDto>.Success(await service.CorrectLateAsync(id, request, ct)));
    [HttpPost("dismissal-lateness/{id:int}/retry")]
    public async Task<IActionResult> Retry(int id, CancellationToken ct) { await service.RetryNoticesAsync(id, ct); return Ok(ApiResponse<bool>.Success(true)); }
    [HttpGet("dismissal-lateness")]
    public async Task<IActionResult> Statistics([FromQuery] DutyDismissalQuery query, CancellationToken ct) => Ok(ApiResponse<DutyDismissalStatistics>.Success(await service.StatisticsAsync(query, ct)));
    [HttpGet("guardian/notices")]
    public async Task<IActionResult> Guardian(CancellationToken ct) => Ok(ApiResponse<List<DutyGuardianNotice>>.Success(await service.GuardianNoticesAsync(ct)));
    [HttpPost("guardian/notices/{id:int}/read")]
    public async Task<IActionResult> Read(int id, CancellationToken ct) { await service.MarkGuardianNoticeReadAsync(id, ct); return Ok(ApiResponse<bool>.Success(true)); }
    [HttpGet("documents/{id:int}/snapshot")]
    public async Task<IActionResult> Snapshot(int id, CancellationToken ct) => Ok(ApiResponse<System.Text.Json.JsonElement>.Success(System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(await service.SignedSnapshotAsync(id, ct))));
}
