using System.Text.Json;
using System.Text.Json.Serialization;
using AlFalah.Application.Common;
using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// Drives the Google OAuth 2.0 authorization-code flow that replaces pasting a refresh token
/// by hand. A manager still supplies the school's own Google Cloud OAuth client — client id
/// and secret live per school on <see cref="SchoolGoogleDrive"/> — because each school owns
/// its own Google project. What this service removes is the one step a human should never do
/// manually: obtaining the refresh token.
///
/// The two halves have deliberately different trust models:
///   • <see cref="GetAuthUrlForCurrentSchoolAsync"/> runs inside an authenticated manager
///     request and mints a protected <c>state</c> naming the school and the manager.
///   • <see cref="ExchangeAuthCodeAsync"/> runs on Google's browser redirect, which carries no
///     JWT and no cookie. It therefore trusts NOTHING from the query string except what it can
///     unprotect out of that state — which is simultaneously the school binding and the CSRF
///     defence. Without it, anyone who could reach the callback could bind a refresh token
///     they control to another school's evidence storage.
/// </summary>
public sealed class GoogleDriveOAuthService : IGoogleDriveOAuthService
{
    /// <summary>
    /// Full Drive scope, matching <see cref="GoogleDriveTokenService"/>. Narrower scopes such
    /// as <c>drive.file</c> only ever see files this application created, which cannot browse
    /// an evidence tree an administrator provisioned by hand.
    /// </summary>
    private const string DriveScope = "https://www.googleapis.com/auth/drive";

    private const string DefaultAuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string DefaultTokenEndpoint = "https://oauth2.googleapis.com/token";

    /// <summary>
    /// Distinct from the credential protector's purpose on purpose: a state string is not a
    /// credential, and separate purposes mean a payload of one kind can never be substituted
    /// for the other. Must stay stable across releases — changing it invalidates in-flight
    /// consent round trips (harmless) but is still a behaviour change.
    /// </summary>
    private const string StatePurpose = "AlFalah.SchoolGoogleDrive.OAuthState.v1";

    /// <summary>
    /// Long enough for a manager to pick an account and read Google's consent screen, short
    /// enough that a leaked consent URL is not indefinitely replayable.
    /// </summary>
    private static readonly TimeSpan DefaultStateLifetime = TimeSpan.FromMinutes(15);

    private readonly AlFalahDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly SchoolScopeGuard _scopeGuard;
    private readonly AuditLogWriter _audit;
    private readonly GoogleDriveCredentialProtector _protector;
    private readonly IDataProtector _stateProtector;
    private readonly IGoogleDriveTokenService _tokens;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleDriveOAuthService> _logger;

    public GoogleDriveOAuthService(
        AlFalahDbContext context,
        ICurrentUserService currentUser,
        SchoolScopeGuard scopeGuard,
        AuditLogWriter audit,
        GoogleDriveCredentialProtector protector,
        IDataProtectionProvider dataProtectionProvider,
        IGoogleDriveTokenService tokens,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GoogleDriveOAuthService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _scopeGuard = scopeGuard;
        _audit = audit;
        _protector = protector;
        _stateProtector = dataProtectionProvider.CreateProtector(StatePurpose);
        _tokens = tokens;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<GoogleAuthUrlDto> GetAuthUrlForCurrentSchoolAsync(CancellationToken cancellationToken = default)
    {
        EnsureManager();
        var schoolId = ResolveSchoolId();

        var drive = await _context.SchoolGoogleDrives.AsNoTracking()
            .SingleOrDefaultAsync(x => x.SchoolId == schoolId, cancellationToken);

        // The client id is what identifies the school's Google project to the consent screen,
        // and the secret is needed to redeem the code moments later. Failing here — before the
        // manager leaves the application — is far clearer than failing on the callback, where
        // the only thing they would see is a bounce back with an error flag.
        var clientId = drive?.OAuthClientId?.Trim();
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(drive?.ProtectedOAuthClientSecret))
            throw new InvalidOperationException(
                "احفظ OAuth Client ID و Client Secret الخاصين بالمدرسة أولاً، ثم ابدأ ربط حساب Google Drive.");

        var expiresAtUtc = DateTimeOffset.UtcNow.Add(StateLifetime);
        var state = _stateProtector.Protect(JsonSerializer.Serialize(new OAuthState
        {
            SchoolId = schoolId,
            UserId = _currentUser.UserId,
            ExpiresAtUnixSeconds = expiresAtUtc.ToUnixTimeSeconds()
        }));

        var authorizationUrl = QueryHelpers.AddQueryString(AuthorizationEndpoint, new Dictionary<string, string?>
        {
            ["client_id"] = clientId,
            // Must match a redirect URI registered on the school's OAuth client verbatim, and
            // must match the value replayed during the exchange, or Google refuses both steps.
            ["redirect_uri"] = RedirectUri,
            ["response_type"] = "code",
            ["scope"] = DriveScope,
            // Both of these are load-bearing. access_type=offline is what asks for a refresh
            // token at all; prompt=consent forces Google to issue a NEW one even when this
            // manager has already granted the same client before — without it, a second
            // authorization returns an access token and no refresh_token, which is the classic
            // "it worked once and never again" failure of this flow.
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["include_granted_scopes"] = "true",
            ["state"] = state
        });

        return new GoogleAuthUrlDto(authorizationUrl, state, expiresAtUtc);
    }

