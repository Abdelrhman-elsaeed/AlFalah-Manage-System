using System.Security.Cryptography;
using AlFalah.Application.Duty;
using Microsoft.Extensions.Hosting;

namespace AlFalah.Infrastructure.Services;

/// <summary>Freezes only embedded/local branding. Signing never fetches a browser-selected URL.</summary>
public sealed class DutyDocumentAssets(IHostEnvironment environment) : IDutyDocumentAssets
{
    public async Task<DutyLogoSnapshot> FreezeLogoAsync(string? source, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(source)) return new(null, null, "NotConfigured");
        string? trusted = null;
        if (source.StartsWith("data:image/", StringComparison.Ordinal) && source.Length <= 3 * 1024 * 1024) trusted = source;
        else if (source.StartsWith("/uploads/", StringComparison.Ordinal))
        {
            try
            {
                var root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "wwwroot", "uploads")) + Path.DirectorySeparatorChar;
                var path = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "wwwroot", source.TrimStart('/')));
                if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) trusted = path;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return new(null, null, "UnavailableAtSigning");
            }
        }
        if (trusted is null) return new(null, null, "UnavailableAtSigning");
        var loaded = await new ImageAssetLoader().TryLoadAsync(trusted, cancellationToken: ct);
        return loaded is { } image
            ? new($"data:image/{image.Format};base64,{Convert.ToBase64String(image.Bytes)}", Convert.ToHexString(SHA256.HashData(image.Bytes)), "Frozen")
            : new(null, null, "UnavailableAtSigning");
    }
}
