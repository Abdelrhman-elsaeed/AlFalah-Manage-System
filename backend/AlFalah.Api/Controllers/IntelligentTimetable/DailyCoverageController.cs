using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.IntelligentTimetable;

[ApiController, Authorize, Route("api/v1/timetable-coverage")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DailyCoverageController(DailyCoverageService daily, CoverageExecutionService execution) : ControllerBase
{
    [HttpGet("board")]
    public Task<IActionResult> Board(DateOnly date, CancellationToken ct) => Respond(() => daily.BoardAsync(date, ct));
    [HttpPost("reconcile")]
    public Task<IActionResult> Reconcile(DateOnly date, CancellationToken ct) => Respond(() => daily.RefreshAsync(date, ct));
    [HttpGet("needs/{id:int}/options")]
    public Task<IActionResult> Options(int id, CancellationToken ct) => Respond(() => daily.OptionsAsync(id, ct));
    [HttpPost("preview")]
    public Task<IActionResult> Preview(CoveragePreviewRequest request, CancellationToken ct) => Respond(() => daily.PreviewAsync(request, ct));
    [HttpPost("approve")]
    public Task<IActionResult> Approve(CoverageApproveRequest request, CancellationToken ct) => Respond(() => daily.ApproveAsync(request, ct));
    [HttpGet("my-tasks")]
    public Task<IActionResult> Tasks(CancellationToken ct, int page = 1, int pageSize = 20) => Respond(() => execution.MyTasksAsync(page, pageSize, ct));
    [HttpGet("tasks/{id:int}")]
    public Task<IActionResult> Detail(int id, CancellationToken ct) => Respond(() => execution.DetailAsync(id, ct));
    [HttpPut("tasks/{id:int}/draft")]
    public Task<IActionResult> Draft(int id, CoverageDraftRequest request, CancellationToken ct) => Respond(() => execution.SaveDraftAsync(id, request, ct));
    [HttpPost("tasks/{id:int}/acknowledge")]
    public Task<IActionResult> Acknowledge(int id, CoverageActionRequest request, CancellationToken ct) => Respond(() => execution.ActAsync(id, "Acknowledgement", request, ct));
    [HttpPost("tasks/{id:int}/submit")]
    public Task<IActionResult> Submit(int id, CoverageActionRequest request, CancellationToken ct) => Respond(() => execution.ActAsync(id, "Execution", request, ct));
    [HttpPost("tasks/{id:int}/unable")]
    public Task<IActionResult> Unable(int id, CoverageActionRequest request, CancellationToken ct) => Respond(() => execution.ActAsync(id, "Unable", request, ct));
    [HttpPost("needs/{id:int}/reopen")]
    public Task<IActionResult> Reopen(int id, CoverageActionRequest request, CancellationToken ct) => Respond(() => execution.ActAsync(id, "Reopen", request, ct));
    [HttpPost("needs/{id:int}/keep")]
    public Task<IActionResult> Keep(int id, CoverageActionRequest request, CancellationToken ct) => Respond(() => execution.ActAsync(id, "Keep", request, ct));
    [HttpPost("needs/{id:int}/release")]
    public Task<IActionResult> Release(int id, CoverageActionRequest request, CancellationToken ct) => Respond(() => execution.ActAsync(id, "Release", request, ct));
    private async Task<IActionResult> Respond<T>(Func<Task<T>> action)
    {
        try { return Ok(ApiResponse<T>.Success(await action())); }
        catch (UnauthorizedAccessException) { return StatusCode(403, ApiResponse<T>.Fail("ليس لديك صلاحية هذا الإجراء أو لم يعد التكليف مسندًا إليك.")); }
        catch (KeyNotFoundException) { return NotFound(ApiResponse<T>.Fail("الحصة أو التكليف غير موجود في المدرسة النشطة.")); }
        catch (BellScheduleConflictException ex) { return Conflict(ApiResponse<T>.Fail(ex.Message)); }
        catch (ArgumentException ex) { return BadRequest(ApiResponse<T>.Fail(ex.Message)); }
    }
}