    public async Task<GoogleOAuthConnectionResultDto> ExchangeAuthCodeAsync(
        string code, string state, CancellationToken cancellationToken = default)
    {
        // Note the absence of EnsureManager()/ResolveSchoolId() here: this runs on Google's
        // redirect, with no authenticated principal to consult. The unprotected state is the
        // sole authority on which school is being connected and who asked for it.
        var request = UnprotectState(state);

        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("لم تُرجع Google رمز التفويض (code).");

        var drive = await _context.SchoolGoogleDrives
            .SingleOrDefaultAsync(x => x.SchoolId == request.SchoolId, cancellationToken)
            ?? throw new KeyNotFoundException("إعدادات Google Drive الخاصة بالمدرسة غير موجودة.");

        if (string.IsNullOrWhiteSpace(drive.OAuthClientId) || string.IsNullOrWhiteSpace(drive.ProtectedOAuthClientSecret))
            throw new InvalidOperationException("OAuth Client ID و Client Secret الخاصان بالمدرسة غير مكتملين.");

        var refreshToken = await RedeemAuthorizationCodeAsync(
            code.Trim(),
            drive.OAuthClientId!.Trim(),
            _protector.Unprotect(drive.ProtectedOAuthClientSecret!),
            cancellationToken);

        var before = Describe(drive);
        drive.CredentialType = GoogleDriveCredentialType.OAuthRefreshToken;
        drive.ProtectedCredential = _protector.Protect(refreshToken);
        // Impersonation is a service-account concept. A stale value left over from a previous
        // service-account setup would misdescribe this connection, which now acts as the
        // consenting Google user themselves.
        drive.ImpersonatedUserEmail = null;
        drive.UpdatedAtUtc = DateTimeOffset.UtcNow;

        // Attributed to the manager named in the state, since there is no signed-in user on
        // this request — otherwise the one credential change that matters most would be the
        // only one in the log with no actor.
        _audit.Write(drive.SchoolId, request.UserId, "SchoolGoogleDrive.OAuthConnected", "SchoolGoogleDrive",
            drive.Id.ToString(), null, before, Describe(drive));
        await _context.SaveChangesAsync(cancellationToken);

        // A token minted from the previous credential may still be cached; drop it so the very
        // next Drive call uses the account the manager just authorized.
        _tokens.InvalidateCachedToken(drive.SchoolId);
        return new GoogleOAuthConnectionResultDto(drive.SchoolId, true, drive.UpdatedAtUtc);
    }

