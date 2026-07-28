using AlFalah.Application.DTOs.Auth;
using AlFalah.Application.Common;
using AlFalah.Application.DTOs.Schools;
using AlFalah.Application.Interfaces;
using AlFalah.Infrastructure.Services;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

/// <summary>
/// Authentication endpoints.
/// POST /api/v1/auth/school-login
/// POST /api/v1/auth/main-manager-login
/// POST /api/v1/auth/refresh
/// POST /api/v1/auth/logout
/// GET  /api/v1/auth/me
/// POST /api/v1/auth/forgot-password
/// POST /api/v1/auth/reset-password
/// GET  /api/v1/auth/schools
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ICurrentUserService _currentUser;
    private readonly SchoolLookupService _schoolLookup;

    public AuthController(
        IAuthService authService,
        ICurrentUserService currentUser,
        SchoolLookupService schoolLookup)
    {
        _authService = authService;
        _currentUser = currentUser;
        _schoolLookup = schoolLookup;
    }

    // ─── School User Login ────────────────────────────────────────────────────

    /// <summary>
    /// Login for school-scoped users (School Manager, Moderator, Instructor).
    /// User must select a school first. Backend validates the user is assigned to that school.
    /// </summary>
    [HttpPost("school-login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 401)]
    public async Task<IActionResult> SchoolLogin([FromBody] SchoolLoginRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse<AuthResponseDto>.Fail(errors));
        }

        var ipAddress = GetIpAddress();
        var userAgent = Request.Headers.UserAgent.ToString();

        var result = await _authService.SchoolLoginAsync(request, ipAddress, userAgent);
        return Ok(ApiResponse<AuthResponseDto>.Success(result, "تم تسجيل الدخول بنجاح."));
    }

    // ─── Main Manager Login ───────────────────────────────────────────────────

    /// <summary>
    /// Login for Main Manager and Super Admin (global scope, no school selection).
    /// </summary>
    [HttpPost("main-manager-login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 401)]
    public async Task<IActionResult> MainManagerLogin([FromBody] MainManagerLoginRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse<AuthResponseDto>.Fail(errors));
        }

        var ipAddress = GetIpAddress();
        var userAgent = Request.Headers.UserAgent.ToString();

        var result = await _authService.MainManagerLoginAsync(request, ipAddress, userAgent);
        return Ok(ApiResponse<AuthResponseDto>.Success(result, "تم تسجيل الدخول بنجاح."));
    }

    // ─── Refresh Token ────────────────────────────────────────────────────────

    /// <summary>
    /// Refresh an access token using a valid refresh token.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 401)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto request)
    {
        var ipAddress = GetIpAddress();
        var userAgent = Request.Headers.UserAgent.ToString();

        var result = await _authService.RefreshTokenAsync(request.RefreshToken, ipAddress, userAgent);
        return Ok(ApiResponse<AuthResponseDto>.Success(result, "تم تجديد الرمز بنجاح."));
    }

    // ─── Logout ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Revoke the current refresh token (logout).
    /// </summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDto request)
    {
        await _authService.LogoutAsync(request.RefreshToken);
        return Ok(ApiResponse.Success("تم تسجيل الخروج بنجاح."));
    }

    // ─── Current User ─────────────────────────────────────────────────────────

    /// <summary>
    /// Get the currently authenticated user's profile and permissions.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Me()
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse.Fail("المستخدم غير مصرح له."));

        var result = await _authService.GetCurrentUserAsync(userId);
        return Ok(ApiResponse<CurrentUserDto>.Success(result));
    }

    // ─── Forgot / Reset Password ──────────────────────────────────────────────

    /// <summary>
    /// Initiate password reset for a username.
    /// Always returns a generic success message regardless of whether the
    /// user exists (prevents user enumeration).
    /// In Development the reset token is included in the response payload
    /// so the client can complete the flow without an email gateway.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object?>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse<object?>.Fail(errors));
        }

        var token = await _authService.ForgotPasswordAsync(request.Username);

        // Generic success — never reveal whether the user exists.
        // In dev, the token is returned in `data` to make the flow testable.
        return Ok(ApiResponse<object?>.Success(
            new { resetToken = token },
            "إذا كان الحساب موجوداً، فسيتم إرسال تعليمات إعادة تعيين كلمة المرور."));
    }

    /// <summary>
    /// Complete the password reset using the token issued by /forgot-password.
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse.Fail(errors));
        }

        await _authService.ResetPasswordAsync(request.Username, request.Token, request.NewPassword);
        return Ok(ApiResponse.Success("تم تغيير كلمة المرور بنجاح."));
    }

    // ─── Schools Lookup ───────────────────────────────────────────────────────

    /// <summary>
    /// Get active schools for the login school selector.
    /// Public endpoint — no auth required.
    /// </summary>
    /// <summary>Change the current user's password.</summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequestDto request,
        CancellationToken cancellationToken)
    {
        var errors = await ValidationHelper.ValidateAsync(HttpContext.RequestServices, request, cancellationToken);
        if (errors.Count > 0)
            return BadRequest(ApiResponse.Fail(errors));

        if (string.IsNullOrWhiteSpace(_currentUser.UserId))
            return Unauthorized(ApiResponse.Fail("المستخدم غير مصرح له."));

        await _authService.ChangePasswordAsync(_currentUser.UserId!, request.CurrentPassword, request.NewPassword);
        return Ok(ApiResponse.Success("تم تغيير كلمة المرور بنجاح."));
    }

    [HttpGet("schools")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<SchoolLookupDto>>), 200)]
    public async Task<IActionResult> GetSchools()
    {
        var schools = await _schoolLookup.GetActiveSchoolsAsync();
        var result = schools.Select(s => new SchoolLookupDto
        {
            Id = s.Id,
            Name = s.Name,
            City = s.City,
            Stage = s.Stage.ToString(),
            LogoUrl = s.LogoUrl
        }).ToList();

        return Ok(ApiResponse<List<SchoolLookupDto>>.Success(result));
    }

    // ─── Private Helpers ──────────────────────────────────────────────────────

    private string? GetIpAddress()
    {
        if (Request.Headers.ContainsKey("X-Forwarded-For"))
            return Request.Headers["X-Forwarded-For"];

        return HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString();
    }
}
