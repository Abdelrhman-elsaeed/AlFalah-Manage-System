using AlFalah.Application.Interfaces;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

[ApiController]
[Authorize]
public sealed class AccountAccessController(IAccountAccessService service) : ControllerBase
{
    [HttpGet("api/v1/account/signature/requirement")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> SignatureRequirement(CancellationToken ct) =>
        Ok(ApiResponse<SignatureRequirementDto>.Success(await service.GetSignatureRequirementAsync(ct)));

    [HttpPost("api/v1/users/{id}/password/reveal")]
    public async Task<IActionResult> RevealPassword(string id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store, private";
        Response.Headers.Pragma = "no-cache";
        return Ok(ApiResponse<PasswordRevealDto>.Success(await service.RevealPasswordAsync(id, ct)));
    }
}
