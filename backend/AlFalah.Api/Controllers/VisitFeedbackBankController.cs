using AlFalah.Application.DTOs.Visits;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v2/visits/feedback-bank")]
public sealed class VisitFeedbackBankController(
    IVisitFeedbackBankService bank,
    ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(PermissionNames.VisitCreate) &&
            !currentUser.HasPermission(PermissionNames.VisitEdit)) return Forbidden();
        return Ok(ApiResponse<IReadOnlyList<VisitFeedbackTemplateDto>>.Success(
            await bank.ListAsync(cancellationToken)));
    }

    [HttpPost]
    public async Task<IActionResult> Create(SaveVisitFeedbackTemplateDto request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(PermissionNames.VisitEdit)) return Forbidden();
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<VisitFeedbackTemplateDto>.Success(await bank.CreateAsync(request, cancellationToken)));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SaveVisitFeedbackTemplateDto request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(PermissionNames.VisitEdit)) return Forbidden();
        return Ok(ApiResponse<VisitFeedbackTemplateDto>.Success(await bank.UpdateAsync(id, request, cancellationToken)));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(PermissionNames.VisitEdit)) return Forbidden();
        await bank.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse.Success("حُذفت العبارة من البنك، مع بقاء نصوص الزيارات المحفوظة."));
    }

    private ObjectResult Forbidden() => StatusCode(StatusCodes.Status403Forbidden,
        ApiResponse.Fail("ليس لديك صلاحية لإدارة بنك عبارات الزيارات."));
}
