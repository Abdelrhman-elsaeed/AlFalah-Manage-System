using AlFalah.Application.Common;
using AlFalah.Application.DTOs.Schools;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

[ApiController]
[Route("api/v1/school-locations")]
[Authorize]
public sealed class SchoolLocationsController : ControllerBase
{
    private readonly ISchoolLocationService _service;

    public SchoolLocationsController(ISchoolLocationService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var locations = await _service.GetActiveAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SchoolLocationDto>>.Success(locations));
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.SuperAdmin + "," + RoleNames.MainManager)]
    public async Task<IActionResult> Create(
        [FromBody] SchoolLocationCreateRequestDto request,
        CancellationToken cancellationToken)
    {
        var errors = await ValidationHelper.ValidateAsync(HttpContext.RequestServices, request, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<SchoolLocationDto>.Fail(errors));

        var location = await _service.CreateAsync(request, cancellationToken);
        return Created($"/api/v1/school-locations/{location.Id}",
            ApiResponse<SchoolLocationDto>.Success(location, "تمت إضافة الموقع بنجاح."));
    }
}
