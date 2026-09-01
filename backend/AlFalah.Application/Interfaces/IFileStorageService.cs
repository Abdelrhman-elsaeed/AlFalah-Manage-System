namespace AlFalah.Application.Interfaces;

public sealed record StoredFileResult(
    string StorageProvider,
    string StorageKey,
    string Sha256,
    long SizeBytes);

public interface IFileStorageService
{
    Task<StoredFileResult> StoreAsync(
        int schoolId,
        Stream content,
        string originalFileName,
        string contentType,
        CancellationToken cancellationToken);

    Task DeleteIfExistsAsync(
        string storageKey,
        CancellationToken cancellationToken);

    Task<byte[]?> ReadBytesAsync(
        string storageKey,
        CancellationToken cancellationToken);
}
