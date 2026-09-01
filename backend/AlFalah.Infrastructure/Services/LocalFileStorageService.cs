using System.Security.Cryptography;
using AlFalah.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace AlFalah.Infrastructure.Services;

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;

    public LocalFileStorageService(IConfiguration configuration)
    {
        var configuredRoot = configuration["StudentAffairs:ExcuseStoragePath"];
        _rootPath = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(AppContext.BaseDirectory, "App_Data", "absence-excuses")
            : configuredRoot);
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
        var storageKey = $"{schoolId}/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{extension}";
        var targetPath = ResolvePath(storageKey);
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
        var targetPath = ResolvePath(storageKey);
        if (File.Exists(targetPath)) File.Delete(targetPath);
        return Task.CompletedTask;
    }

    public async Task<byte[]?> ReadBytesAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var targetPath = ResolvePath(storageKey);
        if (!File.Exists(targetPath)) return null;
        return await File.ReadAllBytesAsync(targetPath, cancellationToken).ConfigureAwait(false);
    }

    private string ResolvePath(string storageKey)
    {
        var normalizedRoot = _rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var targetPath = Path.GetFullPath(Path.Combine(
            normalizedRoot,
            storageKey.Replace('/', Path.DirectorySeparatorChar)));
        if (!targetPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The storage key resolves outside the configured root");
        return targetPath;
    }
}
