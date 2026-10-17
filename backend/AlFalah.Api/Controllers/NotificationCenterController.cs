using AlFalah.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

[ApiController, Authorize, AlFalah.Api.Filters.ApiResponseValidation, Route("api/v1/notification-center")]
public sealed class NotificationCenterController(CenterService center) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List([FromQuery] CenterQuery query, CancellationToken ct) => Ok(await center.ListAsync(query, ct));
    [HttpGet("state")] public async Task<IActionResult> State(CancellationToken ct) => Ok(await center.StateAsync(ct));
    [HttpPost("{id:guid}/read")] public async Task<IActionResult> Read(Guid id, CancellationToken ct) => Ok(await center.ReadAsync(id, ct));
    [HttpPost("read-all")] public async Task<IActionResult> ReadAll(CancellationToken ct) => Ok(await center.ReadAsync(null, ct));
    [HttpGet("{id:guid}/target")] public async Task<IActionResult> Target(Guid id, CancellationToken ct) => Ok(await center.TargetAsync(id, ct));
    [HttpGet("{id:guid}/detail")] public async Task<IActionResult> Detail(Guid id, CancellationToken ct) => Ok(await center.DetailAsync(id, ct));
}
