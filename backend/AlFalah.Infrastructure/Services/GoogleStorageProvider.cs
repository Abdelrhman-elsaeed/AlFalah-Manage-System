using System.Collections.Concurrent;
using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;
using AlFalah.Application.Storage;

namespace AlFalah.Infrastructure.Services;

public sealed class GoogleStorageProvider(IGoogleDriveClient drive) : IStorageProvider
{
    public bool IsRealDelivery => drive.IsRealDelivery;
    private readonly ConcurrentDictionary<string, Lazy<Task<GoogleDriveFile?>>> metadata = new(StringComparer.Ordinal);
    public void ResetRequestCache() => metadata.Clear();
    public Task<string> AllocateIdAsync(int schoolId, CancellationToken ct) => drive.AllocateFileIdAsync(schoolId, ct);
    public async Task<GoogleDriveFile?> MetadataAsync(int schoolId, string id, CancellationToken ct)
    {
        var key = schoolId + ":" + id;
        var pending = metadata.GetOrAdd(key, _ => new Lazy<Task<GoogleDriveFile?>>(
            () => drive.GetFileAsync(schoolId, id, ct), LazyThreadSafetyMode.ExecutionAndPublication));
        try { return await pending.Value; }
        catch
        {
            metadata.TryRemove(new KeyValuePair<string, Lazy<Task<GoogleDriveFile?>>>(key, pending));
            throw;
        }
    }
    private void Invalidate(int schoolId, string id) => metadata.TryRemove(schoolId + ":" + id, out _);
    public async Task<GoogleDriveFile> CreateFolderAsync(int schoolId, string id, string parent, string name, CancellationToken ct)
    { try { return await drive.CreateFolderAsync(schoolId, id, parent, name, ct); } finally { Invalidate(schoolId, id); } }
    public async Task<GoogleDriveFile> UploadAsync(int schoolId, string id, string parent, string name, string mime, Stream content, CancellationToken ct)
    { try { return await drive.UploadAsync(schoolId, new(content, name, mime, parent, null, id), ct); } finally { Invalidate(schoolId, id); } }
    public async Task<GoogleDriveFile> UploadArchiveAsync(int schoolId, string id, string parent, string name,
        Stream content, IReadOnlyDictionary<string, string> identity, CancellationToken ct)
    { try { return await drive.UploadAsync(schoolId, new(content, name, "application/pdf", parent, null, id, identity), ct); }
      finally { Invalidate(schoolId, id); } }
    public async Task<GoogleDriveFile> MoveAsync(int schoolId, string id, string oldParent, string newParent, CancellationToken ct)
    { try { return await drive.MoveAsync(schoolId, id, oldParent, newParent, ct); } finally { Invalidate(schoolId, id); } }
    public async Task<GoogleDriveFile> RenameAsync(int schoolId, string id, string name, CancellationToken ct)
    { try { return await drive.RenameAsync(schoolId, id, name, ct); } finally { Invalidate(schoolId, id); } }
    public async Task<bool> TrashAsync(int schoolId, string id, string driveId, CancellationToken ct)
    { try { return await drive.TrashAsync(schoolId, id, driveId, ct); } finally { Invalidate(schoolId, id); } }
    public async Task<GoogleDriveFile> RestoreAsync(int schoolId, string id, CancellationToken ct)
    { try { return await drive.RestoreAsync(schoolId,id,ct); } finally { Invalidate(schoolId,id); } }
    public async Task<DriveFileContentDto> ContentAsync(int schoolId, string id, CancellationToken ct)
    {
        var current = await MetadataAsync(schoolId, id, ct) ?? throw new KeyNotFoundException("الملف غير متاح.");
        return await drive.DownloadVerifiedAsync(schoolId, current, ct);
    }
    public async Task<bool> IsWithinAsync(int schoolId, string root, string item, CancellationToken ct)
    {
        var node = await MetadataAsync(schoolId, item, ct);
        if (node is null || node.Trashed) return false;
        if (item == root) return true;
        var visited = new HashSet<string> { item };
        var queue = new Queue<string>(node.Parents);
        var inspected = 0;
        while (queue.Count > 0 && inspected++ < 64)
        {
            var id = queue.Dequeue();
            if (!visited.Add(id)) continue;
            var parent = await MetadataAsync(schoolId, id, ct);
            if (parent is null || parent.Trashed || !parent.IsFolder) continue;
            if (id == root) return true;
            foreach (var ancestor in parent.Parents) queue.Enqueue(ancestor);
        }
        // Match the existing fail-closed guard's bounded ancestry policy.
        return false;
    }
    public async Task<GoogleDriveFileList> ChildrenAsync(int schoolId, string parent, string driveId, string? token, CancellationToken ct)
    {
        var page = await drive.ListChildrenAsync(schoolId, new(parent, null, "folder,name,createdTime", 100, token,
            string.IsNullOrEmpty(driveId) ? null : driveId), ct);
        // A list response has the same complete metadata projection as a GET. Reuse it
        // only within this scoped request; fresh reads/verification still reset the cache.
        foreach (var item in page.Files)
            metadata[schoolId + ":" + item.Id] = new Lazy<Task<GoogleDriveFile?>>(() => Task.FromResult<GoogleDriveFile?>(item));
        return page;
    }
}
