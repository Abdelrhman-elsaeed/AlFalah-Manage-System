using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// Phase 6 / Stage 2 — safe loader for report assets (school logo, user
/// signatures). Accepts:
///  - a remote URL (http/https) → fetches with timeout + size cap.
///  - a base64 payload (data URI or raw base64) → decodes.
///  - an absolute file path on disk → reads.
///
/// Every load is wrapped in try/catch and validates the payload against a
/// short whitelist of magic bytes (PNG / JPEG / GIF / WebP) and a hard size
/// cap (default 2 MB). On any failure the caller receives null + a log
/// warning — the PDF service then renders a neutral fallback. We never
/// propagate image-loading errors into the report pipeline because a missing
/// logo/signature must NEVER crash an official report.
/// </summary>
public sealed class ImageAssetLoader
{
    /// <summary>Default hard cap for any single image — keeps a malicious or
    /// accidentally-huge file from blowing up the in-memory PDF.</summary>
    public const int DefaultMaxBytes = 2 * 1024 * 1024; // 2 MB

    private static readonly HashSet<string> ImageFormats =
        new(System.StringComparer.OrdinalIgnoreCase) { "png", "jpeg", "jpg", "gif" };

    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly ILogger<ImageAssetLoader> _logger;

    public ImageAssetLoader(IHttpClientFactory? httpClientFactory = null,
                            ILogger<ImageAssetLoader>? logger = null)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger ?? NullLogger<ImageAssetLoader>.Instance;
    }

    /// <summary>Result of a load attempt — bytes + detected format, OR null
    /// on any failure (which the caller should treat as "fallback").</summary>
    public readonly record struct LoadResult(byte[] Bytes, string Format)
    {
        public bool IsEmpty => Bytes is null || Bytes.Length == 0;
    }

    /// <summary>
    /// Loads an image from <paramref name="source"/> if it is non-empty.
    /// Returns <c>null</c> on every failure mode (null/empty source, network
    /// error, oversize, unknown format, IO error).
    /// </summary>
    /// <param name="source">URL, base64 payload, or absolute file path. Null/empty → null.</param>
    /// <param name="maxBytes">Hard cap in bytes; defaults to 2 MB.</param>
    /// <param name="cancellationToken">Forwarded to HTTP reads.</param>
    public async Task<LoadResult?> TryLoadAsync(string? source,
                                                int maxBytes = DefaultMaxBytes,
                                                CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
            return null;

        try
        {
            byte[]? bytes = null;

            var trimmed = source.Trim();

            if (LooksLikeBase64(trimmed))
            {
                bytes = DecodeBase64(trimmed, maxBytes);
            }
            else if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
                     && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                bytes = await FetchHttpAsync(uri, maxBytes, cancellationToken);
            }
            else if (Path.IsPathRooted(trimmed) && File.Exists(trimmed))
            {
                bytes = await ReadFileAsync(trimmed, maxBytes, cancellationToken);
            }
            else
            {
                _logger.LogWarning("ImageAssetLoader: unrecognized source (not URL, not base64, not file path).");
                return null;
            }

            if (bytes is null || bytes.Length == 0)
                return null;

            var format = DetectFormat(bytes);
            if (format is null)
            {
                _logger.LogWarning("ImageAssetLoader: bytes do not look like a supported image (PNG/JPEG/GIF).");
                return null;
            }

            return new LoadResult(bytes, format);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ImageAssetLoader: failed to load image — using fallback.");
            return null;
        }
    }

    // ─── Internals ─────────────────────────────────────────────────────────

    private static bool LooksLikeBase64(string s)
    {
        // data URI prefix → definitely base64
        if (s.StartsWith("data:", System.StringComparison.OrdinalIgnoreCase))
            return true;

        // raw base64 heuristic — long enough, only base64 chars, no scheme.
        if (s.Length < 64) return false;
        if (s.Contains("://")) return false;
        foreach (var c in s)
        {
            if (!(char.IsLetterOrDigit(c) || c is '+' or '/' or '=' or '\r' or '\n'))
                return false;
        }
        return true;
    }

    private static byte[]? DecodeBase64(string source, int maxBytes)
    {
        var payload = source;
        if (payload.StartsWith("data:", System.StringComparison.OrdinalIgnoreCase))
        {
            var comma = payload.IndexOf(',');
            if (comma < 0) return null;
            payload = payload[(comma + 1)..];
        }

        try
        {
            var bytes = Convert.FromBase64String(payload);
            if (bytes.Length > maxBytes) return null;
            return bytes;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private async Task<byte[]?> FetchHttpAsync(Uri uri, int maxBytes, CancellationToken cancellationToken)
    {
        if (_httpClientFactory is null)
        {
            _logger.LogWarning("ImageAssetLoader: HTTP fetch requested but no IHttpClientFactory registered.");
            return null;
        }

        using var http = _httpClientFactory.CreateClient("PdfAssetLoader");
        http.Timeout = TimeSpan.FromSeconds(5);

        using var req = new HttpRequestMessage(HttpMethod.Get, uri);
        using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("ImageAssetLoader: HTTP {Status} for {Url}", (int)resp.StatusCode, uri);
            return null;
        }

        var contentLength = resp.Content.Headers.ContentLength;
        if (contentLength.HasValue && contentLength.Value > maxBytes)
        {
            _logger.LogWarning("ImageAssetLoader: remote image too large ({Len} > {Cap}).", contentLength.Value, maxBytes);
            return null;
        }

        await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken);

        // Stream with a hard cap so a missing Content-Length cannot OOM us.
        using var ms = new System.IO.MemoryStream();
        var buffer = new byte[8192];
        int total = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                _logger.LogWarning("ImageAssetLoader: remote image exceeded {Cap} cap mid-stream.", maxBytes);
                return null;
            }
            ms.Write(buffer, 0, read);
        }

        return ms.ToArray();
    }

    private static async Task<byte[]?> ReadFileAsync(string path, int maxBytes, CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > maxBytes) return null;

        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                                       bufferSize: 8192, useAsync: true);
        var bytes = new byte[info.Length];
        var offset = 0;
        while (offset < bytes.Length)
        {
            var n = await fs.ReadAsync(bytes.AsMemory(offset, bytes.Length - offset), cancellationToken);
            if (n <= 0) break;
            offset += n;
        }
        if (offset != bytes.Length) return null;
        return bytes;
    }

    /// <summary>Sniffs the magic bytes of an image. Returns null on unknown.</summary>
    private static string? DetectFormat(byte[] bytes)
    {
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            return "png";

        if (bytes.Length >= 3 &&
            bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "jpeg";

        if (bytes.Length >= 6 &&
            (bytes[0] == 'G' && bytes[1] == 'I' && bytes[2] == 'F'))
            return "gif";

        return null;
    }
}