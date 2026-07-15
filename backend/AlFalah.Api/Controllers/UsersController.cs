using AlFalah.Application.Common;
using AlFalah.Application.DTOs.Users;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

/// <summary>
/// Users management endpoints (Phase 2).
/// Every action is permission-gated. In particular, Instructor.View grants no
/// access here; the Moderator's school-scoped teacher read lives only under
/// TeachersController.
/// </summary>
[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ICurrentUserService _currentUser;

    public UsersController(IUserService userService, ICurrentUserService currentUser)
    {
        _userService = userService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<UserListItemDto>>), 200)]
    public async Task<IActionResult> List([FromQuery] UserListQuery query, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.UserView))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لعرض دليل المستخدمين."));

        var errors = await ValidationHelper.ValidateAsync(HttpContext.RequestServices, query, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<PagedResult<UserListItemDto>>.Fail(errors));

        var result = await _userService.GetPagedAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<UserListItemDto>>.Success(result));
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<UserDetailDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.UserView))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لعرض بيانات المستخدمين."));

        var result = await _userService.GetByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<UserDetailDto>.Success(result));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<UserDetailDto>), 201)]
    [ProducesResponseType(typeof(ApiResponse<UserDetailDto>), 400)]
    public async Task<IActionResult> Create([FromBody] UserCreateRequestDto request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.UserCreate))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لإضافة مستخدم."));

        var errors = await ValidationHelper.ValidateAsync(HttpContext.RequestServices, request, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<UserDetailDto>.Fail(errors));

        var result = await _userService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.UserId }, ApiResponse<UserDetailDto>.Success(result, "تم إنشاء المستخدم بنجاح."));
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<UserDetailDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<UserDetailDto>), 400)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> Update(string id, [FromBody] UserUpdateRequestDto request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.UserEdit))
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لتعديل بيانات المستخدمين."));

        var errors = await ValidationHelper.ValidateAsync(HttpContext.RequestServices, request, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<UserDetailDto>.Fail(errors));

        var result = await _userService.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponse<UserDetailDto>.Success(result, "تم تحديث المستخدم بنجاح."));
    }

    [HttpPost("{id}/deactivate")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> Deactivate(string id, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.UserDelete)
            && !_currentUser.HasPermission(PermissionNames.InstructorDelete))
        {
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية لتعطيل المستخدمين."));
        }

        await _userService.DeactivateAsync(id, cancellationToken);
        return Ok(ApiResponse.Success("تم تعطيل المستخدم بنجاح."));
    }
}
