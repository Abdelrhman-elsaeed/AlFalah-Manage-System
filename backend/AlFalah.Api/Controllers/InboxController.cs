using AlFalah.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

[ApiController, Authorize, AlFalah.Api.Filters.ApiResponseValidation, Route("api/v1/inbox")]
public sealed class InboxController(InboxService inbox) : ControllerBase
{
    [HttpGet("state")]
    public async Task<IActionResult> State(CancellationToken ct) => Ok(await inbox.StateAsync(ct));

    [HttpGet("changes")]
    public async Task<IActionResult> Changes([FromQuery] string after, CancellationToken ct, [FromQuery] int limit = 100)
        => Ok(await inbox.ChangesAsync(after, limit, ct));

    [HttpGet("/api/v1/conversations/{id:int}/messages/window")]
    public async Task<IActionResult> Window(int id, CancellationToken ct, [FromQuery] string? before = null, [FromQuery] int limit = 50)
        => Ok(await inbox.WindowAsync(id, before, limit, ct));
}
