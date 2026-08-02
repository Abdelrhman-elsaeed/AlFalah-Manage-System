using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AlFalah.Application.Common.Exceptions;
using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// Google Drive v3 over plain HTTP. This type knows nothing about teachers, folders or
/// permissions — it only speaks Drive. All authorization lives one layer up, which is what
/// makes the permission rules testable against an in-memory fake of this interface.
/// </summary>
public sealed class GoogleDriveClient : IGoogleDriveClient
{
    /// <summary>Everything the UI and the ledger need from a file, in one round trip.</summary>
    private const string FileFields = "id,name,mimeType,size,modifiedTime,webViewLink,version,parents,trashed,lastModifyingUser(displayName)";

    private readonly IGoogleDriveTokenService _tokens;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleDriveClient> _logger;

    public GoogleDriveClient(
        IGoogleDriveTokenService tokens,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GoogleDriveClient> logger)
    {
        _tokens = tokens;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<GoogleDriveFile?> GetFileAsync(int schoolId, string fileId, CancellationToken cancellationToken = default)
    {
        var path = $"files/{Uri.EscapeDataString(fileId)}?fields={Uri.EscapeDataString(FileFields)}&supportsAllDrives=true";
        var json = await SendAsync(schoolId, HttpMethod.Get, path, null, allowNotFound: true, cancellationToken);
        return json is null ? null : ParseFile(json);
    }

    public async Task<GoogleDriveFileList> ListChildrenAsync(int schoolId, GoogleDriveListRequest request, CancellationToken cancellationToken = default)
    {
        // Drive's `q` grammar is string-literal based, so a single quote in a search term
        // would otherwise terminate the literal early and change the query's meaning.
        var query = new StringBuilder($"'{EscapeQueryLiteral(request.ParentFolderId)}' in parents and trashed = false");
        if (!string.IsNullOrWhiteSpace(request.NameContains))
            query.Append($" and name contains '{EscapeQueryLiteral(request.NameContains!.Trim())}'");

        var path = new StringBuilder("files?")
            .Append("q=").Append(Uri.EscapeDataString(query.ToString()))
            .Append("&fields=").Append(Uri.EscapeDataString($"nextPageToken,files({FileFields})"))
            .Append("&pageSize=").Append(Math.Clamp(request.PageSize, 1, 1000))
            .Append("&orderBy=").Append(Uri.EscapeDataString(request.OrderBy))
            .Append("&supportsAllDrives=true&includeItemsFromAllDrives=true");
        if (!string.IsNullOrWhiteSpace(request.SharedDriveId))
            path.Append("&corpora=drive&driveId=").Append(Uri.EscapeDataString(request.SharedDriveId!));
        if (!string.IsNullOrWhiteSpace(request.PageToken))
            path.Append("&pageToken=").Append(Uri.EscapeDataString(request.PageToken!));

        var json = await SendAsync(schoolId, HttpMethod.Get, path.ToString(), null, allowNotFound: false, cancellationToken)
            ?? throw new InvalidOperationException("استجابة Google Drive غير صالحة.");
        var files = json["files"]?.AsArray().Where(x => x is not null).Select(x => ParseFile(x!)).ToList() ?? [];
        return new(files, json["nextPageToken"]?.GetValue<string>());
    }

    public async Task<GoogleDriveFile> UploadAsync(int schoolId, GoogleDriveUploadRequest request, CancellationToken cancellationToken = default)
    {
        var metadata = new JsonObject
        {
            ["name"] = request.FileName,
            ["parents"] = new JsonArray(request.ParentFolderId)
        };

        // Multipart/related is Drive's one-request upload: metadata part, then bytes.
        // Drive never overwrites on create, so a same-named file can only ever be an
        // additional file — a silent replacement is not possible here.
        using var content = new MultipartContent("related")
        {
            new StringContent(metadata.ToJsonString(), Encoding.UTF8, "application/json"),
            BuildFilePart(request)
        };

        var uploadUrl = $"{UploadBaseUrl}files?uploadType=multipart&supportsAllDrives=true&fields={Uri.EscapeDataString(FileFields)}";
        var json = await SendAsync(schoolId, HttpMethod.Post, uploadUrl, content, allowNotFound: false, cancellationToken)
            ?? throw new InvalidOperationException("لم تُرجع Google Drive بيانات الملف بعد الرفع.");
        return ParseFile(json);
    }

    public async Task<DriveFileContentDto> DownloadAsync(int schoolId, string fileId, CancellationToken cancellationToken = default)
    {
        var metadata = await GetFileAsync(schoolId, fileId, cancellationToken)
            ?? throw new TeacherDriveAccessDeniedException("لم يعد الملف موجوداً.");
        if (metadata.IsFolder) throw new ArgumentException("لا يمكن تنزيل مجلد.");

        var token = await _tokens.GetAccessTokenAsync(schoolId, cancellationToken);
        var url = $"{ApiBaseUrl}files/{Uri.EscapeDataString(fileId)}?alt=media&supportsAllDrives=true";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // ResponseHeadersRead keeps a large file out of memory: the bytes flow straight
        // from Drive to the browser. The response is therefore NOT disposed here — the
        // returned stream owns it and the ASP.NET Core FileStreamResult disposes it.
        var response = await _httpClientFactory.CreateClient("GoogleDrive")
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var status = response.StatusCode;
            response.Dispose();
            if (status == HttpStatusCode.NotFound) throw new TeacherDriveAccessDeniedException("لم يعد الملف موجوداً.");
            if (status == HttpStatusCode.Unauthorized) _tokens.InvalidateCachedToken(schoolId);
            _logger.LogError("Google Drive download of {FileId} failed with {StatusCode}.", fileId, status);
            throw new InvalidOperationException("تعذر تنزيل الملف من Google Drive. يرجى المحاولة مرة أخرى.");
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType
            ?? (string.IsNullOrWhiteSpace(metadata.MimeType) ? "application/octet-stream" : metadata.MimeType);
        return new(new HttpOwnedStream(stream, response), metadata.Name, contentType,
            response.Content.Headers.ContentLength ?? metadata.Size);
    }

    public async Task<bool> TrashAsync(int schoolId, string fileId, string? sharedDriveId, CancellationToken cancellationToken = default)
    {
        // Trash rather than permanently delete: the matrix checkmark must clear immediately,
        // but a mistaken deletion has to stay recoverable from Drive itself.
        var path = $"files/{Uri.EscapeDataString(fileId)}?supportsAllDrives=true&fields=id,trashed";
        using var body = new StringContent("{\"trashed\":true}", Encoding.UTF8, "application/json");
        var json = await SendAsync(schoolId, HttpMethod.Patch, path, body, allowNotFound: true, cancellationToken);
        return json is not null;
    }

    private static HttpContent BuildFilePart(GoogleDriveUploadRequest request)
    {
        var part = new StreamContent(request.Content);
        part.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType);
        return part;
    }

