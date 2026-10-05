using AlFalah.Application.Interfaces;
using AlFalah.Application.Storage;

namespace AlFalah.Infrastructure.Services;

// Runs before the SQL approval transaction. Images become immutable data URIs;
// the worker never re-reads changed branding/signatures or remote URLs.
public sealed class VisitArchiveAssetFreezer(ImageAssetLoader loader) : IVisitArchiveAssetFreezer
{
    private sealed class ApprovalAssetHttpFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new HttpClientHandler { AllowAutoRedirect = false });
    }
    public async Task<VisitV2PdfAssetSources> FreezeAsync(VisitV2PdfAssetSources sources, CancellationToken ct)
    {
        async Task<string?> Image(string? source)
        {
            // Approval HTTP must never contact Google, even through an asset URL.
            if (Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
                (uri.Host.EndsWith("google.com", StringComparison.OrdinalIgnoreCase) ||
                 uri.Host.EndsWith("googleapis.com", StringComparison.OrdinalIgnoreCase) ||
                 uri.Host.EndsWith("googleusercontent.com", StringComparison.OrdinalIgnoreCase))) return null;
            // A permitted image host must not redirect approval HTTP into Google.
            var assetLoader = Uri.TryCreate(source, UriKind.Absolute, out var remote) &&
                remote.Scheme is "https" or "http" ? new ImageAssetLoader(new ApprovalAssetHttpFactory()) : loader;
            var loaded = await assetLoader.TryLoadAsync(source, cancellationToken: ct);
            return loaded.HasValue ? $"data:image/{loaded.Value.Format};base64,{Convert.ToBase64String(loaded.Value.Bytes)}" : null;
        }
        var logo = await Image(sources.LogoSource) ?? await Image(Path.Combine(AppContext.BaseDirectory, "Assets", "Logo.png"));
        return sources with
        {
            LogoSource = logo, InstructorSignatureSource = await Image(sources.InstructorSignatureSource),
            EvaluatorSignatureSource = sources.ShowEvaluatorSignature ? await Image(sources.EvaluatorSignatureSource) : null,
            ManagerSignatureSource = sources.ShowManagerSignature ? await Image(sources.ManagerSignatureSource) : null,
            Frozen = true
        };
    }
}
