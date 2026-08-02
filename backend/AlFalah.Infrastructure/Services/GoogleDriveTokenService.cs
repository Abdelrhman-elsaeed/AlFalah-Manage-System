using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// Turns a school's stored Google credential into a short-lived access token.
///
/// Two grants are supported and both end at the same OAuth token endpoint:
/// a self-signed JWT assertion for a service account, or a refresh-token exchange for a
/// plain Google account. Tokens are cached in memory until shortly before they expire —
/// they are never written to the database.
/// </summary>
public sealed class GoogleDriveTokenService : IGoogleDriveTokenService
{
    /// <summary>Full Drive scope. Browsing an administrator-provisioned folder tree needs more than <c>drive.file</c>.</summary>
    private const string DriveScope = "https://www.googleapis.com/auth/drive";

    private const string DefaultTokenEndpoint = "https://oauth2.googleapis.com/token";

    /// <summary>Renew this early so an in-flight upload never dies on a token that expired mid-request.</summary>
    private static readonly TimeSpan ExpiryGuard = TimeSpan.FromMinutes(5);

    private readonly AlFalahDbContext _context;
    private readonly GoogleDriveCredentialProtector _protector;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleDriveTokenService> _logger;

    public GoogleDriveTokenService(
        AlFalahDbContext context,
        GoogleDriveCredentialProtector protector,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<GoogleDriveTokenService> logger)
    {
        _context = context;
        _protector = protector;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(int schoolId, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey(schoolId), out string? cached) && !string.IsNullOrEmpty(cached))
            return cached;

        var drive = await _context.SchoolGoogleDrives.AsNoTracking()
            .SingleOrDefaultAsync(x => x.SchoolId == schoolId && x.IsEnabled, cancellationToken)
            ?? throw new InvalidOperationException("لم يتم ربط حساب Google Drive الخاص بالمدرسة بعد.");
        if (string.IsNullOrWhiteSpace(drive.ProtectedCredential))
            throw new InvalidOperationException("بيانات اعتماد Google Drive الخاصة بالمدرسة غير مكتملة.");

        var secret = _protector.Unprotect(drive.ProtectedCredential);
        var content = drive.CredentialType switch
        {
            GoogleDriveCredentialType.ServiceAccount => BuildServiceAccountAssertion(secret, drive.ImpersonatedUserEmail),
            GoogleDriveCredentialType.OAuthRefreshToken => BuildRefreshTokenGrant(secret, drive.OAuthClientId,
                string.IsNullOrWhiteSpace(drive.ProtectedOAuthClientSecret) ? null : _protector.Unprotect(drive.ProtectedOAuthClientSecret)),
            _ => throw new InvalidOperationException("نوع بيانات اعتماد Google Drive غير مدعوم.")
        };

        var (token, expiresIn) = await RequestTokenAsync(content, cancellationToken);
        // Never cache past the token's own lifetime, and never for a negative span if
        // Google were to return an already-elapsed expires_in.
        var lifetime = TimeSpan.FromSeconds(Math.Max(expiresIn, 0));
        var cacheFor = lifetime > ExpiryGuard ? lifetime - ExpiryGuard : TimeSpan.FromSeconds(30);
        _cache.Set(CacheKey(schoolId), token, cacheFor);
        return token;
    }

    public void InvalidateCachedToken(int schoolId) => _cache.Remove(CacheKey(schoolId));

    private static string CacheKey(int schoolId) => $"google-drive-token:{schoolId}";

    private string TokenEndpoint => _configuration["GoogleDrive:TokenEndpoint"] ?? DefaultTokenEndpoint;

    private FormUrlEncodedContent BuildServiceAccountAssertion(string serviceAccountJson, string? impersonatedUserEmail)
    {
        ServiceAccountKey? key;
        try
        {
            key = JsonSerializer.Deserialize<ServiceAccountKey>(serviceAccountJson);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("مفتاح حساب الخدمة (Service Account JSON) غير صالح.", ex);
        }

        if (key is null || string.IsNullOrWhiteSpace(key.ClientEmail) || string.IsNullOrWhiteSpace(key.PrivateKey))
            throw new InvalidOperationException("مفتاح حساب الخدمة يجب أن يحتوي على client_email و private_key.");

        var audience = string.IsNullOrWhiteSpace(key.TokenUri) ? TokenEndpoint : key.TokenUri!;
        var issuedAt = DateTimeOffset.UtcNow;
        var claims = new Dictionary<string, object>
        {
            ["iss"] = key.ClientEmail!,
            ["scope"] = DriveScope,
            ["aud"] = audience,
            ["iat"] = issuedAt.ToUnixTimeSeconds(),
            ["exp"] = issuedAt.AddMinutes(60).ToUnixTimeSeconds()
        };
        // Domain-wide delegation: the token then belongs to this Workspace user, so files
        // are owned by them and consume the school's quota instead of the service account's.
        if (!string.IsNullOrWhiteSpace(impersonatedUserEmail)) claims["sub"] = impersonatedUserEmail!.Trim();

        var assertion = SignJwt(claims, key.PrivateKey!);
        return new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = assertion
        });
    }

    private static FormUrlEncodedContent BuildRefreshTokenGrant(string refreshToken, string? clientId, string? clientSecret)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            throw new InvalidOperationException("يجب إعداد OAuth Client ID و Client Secret لاستخدام رمز التحديث (Refresh Token).");
        return new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = clientId!.Trim(),
            ["client_secret"] = clientSecret!,
            ["refresh_token"] = refreshToken.Trim()
        });
    }

    private static string SignJwt(IDictionary<string, object> claims, string privateKeyPem)
    {
        using var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(privateKeyPem);
        }
        catch (Exception ex) when (ex is ArgumentException or CryptographicException)
        {
            throw new InvalidOperationException("المفتاح الخاص في ملف حساب الخدمة غير صالح.", ex);
        }

        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, string>
        {
            ["alg"] = "RS256",
            ["typ"] = "JWT"
        }));
        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(claims));
        var signingInput = Encoding.ASCII.GetBytes($"{header}.{payload}");
        var signature = Base64UrlEncode(rsa.SignData(signingInput, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        return $"{header}.{payload}.{signature}";
    }

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private async Task<(string AccessToken, int ExpiresInSeconds)> RequestTokenAsync(
        FormUrlEncodedContent content, CancellationToken cancellationToken)
    {
        using var request = content;
        using var response = await _httpClientFactory.CreateClient("GoogleOAuth")
            .PostAsync(TokenEndpoint, request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // The body carries Google's own reason (invalid_grant, unauthorized_client, …).
            // It is logged for the administrator but never returned to a teacher's client.
            _logger.LogError("Google OAuth token request failed with {StatusCode}: {Body}", response.StatusCode, body);
            throw new InvalidOperationException("تعذر الاتصال بحساب Google Drive الخاص بالمدرسة. تحقق من بيانات الاعتماد.");
        }

        var payload = JsonSerializer.Deserialize<TokenResponse>(body);
        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
            throw new InvalidOperationException("لم تُرجع Google رمز وصول صالحاً.");
        return (payload.AccessToken!, payload.ExpiresIn);
    }

    private sealed class ServiceAccountKey
    {
        [JsonPropertyName("client_email")] public string? ClientEmail { get; set; }
        [JsonPropertyName("private_key")] public string? PrivateKey { get; set; }
        [JsonPropertyName("token_uri")] public string? TokenUri { get; set; }
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    }
}
