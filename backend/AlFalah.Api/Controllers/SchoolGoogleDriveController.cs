using AlFalah.Application.Common;
using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace AlFalah.Api.Controllers;

/// <summary>
/// School-manager setup for the school's Google Drive connection. Credentials are
/// write-only: the GET response reports whether one is stored, never any part of it.
///
/// The refresh token is no longer entered by hand — <c>auth-url</c> and <c>callback</c> run
/// the OAuth 2.0 authorization-code flow instead. A manager saves their OAuth client id and
/// secret through <see cref="Configure"/>, then completes consent once.
/// </summary>
[ApiController]
[Route("api/v1/school-google-drive")]
[Authorize]
public sealed class SchoolGoogleDriveController : ControllerBase
{
    private readonly ISchoolGoogleDriveService _service;
    private readonly IGoogleDriveOAuthService _oauth;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SchoolGoogleDriveController> _logger;

    public SchoolGoogleDriveController(
        ISchoolGoogleDriveService service,
        IGoogleDriveOAuthService oauth,
        IConfiguration configuration,
        ILogger<SchoolGoogleDriveController> logger)
    {
        _service = service;
        _oauth = oauth;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Ok(ApiResponse<SchoolGoogleDriveSettingsDto>.Success(await _service.GetForCurrentSchoolAsync(cancellationToken)));

    [HttpPut]
    public async Task<IActionResult> Configure(
        [FromBody] ConfigureSchoolGoogleDriveRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<SchoolGoogleDriveSettingsDto>.Success(
            await _service.ConfigureForCurrentSchoolAsync(request, cancellationToken),
            "تم إعداد حساب Google Drive الخاص بالمدرسة."));

    /// <summary>
    /// Returns the Google consent URL for the caller's school. The client should open it as a
    /// top-level navigation, not via fetch — Google's consent screen cannot be framed or
    /// requested cross-origin.
    /// </summary>
    [HttpGet("auth-url")]
    public async Task<IActionResult> GetAuthUrl(CancellationToken cancellationToken) =>
        Ok(ApiResponse<GoogleAuthUrlDto>.Success(await _oauth.GetAuthUrlForCurrentSchoolAsync(cancellationToken)));

    /// <summary>
    /// Google's redirect target. Anonymous by necessity: a redirect from an external origin
    /// carries neither the bearer token nor a cookie. Authorization comes entirely from the
    /// integrity-protected <paramref name="state"/>, which the service refuses to trust if it
    /// has been altered or has expired.
    /// </summary>
    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken cancellationToken)
    {
        // A browser is on the other end of this request, not an API client. Where a completion
        // URL is configured, both outcomes bounce back to the settings screen with a flag —
        // replacing the manager's page with a raw JSON envelope would look like a crash.
        var completionUri = _configuration["GoogleOAuth:CompletionRedirectUri"];
        try
        {
            // The manager pressed "cancel", or Google declined the request outright.
            if (!string.IsNullOrWhiteSpace(error))
                throw new InvalidOperationException($"لم تكتمل الموافقة على ربط Google Drive: {error}");

            var result = await _oauth.ExchangeAuthCodeAsync(code ?? string.Empty, state ?? string.Empty, cancellationToken);

            return string.IsNullOrWhiteSpace(completionUri)
                ? Ok(ApiResponse<GoogleOAuthConnectionResultDto>.Success(result, "تم ربط حساب Google Drive الخاص بالمدرسة."))
                : Redirect(QueryHelpers.AddQueryString(completionUri, "googleDrive", "connected"));
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedSchoolAccessException or KeyNotFoundException)
        {
            // The reason is logged rather than put in the URL: it is the administrator's
            // diagnostic, and query strings end up in browser history and proxy logs. With no
            // completion URL configured, rethrow so GlobalExceptionMiddleware maps the status.
            _logger.LogWarning(ex, "Google Drive OAuth callback did not complete.");
            if (string.IsNullOrWhiteSpace(completionUri)) throw;
            return Redirect(QueryHelpers.AddQueryString(completionUri, "googleDrive", "failed"));
        }
    }
}
