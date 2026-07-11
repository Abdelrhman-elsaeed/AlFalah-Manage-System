using AlFalah.Application.Common;
using AlFalah.Application.DTOs.UserSchoolRoles;
using AlFalah.Application.Interfaces;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

/// <summary>
/// UserSchoolRole assignment endpoints (Phase 2).
/// </summary>
[ApiController]
[Route("api/v1/user-school-roles")]
[Authorize]
public class UserSchoolRolesController : ControllerBase
{
    private readonly IUserSchoolRoleService _userSchoolRoleService;

    public UserSchoolRolesController(IUserSchoolRoleService userSchoolRoleService)
    {
        _userSchoolRoleService = userSchoolRoleService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<UserSchoolRoleDetailDto>), 201)]
    [ProducesResponseType(typeof(ApiResponse<UserSchoolRoleDetailDto>), 400)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> Create([FromBody] UserSchoolRoleCreateRequestDto request, CancellationToken cancellationToken)
    {
        var errors = await ValidationHelper.ValidateAsync(HttpContext.RequestServices, request, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<UserSchoolRoleDetailDto>.Fail(errors));

        var result = await _userSchoolRoleService.CreateAsync(request, cancellationToken);
        return StatusCode(201, ApiResponse<UserSchoolRoleDetailDto>.Success(result, "تم تعيين المستخدم للمدرسة بنجاح."));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _userSchoolRoleService.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse.Success("تم إلغاء تعيين المستخدم بنجاح."));
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UserSchoolRoleDetailDto>>), 200)]
    public async Task<IActionResult> GetBySchool([FromQuery] int? schoolId, CancellationToken cancellationToken)
    {
        var result = await _userSchoolRoleService.GetBySchoolAsync(schoolId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<UserSchoolRoleDetailDto>>.Success(result));
    }
}