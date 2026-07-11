using AlFalah.Application.DTOs.Account;
using AlFalah.Application.Interfaces;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize] // Must be logged in
public class AccountController : ControllerBase
{
    private readonly IAccountService _accountService;
    private readonly ICurrentUserService _currentUser;

    public AccountController(IAccountService accountService, ICurrentUserService currentUser)
    {
        _accountService = accountService;
        _currentUser = currentUser;
    }

    [HttpGet("signature")]
    public async Task<ActionResult<ApiResponse<SignatureDto>>> GetSignature(CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<SignatureDto>.Fail("User not found in context"));

        var response = await _accountService.GetSignatureAsync(userId, cancellationToken);
        return Ok(response);
    }

    [HttpPut("signature")]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateSignature([FromBody] SignatureDto dto, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<bool>.Fail("User not found in context"));

        var response = await _accountService.UpdateSignatureAsync(userId, dto, cancellationToken);
        if (!response.IsSuccess)
            return BadRequest(response);

        return Ok(response);
    }
}
