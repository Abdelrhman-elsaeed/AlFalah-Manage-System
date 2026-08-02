using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AlFalah.Tests.Evidence;

/// <summary>
/// The credential layer. A mistake here is invisible until it takes the whole feature down, so
/// the service-account assertion is verified cryptographically rather than just "not empty":
/// Google will reject a JWT whose signature or claims are wrong, and the failure would surface
/// only as an opaque <c>invalid_grant</c> in production.
/// </summary>
public sealed class GoogleDriveTokenServiceTests : IAsyncDisposable
{
    private const string TokenEndpoint = "https://oauth2.test/token";
    private readonly ServiceProvider _dataProtection = new ServiceCollection().AddDataProtection().Services.BuildServiceProvider();

    [Fact]
    public async Task ServiceAccount_Assertion_Is_A_Valid_RS256_Jwt_With_The_Drive_Scope()
    {
        using var rsa = RSA.Create(2048);
        var handler = new CapturingHandler();
        await using var harness = await CreateAsync(GoogleDriveCredentialType.ServiceAccount, ServiceAccountJson(rsa), handler);

        var token = await harness.Service.GetAccessTokenAsync(1);

        token.Should().Be("granted-access-token");
        var form = handler.LastForm.Should().NotBeNull().And.Subject as Dictionary<string, string>;
        form!["grant_type"].Should().Be("urn:ietf:params:oauth:grant-type:jwt-bearer");

        var parts = form["assertion"].Split('.');
        parts.Should().HaveCount(3);
        JsonNode.Parse(Decode(parts[0]))!["alg"]!.GetValue<string>().Should().Be("RS256");

        // The signature must verify against the key from the JSON, over exactly header.payload.
        var signed = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
        rsa.VerifyData(signed, DecodeBytes(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .Should().BeTrue("Google rejects an assertion whose RS256 signature does not verify");

        var claims = JsonNode.Parse(Decode(parts[1]))!;
        claims["iss"]!.GetValue<string>().Should().Be("svc@test.iam.gserviceaccount.com");
        claims["scope"]!.GetValue<string>().Should().Be("https://www.googleapis.com/auth/drive");
        claims["aud"]!.GetValue<string>().Should().Be(TokenEndpoint);
        claims["exp"]!.GetValue<long>().Should().BeGreaterThan(claims["iat"]!.GetValue<long>());
        // No impersonation configured, so `sub` must be absent — sending it unset would make
        // Google reject the assertion.
        claims.AsObject().Should().NotContainKey("sub");
    }

    [Fact]
    public async Task Impersonation_Adds_The_Sub_Claim()
    {
        using var rsa = RSA.Create(2048);
        var handler = new CapturingHandler();
        await using var harness = await CreateAsync(GoogleDriveCredentialType.ServiceAccount, ServiceAccountJson(rsa), handler,
            impersonatedUserEmail: "evidence@school.edu.sa");

        await harness.Service.GetAccessTokenAsync(1);

        var claims = JsonNode.Parse(Decode(handler.LastForm!["assertion"].Split('.')[1]))!;
        claims["sub"]!.GetValue<string>().Should().Be("evidence@school.edu.sa");
    }

    [Fact]
    public async Task RefreshToken_Grant_Sends_The_Client_Credentials()
    {
        var handler = new CapturingHandler();
        await using var harness = await CreateAsync(GoogleDriveCredentialType.OAuthRefreshToken, "1//refresh-token", handler,
            oAuthClientId: "client-id", oAuthClientSecret: "client-secret");

        var token = await harness.Service.GetAccessTokenAsync(1);

        token.Should().Be("granted-access-token");
        handler.LastForm!["grant_type"].Should().Be("refresh_token");
        handler.LastForm["client_id"].Should().Be("client-id");
        handler.LastForm["client_secret"].Should().Be("client-secret");
        handler.LastForm["refresh_token"].Should().Be("1//refresh-token");
    }

    [Fact]
    public async Task The_Token_Is_Cached_Until_Invalidated()
    {
        var handler = new CapturingHandler();
        await using var harness = await CreateAsync(GoogleDriveCredentialType.OAuthRefreshToken, "1//refresh", handler,
            oAuthClientId: "id", oAuthClientSecret: "secret");

        await harness.Service.GetAccessTokenAsync(1);
        await harness.Service.GetAccessTokenAsync(1);
        handler.Calls.Should().Be(1, "a cached token must not be re-fetched on every Drive call");

        // A credential change (or a 401) must force re-authentication.
        harness.Service.InvalidateCachedToken(1);
        await harness.Service.GetAccessTokenAsync(1);
        handler.Calls.Should().Be(2);
    }

    [Fact]
    public async Task An_Already_Expired_Lifetime_Is_Never_Cached_For_A_Negative_Span()
    {
        var handler = new CapturingHandler { ExpiresIn = 0 };
        await using var harness = await CreateAsync(GoogleDriveCredentialType.OAuthRefreshToken, "1//refresh", handler,
            oAuthClientId: "id", oAuthClientSecret: "secret");

        // A zero (or tiny) expires_in must not produce a negative cache duration, which would
        // throw instead of simply re-fetching.
        await harness.Service.Invoking(x => x.GetAccessTokenAsync(1)).Should().NotThrowAsync();
    }

    [Fact]
    public async Task A_Google_Rejection_Surfaces_As_A_Configuration_Error_Without_Leaking_The_Reason()
    {
        var handler = new CapturingHandler { StatusCode = HttpStatusCode.BadRequest, Body = "{\"error\":\"invalid_grant\"}" };
        await using var harness = await CreateAsync(GoogleDriveCredentialType.OAuthRefreshToken, "1//stale", handler,
            oAuthClientId: "id", oAuthClientSecret: "secret");

        var act = () => harness.Service.GetAccessTokenAsync(1);

        // Google's raw reason is logged for the administrator, not returned to a teacher.
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().NotContain("invalid_grant");
    }

    [Fact]
    public async Task An_Unconfigured_Or_Disabled_School_Cannot_Get_A_Token()
    {
        var handler = new CapturingHandler();
        await using var harness = await CreateAsync(GoogleDriveCredentialType.OAuthRefreshToken, "1//refresh", handler,
            oAuthClientId: "id", oAuthClientSecret: "secret", isEnabled: false);

        await harness.Service.Invoking(x => x.GetAccessTokenAsync(1)).Should().ThrowAsync<InvalidOperationException>();
        await harness.Service.Invoking(x => x.GetAccessTokenAsync(99)).Should().ThrowAsync<InvalidOperationException>();
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task A_Corrupt_ServiceAccount_Key_Fails_With_A_Clear_Message()
    {
        var handler = new CapturingHandler();
        await using var harness = await CreateAsync(GoogleDriveCredentialType.ServiceAccount, "not-json-at-all", handler);

        await harness.Service.Invoking(x => x.GetAccessTokenAsync(1)).Should().ThrowAsync<InvalidOperationException>();
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public void A_Tampered_Ciphertext_Reports_A_Re_Entry_Requirement_Not_A_Crypto_Error()
    {
        var protector = new GoogleDriveCredentialProtector(_dataProtection.GetRequiredService<IDataProtectionProvider>());
        var ciphertext = protector.Protect("secret-value");

        protector.Unprotect(ciphertext).Should().Be("secret-value");
        ciphertext.Should().NotContain("secret-value");

        // The only fix is for a manager to paste the credential again, so the error has to say
        // that rather than surfacing a raw CryptographicException.
        protector.Invoking(x => x.Unprotect(ciphertext[..^4] + "AAAA"))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*إعادة إدخالها*");
    }

    // ─── Harness ──────────────────────────────────────────────────────────────

    private async Task<TokenHarness> CreateAsync(
        GoogleDriveCredentialType credentialType,
        string credential,
        CapturingHandler handler,
        string? impersonatedUserEmail = null,
        string? oAuthClientId = null,
        string? oAuthClientSecret = null,
        bool isEnabled = true)
    {
        var context = new AlFalahDbContext(new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase($"google-token-{Guid.NewGuid()}").Options);
        context.Schools.Add(new School { Id = 1, Name = "مدرسة", City = "الرياض", IsActive = true });

        var protector = new GoogleDriveCredentialProtector(_dataProtection.GetRequiredService<IDataProtectionProvider>());
        context.SchoolGoogleDrives.Add(new SchoolGoogleDrive
        {
            SchoolId = 1,
            CredentialType = credentialType,
            SchoolGoogleEmail = "evidence@school.edu.sa",
            ProtectedCredential = protector.Protect(credential),
            ImpersonatedUserEmail = impersonatedUserEmail,
            OAuthClientId = oAuthClientId,
            ProtectedOAuthClientSecret = oAuthClientSecret is null ? null : protector.Protect(oAuthClientSecret),
            SharedDriveId = "shared-1",
            RootFolderId = "root",
            RootFolderDisplayName = "ملفات الإنجاز",
            IsEnabled = isEnabled
        });
        await context.SaveChangesAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GoogleDrive:TokenEndpoint"] = TokenEndpoint })
            .Build();
        var service = new GoogleDriveTokenService(context, protector, new SingleClientFactory(handler),
            new MemoryCache(new MemoryCacheOptions()), configuration, NullLogger<GoogleDriveTokenService>.Instance);
        return new TokenHarness(context, service);
    }

    public async ValueTask DisposeAsync()
    {
        await _dataProtection.DisposeAsync();
    }

    private static string ServiceAccountJson(RSA rsa) => JsonSerializer.Serialize(new Dictionary<string, string>
    {
        ["type"] = "service_account",
        ["client_email"] = "svc@test.iam.gserviceaccount.com",
        ["private_key"] = new string(PemEncode(rsa)),
        ["token_uri"] = TokenEndpoint
    });

    private static char[] PemEncode(RSA rsa) => rsa.ExportPkcs8PrivateKeyPem().ToCharArray();

    private static string Decode(string base64Url) => Encoding.UTF8.GetString(DecodeBytes(base64Url));

    private static byte[] DecodeBytes(string base64Url)
    {
        var padded = base64Url.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }

    private sealed record TokenHarness(AlFalahDbContext Context, GoogleDriveTokenService Service) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public Dictionary<string, string>? LastForm { get; private set; }
        public HttpStatusCode StatusCode { get; init; } = HttpStatusCode.OK;
        public int ExpiresIn { get; init; } = 3599;
        public string? Body { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var raw = await request.Content!.ReadAsStringAsync(cancellationToken);
            LastForm = raw.Split('&')
                .Select(pair => pair.Split('=', 2))
                .ToDictionary(pair => Uri.UnescapeDataString(pair[0]), pair => Uri.UnescapeDataString(pair[1].Replace('+', ' ')));
            var body = Body ?? $"{{\"access_token\":\"granted-access-token\",\"expires_in\":{ExpiresIn}}}";
            return new HttpResponseMessage(StatusCode) { Content = new StringContent(body) };
        }
    }

    private sealed class SingleClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public SingleClientFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }
}
