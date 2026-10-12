using AlFalah.Application.DTOs.Dashboards;
using AlFalah.Application.Interfaces;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/dashboard/school-manager/operations")]
public sealed class SchoolManagerOperationsController(ISchoolManagerOperationsService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Ok(ApiResponse<SchoolManagerOperationsDto>.Success(await service.GetAsync(cancellationToken)));
}
