using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.IntelligentTimetable;

[ApiController, Authorize, Route("api/v1/timetables/{id:int}/standby")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class WeeklyStandbyController(WeeklyStandbyService service) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> Get(int id, CancellationToken ct) => Respond(() => service.GetAsync(id, ct));
    [HttpPost("preview")]
    public Task<IActionResult> Preview(int id, WeeklyStandbyRequest request, CancellationToken ct) => Respond(() => service.PreviewAsync(id, request, ct));
    [HttpPut]
    public Task<IActionResult> Apply(int id, WeeklyStandbyRequest request, CancellationToken ct) => Respond(() => service.ApplyAsync(id, request, ct));
    private async Task<IActionResult> Respond<T>(Func<Task<T>> action)
    {
        try { return Ok(ApiResponse<T>.Success(await action())); }
        catch (UnauthorizedAccessException) { return StatusCode(403, ApiResponse<T>.Fail("ليس لديك صلاحية إدارة الانتظار في هذه المدرسة.")); }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<T>.Fail(ex.Message)); }
        catch (BellScheduleConflictException ex) { return Conflict(ApiResponse<T>.Fail(ex.Message)); }
        catch (ArgumentException ex) { return BadRequest(ApiResponse<T>.Fail(ex.Message)); }
    }
}
