using AlFalah.Application.Duty;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AlFalah.Api.Controllers;
[Authorize,ApiController,Route("api/v1/school-duty/administrators")]
public sealed class DutyAdministrationController(IDutyAdministrationService service):ControllerBase
{
    [HttpGet]public async Task<IActionResult> Accounts(CancellationToken ct)=>Ok(ApiResponse<IReadOnlyList<DutyAdministratorAccount>>.Success(await service.AccountsAsync(ct)));
    [HttpPut("{userId}")]public async Task<IActionResult> Change(string userId,DutyAdministratorChange request,CancellationToken ct)=>Ok(ApiResponse<IReadOnlyList<DutyAdministratorAccount>>.Success(await service.ChangeAsync(userId,request,ct)));
}
