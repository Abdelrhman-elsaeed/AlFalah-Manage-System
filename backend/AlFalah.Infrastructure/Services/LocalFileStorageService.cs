using System.Security.Cryptography;
using System.Globalization;
using AlFalah.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AlFalah.Infrastructure.Services;

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;
    private readonly string _legacyRootPath;

    public LocalFileStorageService(IConfiguration configuration, IHostEnvironment environment)
    {
        var configuredRoot = configuration["StudentAffairs:ExcuseStoragePath"];
        _rootPath = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "absence-excuses")
            : Path.IsPathRooted(configuredRoot)
                ? configuredRoot
                : Path.Combine(environment.ContentRootPath, configuredRoot));
        _legacyRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "App_Data", "absence-excuses"));
    }

    public async Task<StoredFileResult> StoreAsync(
        int schoolId,
        Stream content,
        string originalFileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var extension = Path.GetExtension(Path.GetFileName(originalFileName)).ToLowerInvariant();
        var storageKey = $"{schoolId}/{DateTime.UtcNow.ToString("yyyy/MM", CultureInfo.InvariantCulture)}/{Guid.NewGuid():N}{extension}";
        var targetPath = ResolvePath(storageKey, _rootPath);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        try
        {
            await using var output = new FileStream(
                targetPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            long size = 0;
            int read;
            while ((read = await content.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                hash.AppendData(buffer, 0, read);
                size += read;
            }

            return new StoredFileResult(
                "Local",
                storageKey,
                Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
                size);
        }
        catch
        {
            if (File.Exists(targetPath)) File.Delete(targetPath);
            throw;
        }
    }

    public Task DeleteIfExistsAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var targetPath = ResolvePath(storageKey, _rootPath);
        if (File.Exists(targetPath)) File.Delete(targetPath);
        return Task.CompletedTask;
    }

    public async Task<byte[]?> ReadBytesAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var root in new[] { _rootPath, _legacyRootPath }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var targetPath = ResolvePath(storageKey, root);
            if (File.Exists(targetPath))
                return await File.ReadAllBytesAsync(targetPath, cancellationToken).ConfigureAwait(false);

            // Older uploads used the active Arabic calendar. Its year contained a
            // direction mark that the varchar storage key column saved as '?'.
            if (storageKey.Contains('?'))
            {
                targetPath = ResolvePath(storageKey.Replace('?', '\u200f'), root);
                if (File.Exists(targetPath))
                    return await File.ReadAllBytesAsync(targetPath, cancellationToken).ConfigureAwait(false);
            }
        }
        return null;
    }

    private static string ResolvePath(string storageKey, string rootPath)
    {
        var normalizedRoot = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var targetPath = Path.GetFullPath(Path.Combine(
            normalizedRoot,
            storageKey.Replace('/', Path.DirectorySeparatorChar)));
        if (!targetPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The storage key resolves outside the configured root");
        return targetPath;
    }
}
