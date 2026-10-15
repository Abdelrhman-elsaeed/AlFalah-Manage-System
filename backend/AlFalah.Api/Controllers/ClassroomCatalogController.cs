using AlFalah.Application.Classrooms;
using AlFalah.Application.Common;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

[ApiController, Authorize, Route("api/v1/classroom-catalog")]
public sealed class ClassroomCatalogController(IClassroomCatalogService service) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> Options([FromQuery] int? academicYearId, [FromQuery] int page = 1,
        [FromQuery] bool includeInactive = false, CancellationToken ct = default, [FromQuery] DateOnly? onDate = null) => Respond(() => service.OptionsAsync(academicYearId, page, includeInactive, ct, onDate));
    [HttpPost("batch")]
    public Task<IActionResult> Create(ClassroomBatch request, CancellationToken ct) => Respond(() => service.CreateAsync(request, ct));
    [HttpPatch("{id:int}")]
    public Task<IActionResult> Update(int id, ClassroomEdit request, CancellationToken ct) => Respond(() => service.UpdateAsync(id, request, ct));
    private async Task<IActionResult> Respond<T>(Func<Task<T>> action)
    {
        try { return Ok(ApiResponse<T>.Success(await action())); }
        catch (UnauthorizedSchoolAccessException ex) { return StatusCode(403, ApiResponse<T>.Fail(ex.Message)); }
        catch (ClassroomCatalogConflictException ex) { return Conflict(ApiResponse<T>.Fail(ex.Message)); }
        catch (KeyNotFoundException) { return NotFound(ApiResponse<T>.Fail("المدرسة أو الفصل غير متاح.")); }
        catch (ArgumentException ex) { return BadRequest(ApiResponse<T>.Fail(ex.Message)); }
    }
}
