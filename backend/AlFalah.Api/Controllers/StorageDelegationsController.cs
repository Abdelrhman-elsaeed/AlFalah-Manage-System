using AlFalah.Application.Storage;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/storage/delegations")]
public sealed class StorageDelegationsController(IStorageDelegationService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<StorageDelegationDto>>.Success(await service.ListAsync(ct)));

    [HttpGet("candidates")]
    public async Task<IActionResult> Candidates(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<StorageDelegationCandidateDto>>.Success(await service.CandidatesAsync(ct)));

    [HttpPost]
    public async Task<IActionResult> Grant(GrantStorageDelegationRequest request, CancellationToken ct) =>
        Ok(ApiResponse<StorageDelegationDto>.Success(await service.GrantAsync(request, ct), "تم منح التفويض."));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Revoke(int id, RevokeStorageDelegationRequest request, CancellationToken ct) =>
        Ok(ApiResponse<StorageDelegationDto>.Success(await service.RevokeAsync(id, request, ct), "تم سحب التفويض."));
}