    /// <summary>
    /// Trades the one-time code for tokens. Only the refresh token is returned to the caller —
    /// the access token that arrives with it is intentionally dropped, because
    /// <see cref="GoogleDriveTokenService"/> owns access-token lifetime and caching.
    /// </summary>
    private async Task<string> RedeemAuthorizationCodeAsync(
        string code, string clientId, string clientSecret, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            // Google compares this against the consent request's redirect_uri byte for byte
            // and answers redirect_uri_mismatch on any difference — it is not where the
            // response is sent, it is part of the proof this is the same round trip.
            ["redirect_uri"] = RedirectUri,
            ["grant_type"] = "authorization_code"
        });

        using var response = await _httpClientFactory.CreateClient("GoogleOAuth")
            .PostAsync(TokenEndpoint, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // The body carries Google's own reason (invalid_grant, redirect_uri_mismatch,
            // invalid_client, …) and no token, so it is safe to log for the administrator —
            // but it is never returned to the browser.
            _logger.LogError("Google authorization-code exchange failed with {StatusCode}: {Body}",
                response.StatusCode, body);
            throw new InvalidOperationException("تعذر إكمال ربط حساب Google Drive. تحقق من إعدادات OAuth ثم حاول مرة أخرى.");
        }

        AuthorizationCodeResponse? payload;
        try
        {
            payload = JsonSerializer.Deserialize<AuthorizationCodeResponse>(body);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("استجابة Google لطلب الرموز غير مفهومة.", ex);
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.RefreshToken))
        {
            // Deliberately logged WITHOUT the body: on success it holds a live access token.
            // The code has now been consumed, so retrying this request cannot help — the
            // manager has to pass through consent again, which is what the message says.
            _logger.LogError("Google returned no refresh_token for the authorization-code exchange.");
            throw new InvalidOperationException(
                "لم تُرجع Google رمز تحديث (Refresh Token). أعد المحاولة وامنح الموافقة من جديد.");
        }

        return payload.RefreshToken!.Trim();
    }

    private OAuthState UnprotectState(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
            throw new UnauthorizedSchoolAccessException("طلب ربط Google Drive غير صالح.");

        OAuthState? request;
        try
        {
            request = JsonSerializer.Deserialize<OAuthState>(_stateProtector.Unprotect(state));
        }
        catch (Exception ex)
        {
            // Tampering, a state minted for a different purpose, and a Data-Protection key
            // rotation all land here. All three mean the same thing to the caller: start over.
            _logger.LogWarning(ex, "Rejected a Google Drive OAuth callback whose state could not be unprotected.");
            throw new UnauthorizedSchoolAccessException("طلب ربط Google Drive غير صالح أو منتهي الصلاحية.");
        }

        if (request is null || request.SchoolId <= 0)
            throw new UnauthorizedSchoolAccessException("طلب ربط Google Drive غير صالح.");
        if (DateTimeOffset.FromUnixTimeSeconds(request.ExpiresAtUnixSeconds) < DateTimeOffset.UtcNow)
            throw new UnauthorizedSchoolAccessException("انتهت صلاحية طلب ربط Google Drive. ابدأ الربط من جديد.");

        return request;
    }

    private int ResolveSchoolId() =>
        _scopeGuard.ResolveAllowedSchoolId(null) ?? throw new UnauthorizedSchoolAccessException("اختر مدرسة قبل ربط حساب Google Drive.");

    private void EnsureManager()
    {
        if (!_currentUser.IsGlobalAdmin() && !_currentUser.GetRoles().Contains(RoleNames.SchoolManager))
            throw new UnauthorizedSchoolAccessException("ربط حساب Google Drive متاح لمدير المدرسة فقط.");
    }

    private string AuthorizationEndpoint =>
        Fallback(_configuration["GoogleOAuth:AuthorizationEndpoint"], DefaultAuthorizationEndpoint);

    /// <summary>Shared with <see cref="GoogleDriveTokenService"/> so both grants hit the same endpoint.</summary>
    private string TokenEndpoint =>
        Fallback(_configuration["GoogleDrive:TokenEndpoint"], DefaultTokenEndpoint);

    /// <summary>
    /// The application's own callback URL. Unlike the client id and secret this is a property
    /// of the deployment, not of a school, so it is configured once — but it has no safe
    /// default, because a wrong value silently sends authorization codes elsewhere.
    /// </summary>
    private string RedirectUri => _configuration["GoogleOAuth:RedirectUri"] is { } value && !string.IsNullOrWhiteSpace(value)
        ? value.Trim()
        : throw new InvalidOperationException("لم يتم إعداد GoogleOAuth:RedirectUri في إعدادات التطبيق.");

    private TimeSpan StateLifetime =>
        _configuration.GetValue<int?>("GoogleOAuth:StateLifetimeMinutes") is > 0 and { } minutes
            ? TimeSpan.FromMinutes(minutes)
            : DefaultStateLifetime;

    private static string Fallback(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    /// <summary>
    /// Audit projection, mirroring the one in <see cref="SchoolGoogleDriveService"/>. Kept
    /// private to each service rather than shared, so neither can widen the other's audit
    /// output by accident. Excludes every protected field.
    /// </summary>
    private static object Describe(SchoolGoogleDrive drive) => new
    {
        CredentialType = drive.CredentialType.ToString(),
        drive.SchoolGoogleEmail,
        drive.ImpersonatedUserEmail,
        drive.OAuthClientId,
        drive.SharedDriveId,
        drive.RootFolderId,
        drive.RootFolderDisplayName,
        drive.IsEnabled,
        HasStoredCredential = !string.IsNullOrWhiteSpace(drive.ProtectedCredential)
    };

    /// <summary>
    /// Round trip payload for the <c>state</c> parameter. Short names keep the resulting
    /// protected string short enough for a URL.
    /// </summary>
    private sealed class OAuthState
    {
        [JsonPropertyName("sid")] public int SchoolId { get; set; }
        [JsonPropertyName("uid")] public string? UserId { get; set; }
        [JsonPropertyName("exp")] public long ExpiresAtUnixSeconds { get; set; }
    }

    private sealed class AuthorizationCodeResponse
    {
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
        [JsonPropertyName("scope")] public string? Scope { get; set; }
    }
}