    private string ApiBaseUrl => Normalize(_configuration["GoogleDrive:ApiBaseUrl"], "https://www.googleapis.com/drive/v3/");
    private string UploadBaseUrl => Normalize(_configuration["GoogleDrive:UploadBaseUrl"], "https://www.googleapis.com/upload/drive/v3/");

    private static string Normalize(string? configured, string fallback)
    {
        var value = string.IsNullOrWhiteSpace(configured) ? fallback : configured!;
        return value.EndsWith('/') ? value : value + "/";
    }

    /// <summary>
    /// Returns null only when Drive answered 404 and the caller opted into that. Anything
    /// else throws, so a transport failure can never be mistaken for "the file is gone".
    /// </summary>
    private async Task<JsonNode?> SendAsync(
        int schoolId, HttpMethod method, string pathOrUrl, HttpContent? content, bool allowNotFound, CancellationToken cancellationToken)
    {
        var token = await _tokens.GetAccessTokenAsync(schoolId, cancellationToken);
        var url = pathOrUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? pathOrUrl : ApiBaseUrl + pathOrUrl;
        using var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClientFactory.CreateClient("GoogleDrive")
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            if (allowNotFound) return null;
            throw new TeacherDriveAccessDeniedException("لم يعد الملف أو المجلد موجوداً.");
        }
        if (response.StatusCode is HttpStatusCode.Unauthorized)
        {
            // The cached token is unusable (revoked key, rotated secret). Drop it so the
            // next attempt re-authenticates instead of replaying the dead token.
            _tokens.InvalidateCachedToken(schoolId);
            _logger.LogWarning("Google Drive rejected the school {SchoolId} credential with 401.", schoolId);
            throw new InvalidOperationException("انتهت صلاحية اتصال Google Drive الخاص بالمدرسة. يرجى إعادة إعداده.");
        }
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            var forbiddenBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Google Drive returned 403 for school {SchoolId}: {Body}", schoolId, forbiddenBody);
            // 403 is overloaded — no permission, rate limited, or out of storage — and the three
            // have completely different remedies, so they must not share one message.
            throw ReadErrorReason(forbiddenBody) switch
            {
                "rateLimitExceeded" or "userRateLimitExceeded" =>
                    new InvalidOperationException("خدمة الملفات مشغولة حالياً. يرجى المحاولة بعد قليل."),
                // A service account owns no storage quota, so a file it CREATES in an ordinary
                // My Drive folder is refused even when it can read that folder perfectly well.
                // Naming the real cause stops an administrator hunting a permission problem
                // that does not exist.
                "storageQuotaExceeded" => new InvalidOperationException(
                    "لا تتوفر مساحة تخزين لحساب Google المستخدم لحفظ الملف. حساب الخدمة لا يملك مساحة خاصة: "
                    + "استخدم Shared Drive أو بريد مستخدم للانتحال (Domain-Wide Delegation)، أو حساب Google عادي برمز تحديث."),
                _ => new TeacherDriveAccessDeniedException("ليس لدى حساب المدرسة صلاحية على هذا المجلد.")
            };
        }
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new InvalidOperationException("خدمة الملفات مشغولة حالياً. يرجى المحاولة بعد قليل.");

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Google Drive request {Method} {Url} failed with {StatusCode}: {Body}", method, url, response.StatusCode, body);
            throw new InvalidOperationException("تعذر الاتصال بخدمة الملفات. يرجى المحاولة مرة أخرى.");
        }
        return JsonNode.Parse(body) ?? throw new InvalidOperationException("استجابة خدمة الملفات غير صالحة.");
    }

    /// <summary>
    /// First <c>reason</c> code in a Drive error payload, or null when the body is missing,
    /// unparseable, or carries no reason. Used to disambiguate an overloaded 403.
    /// </summary>
    internal static string? ReadErrorReason(string body)
    {
        try
        {
            var errors = JsonNode.Parse(body)?["error"]?["errors"]?.AsArray();
            if (errors is null) return null;
            return errors
                .Select(x => x?["reason"]?.GetValue<string>())
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Escapes a value for embedding in a Drive <c>q</c> string literal.</summary>
    internal static string EscapeQueryLiteral(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal);

    internal static GoogleDriveFile ParseFile(JsonNode node)
    {
        // Drive returns `size` as a string (int64 over JSON) and omits it for folders and
        // for native Google Docs, so it has to be parsed leniently rather than cast.
        long? size = long.TryParse(node["size"]?.GetValue<string>(), out var parsedSize) ? parsedSize : null;
        DateTimeOffset? modified = DateTimeOffset.TryParse(node["modifiedTime"]?.GetValue<string>(), out var parsedDate) ? parsedDate : null;
        var parents = node["parents"]?.AsArray()
            .Select(x => x?.GetValue<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToList() ?? [];

        return new(
            node["id"]?.GetValue<string>() ?? string.Empty,
            node["name"]?.GetValue<string>() ?? string.Empty,
            node["mimeType"]?.GetValue<string>() ?? string.Empty,
            size,
            modified,
            node["lastModifyingUser"]?["displayName"]?.GetValue<string>(),
            node["webViewLink"]?.GetValue<string>(),
            node["version"]?.ToString(),
            parents,
            node["trashed"]?.GetValue<bool>() ?? false);
    }

    /// <summary>
    /// A read-only pass-through stream that also disposes the <see cref="HttpResponseMessage"/>
    /// it came from. Without it the download response would leak the pooled connection,
    /// because the stream outlives the method that created it.
    /// </summary>
    private sealed class HttpOwnedStream : Stream
    {
        private readonly Stream _inner;
        private readonly HttpResponseMessage _response;

        public HttpOwnedStream(Stream inner, HttpResponseMessage response)
        {
            _inner = inner;
            _response = response;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => _inner.Read(buffer);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _inner.ReadAsync(buffer, offset, count, cancellationToken);

        public override void Flush() => _inner.Flush();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                _response.Dispose();
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync();
            _response.Dispose();
            await base.DisposeAsync();
        }
    }
}
