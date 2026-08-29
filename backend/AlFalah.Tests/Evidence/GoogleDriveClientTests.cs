using System.Net;
using System.Text.Json;
using AlFalah.Application.Common.Exceptions;
using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;
using AlFalah.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AlFalah.Tests.Evidence;

/// <summary>
/// Error mapping for the Drive transport.
///
/// A 403 from Drive means three unrelated things — no permission, rate limited, or out of
/// storage — and each needs a different action from the administrator. This matters most for
/// <c>storageQuotaExceeded</c>: SharedDriveId and ImpersonatedUserEmail are optional, so a
/// service account CAN be pointed at an ordinary My Drive folder, and an upload there is the
/// one configuration Google refuses on quota. If that surfaced as "no permission" it would send
/// someone auditing Drive sharing for a problem that is not a permission problem at all.
/// </summary>
public sealed class GoogleDriveClientTests
{
    private const string QuotaBody = """
        {"error":{"errors":[{"domain":"usageLimits","reason":"storageQuotaExceeded",
        "message":"Service Accounts do not have storage quota."}],"code":403}}
        """;

    private const string RateLimitBody = """
        {"error":{"errors":[{"domain":"usageLimits","reason":"userRateLimitExceeded"}],"code":403}}
        """;

    private const string PermissionBody = """
        {"error":{"errors":[{"domain":"global","reason":"insufficientFilePermissions"}],"code":403}}
        """;

    [Fact]
    public async Task A_Quota_403_Names_The_Storage_Cause_And_Is_Not_A_Permission_Error()
    {
        var client = Client(HttpStatusCode.Forbidden, QuotaBody);

        var act = () => client.GetFileAsync(1, "folder-1");

        var error = await act.Should().ThrowAsync<InvalidOperationException>();
        error.Which.Should().NotBeOfType<TeacherDriveAccessDeniedException>(
            "a quota failure is not an access-control failure and must not read as one");
        error.Which.Message.Should().Contain("مساحة تخزين");
        error.Which.Message.Should().Contain("Shared Drive");
    }

    [Fact]
    public async Task A_RateLimit_403_Reads_As_Temporary()
    {
        var client = Client(HttpStatusCode.Forbidden, RateLimitBody);

        var act = () => client.GetFileAsync(1, "folder-1");

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Should().NotBeOfType<TeacherDriveAccessDeniedException>();
    }

    [Fact]
    public async Task A_Permission_403_Still_Denies_Access()
    {
        var client = Client(HttpStatusCode.Forbidden, PermissionBody);

        await client.Invoking(x => x.GetFileAsync(1, "folder-1"))
            .Should().ThrowAsync<TeacherDriveAccessDeniedException>();
    }

    [Fact]
    public async Task An_Unreadable_403_Body_Falls_Back_To_Denying_Access()
    {
        // Fail closed: an unparseable body must not be optimistically treated as a transient
        // problem the caller should retry.
        var client = Client(HttpStatusCode.Forbidden, "<html>not json</html>");

        await client.Invoking(x => x.GetFileAsync(1, "folder-1"))
            .Should().ThrowAsync<TeacherDriveAccessDeniedException>();
    }

    [Fact]
    public async Task A_404_Is_A_Missing_File_Not_An_Error()
    {
        var client = Client(HttpStatusCode.NotFound, "{}");

        // GetFileAsync opts into null-on-404 so reconciliation can distinguish "gone" from
        // "unreachable" — the distinction the whole missing-from-Drive flag depends on.
        (await client.GetFileAsync(1, "folder-1")).Should().BeNull();
    }

    [Fact]
    public async Task A_401_Invalidates_The_Cached_Token_So_The_Next_Call_Reauthenticates()
    {
        var tokens = new RecordingTokenService();
        var client = Client(HttpStatusCode.Unauthorized, "{}", tokens);

        await client.Invoking(x => x.GetFileAsync(1, "folder-1")).Should().ThrowAsync<InvalidOperationException>();

        tokens.Invalidated.Should().Contain(1);
    }

    [Fact]
    public async Task Rename_Uses_A_File_Patch_And_Returns_The_Updated_Metadata()
    {
        const string body = """
            {"id":"file-1","name":"خطة محدثة.pdf","mimeType":"application/pdf","size":"12",
             "parents":["folder-a"],"trashed":false}
            """;
        var handler = new StubHandler(HttpStatusCode.OK, body);
        var client = new GoogleDriveClient(
            new RecordingTokenService(), new StubClientFactory(handler),
            new ConfigurationBuilder().Build(), NullLogger<GoogleDriveClient>.Instance);

        var renamed = await client.RenameAsync(1, "file-1", "خطة محدثة.pdf");

        renamed.Name.Should().Be("خطة محدثة.pdf");
        handler.LastMethod.Should().Be(HttpMethod.Patch);
        handler.LastUrl.Should().Contain("files/file-1").And.Contain("supportsAllDrives=true");
        using var requestBody = JsonDocument.Parse(handler.LastBody!);
        requestBody.RootElement.GetProperty("name").GetString().Should().Be("خطة محدثة.pdf");
    }

    [Fact]
    public void A_Search_Term_Containing_A_Quote_Cannot_Break_Out_Of_The_Query_Literal()
    {
        // Drive's `q` grammar is string-literal based, so an unescaped quote would terminate the
        // literal early and change what the query means.
        GoogleDriveClient.EscapeQueryLiteral("a'b").Should().Be("a\\'b");
        GoogleDriveClient.EscapeQueryLiteral(@"a\b").Should().Be(@"a\\b");
    }

    [Fact]
    public void ReadErrorReason_Returns_Null_Rather_Than_Throwing_On_Junk()
    {
        GoogleDriveClient.ReadErrorReason("not json").Should().BeNull();
        GoogleDriveClient.ReadErrorReason("{}").Should().BeNull();
        GoogleDriveClient.ReadErrorReason("""{"error":{"code":403}}""").Should().BeNull();
        GoogleDriveClient.ReadErrorReason(QuotaBody).Should().Be("storageQuotaExceeded");
    }

    // ─── Harness ──────────────────────────────────────────────────────────────

    private static GoogleDriveClient Client(
        HttpStatusCode statusCode, string body, IGoogleDriveTokenService? tokens = null) =>
        new(tokens ?? new RecordingTokenService(),
            new StubClientFactory(new StubHandler(statusCode, body)),
            new ConfigurationBuilder().Build(),
            NullLogger<GoogleDriveClient>.Instance);

    private sealed class RecordingTokenService : IGoogleDriveTokenService
    {
        public List<int> Invalidated { get; } = [];
        public Task<string> GetAccessTokenAsync(int schoolId, CancellationToken cancellationToken = default) =>
            Task.FromResult("fake-token");
        public void InvalidateCachedToken(int schoolId) => Invalidated.Add(schoolId);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _body;

        public StubHandler(HttpStatusCode statusCode, string body)
        {
            _statusCode = statusCode;
            _body = body;
        }

        public HttpMethod? LastMethod { get; private set; }
        public string? LastUrl { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastMethod = request.Method;
            LastUrl = request.RequestUri?.ToString();
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(_statusCode) { Content = new StringContent(_body) };
        }
    }

    private sealed class StubClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public StubClientFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }
}
