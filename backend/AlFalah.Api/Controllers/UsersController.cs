using AlFalah.Application.Common;
using AlFalah.Application.DTOs.Users;
using AlFalah.Application.Interfaces;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

/// <summary>
/// Users management endpoints (Phase 2).
/// Scope: SchoolManager, Moderator, Instructor.
/// </summary>
[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<UserListItemDto>>), 200)]
    public async Task<IActionResult> List([FromQuery] UserListQuery query, CancellationToken cancellationToken)
    {
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
        var result = await _userService.GetByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<UserDetailDto>.Success(result));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<UserDetailDto>), 201)]
    [ProducesResponseType(typeof(ApiResponse<UserDetailDto>), 400)]
    public async Task<IActionResult> Create([FromBody] UserCreateRequestDto request, CancellationToken cancellationToken)
    {
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
        await _userService.DeactivateAsync(id, cancellationToken);
        return Ok(ApiResponse.Success("تم تعطيل المستخدم بنجاح."));
    }
}