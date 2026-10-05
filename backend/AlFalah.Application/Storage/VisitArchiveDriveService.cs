using System.Security.Cryptography;
using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Domain.Entities.Storage;

namespace AlFalah.Application.Storage;

// Shared live root/grant/content checks for workers and authorized readers.
public sealed class VisitArchiveDriveService(IStorageRepository storage, IStorageLibraryRepository library,
    IVisitArchiveRepository archives, IStorageProvider provider)
{
    private readonly Dictionary<int, (StorageDriveRoot Root, IReadOnlyList<StorageDriveRoot> Grants)> roots = [];
    public void Reset() { roots.Clear(); provider.ResetRequestCache(); }
    public static IReadOnlyDictionary<string, string> Identity(VisitArchiveOperation o) => new Dictionary<string, string>
    {
        ["schoolId"] = o.SchoolId.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["visitId"] = o.VisitId.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["approvalRevision"] = o.ApprovalRevision.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["uploadIdentity"] = o.UploadIdentity!, ["sha256"] = o.PdfSHA256!
    };
    public async Task<StorageDriveRoot> RootAsync(int schoolId, string? archiveItemId, CancellationToken ct)
    {
        provider.ResetRequestCache();
        if (!roots.TryGetValue(schoolId, out var scope))
        {
            scope = (await storage.GetSchoolDriveRootAsync(schoolId, ct) ?? throw new StorageUnavailableException(), await library.TeacherRootsAsync(schoolId, ct));
            roots[schoolId] = scope;
        }
        var root = scope.Root;
        var meta = await provider.MetadataAsync(schoolId, root.RootItemId, ct);
        if (meta is not { IsFolder: true, Trashed: false }) throw new StorageUnavailableException();
        if (archiveItemId != null && !await provider.IsWithinAsync(schoolId, root.RootItemId, archiveItemId, ct)) throw new StorageUnavailableException();
        foreach (var grant in scope.Grants)
        {
            if (grant.DriveId != root.DriveId || !await provider.IsWithinAsync(schoolId, root.RootItemId, grant.RootItemId, ct) ||
                grant.RootItemId == root.RootItemId || await provider.IsWithinAsync(schoolId, grant.RootItemId, root.RootItemId, ct) ||
                archiveItemId != null && (await provider.IsWithinAsync(schoolId, grant.RootItemId, archiveItemId, ct) ||
                    await provider.IsWithinAsync(schoolId, archiveItemId, grant.RootItemId, ct)))
                throw new StorageUnavailableException("يحتاج المسؤول إلى مراجعة حدود مجلدات المدرسة.");
        }
        return root;
    }
    public async Task<StorageFolder> FolderAsync(int schoolId, CancellationToken ct)
    {
        StorageFolder? folder = null;
        if (!await archives.ExclusiveAsync("folder:" + schoolId, async token =>
        {
            var root = await RootAsync(schoolId, null, token);
            folder = await archives.ArchiveFolderAsync(schoolId, token);
            if (folder == null)
            {
                folder = new StorageFolder { SchoolId = schoolId, Kind = Domain.Enums.StorageFolderKind.VisitArchive,
                    DisplayName = "أرشيف الزيارات", DriveId = root.DriveId, DriveItemId = await provider.AllocateIdAsync(schoolId, token) };
                await archives.AddArchiveFolderAsync(folder, token); // reservation precedes external create
            }
            if (folder.DriveId != root.DriveId) throw new StorageUnavailableException();
            provider.ResetRequestCache();
            var metadata = await provider.MetadataAsync(schoolId, folder.DriveItemId, token);
            if (metadata == null)
                await provider.CreateFolderAsync(schoolId, folder.DriveItemId, root.RootItemId, folder.DisplayName, token);
            else if (metadata.Trashed || !metadata.IsFolder) throw new StorageUnavailableException();
            await RootAsync(schoolId, folder.DriveItemId, token);
        }, ct)) throw new StorageUnavailableException();
        return folder!;
    }
    public async Task<bool> VerifyAsync(VisitArchiveOperation operation, string itemId, string hash, long length, CancellationToken ct)
    {
        var root = await RootAsync(operation.SchoolId, null, ct);
        if (root.DriveId != operation.DriveId || root.RootItemId != operation.SchoolRootItemId) throw new StorageUnavailableException();
        var folder = await provider.MetadataAsync(operation.SchoolId, operation.ArchiveFolderItemId!, ct);
        if (folder is not { IsFolder: true, Trashed: false } ||
            !await provider.IsWithinAsync(operation.SchoolId, root.RootItemId, operation.ArchiveFolderItemId!, ct)) return false;
        await RootAsync(operation.SchoolId, operation.ArchiveFolderItemId, ct);
        var item = await provider.MetadataAsync(operation.SchoolId, itemId, ct);
        if (item == null || item.Trashed || !await provider.IsWithinAsync(operation.SchoolId, root.RootItemId, itemId, ct) ||
            !await provider.IsWithinAsync(operation.SchoolId, operation.ArchiveFolderItemId!, itemId, ct)) return false;
        // ID + exact tuple, upload generation and expected hash, never the display filename.
        if (item.MimeType != "application/pdf" || item.Size != length || item.AppProperties == null ||
            Identity(operation).Any(p => !item.AppProperties.TryGetValue(p.Key, out var v) || v != p.Value))
            throw new ArchiveIdentityException();
        var content = await provider.ContentAsync(operation.SchoolId, itemId, ct);
        await using var stream = content.Content;
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[65536]; long count = 0; int read;
        while ((read = await stream.ReadAsync(buffer, ct)) != 0)
        {
            count += read; if (count > length) throw new ArchiveIdentityException(); digest.AppendData(buffer, 0, read);
        }
        if (count != length || Convert.ToHexString(digest.GetHashAndReset()) != hash) throw new ArchiveIdentityException();
        return true;
    }
}
public sealed class ArchiveIdentityException : Exception;
