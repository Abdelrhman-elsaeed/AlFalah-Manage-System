using AlFalah.Application.Notifications;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AlFalah.Api.Controllers;

[ApiController, Authorize, AlFalah.Api.Filters.ApiResponseValidation, Route("api/v1/push")]
public sealed class PushSubscriptionsController(PushService push) : ControllerBase
{
    [HttpGet("config")] public IActionResult Config() => Ok(ApiResponse<PushConfig>.Success(push.Config()));
    [HttpPost("subscriptions"), RequestSizeLimit(8192), EnableRateLimiting("push-registration")]
    public async Task<IActionResult> Register(RegisterPush request, CancellationToken ct)
    {
        var result = await push.RegisterAsync(request, ct); return result.IsSuccess ? Ok(result) : BadRequest(result);
    }
    [HttpGet("subscriptions")] public async Task<IActionResult> Devices(CancellationToken ct) => Ok(await push.DevicesAsync(ct));
    [HttpDelete("subscriptions/{id:guid}")] public async Task<IActionResult> Revoke(Guid id, CancellationToken ct) => Ok(await push.RevokeAsync(id, ct));
}
