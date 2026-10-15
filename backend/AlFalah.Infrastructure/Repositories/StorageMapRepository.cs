using AlFalah.Application.Storage;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class StorageMapRepository(AlFalahDbContext db) : IStorageMapRepository
{
    public async Task<IReadOnlyList<StorageMapRow>> RowsAsync(int school, CancellationToken ct)
    {
        // Materialize only bounded folder identities and grouped counts, never file bytes.
        var rows = await db.StorageFolders.AsNoTracking().Where(x => x.SchoolId == school && x.IsActive &&
            x.OwnerTeacherId == null && (x.ParentFolderId == null || x.Kind == StorageFolderKind.SchoolLibrary))
            .OrderBy(x => x.DisplayName).ThenBy(x => x.Id).Take(5001).ToListAsync(ct);
        if (rows.Count > 5000) throw new StorageUnavailableException("افتح المكتبة لتصفح هذه البنية الكبيرة بالصفحات.");
        var counts = await db.StoredFiles.AsNoTracking().Where(f => f.SchoolId == school && f.OwnerTeacherId == null &&
            f.Folder.Kind == StorageFolderKind.SchoolLibrary && f.Folder.IsActive &&
            f.SourceKind != StoredFileSourceKind.DutyArchive && f.SourceKind != StoredFileSourceKind.VisitArchive &&
            db.StoredFileVersions.Any(v => v.Id == f.CurrentVersionId && v.SchoolId == school && v.Availability == StoredFileAvailability.Available))
            .GroupBy(x => x.FolderId).Select(x => new { Id = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        var byId = rows.ToDictionary(x => x.Id);
        var children = rows.Where(x => x.ParentFolderId.HasValue).GroupBy(x => x.ParentFolderId!.Value).ToDictionary(x => x.Key, x => x.ToArray());
        var visible = rows.Where(x => x.ParentFolderId is null || x.Kind == StorageFolderKind.SchoolLibrary &&
            byId.TryGetValue(x.ParentFolderId.Value, out var parent) && (parent.ParentFolderId is null ||
                byId.TryGetValue(parent.ParentFolderId.Value, out var grandparent) && grandparent.ParentFolderId is null)).ToArray();
        if (visible.Length > 500) throw new StorageUnavailableException("افتح المكتبة لتصفح هذه البنية الكبيرة بالصفحات.");
        int Total(int id, HashSet<int> path)
        {
            if (path.Count >= 64 || !path.Add(id)) throw new StorageUnavailableException("مسار المكتبة غير صالح.");
            var n = counts.GetValueOrDefault(id);
            if (children.TryGetValue(id, out var nested)) foreach (var child in nested) n += Total(child.Id, path);
            path.Remove(id);return n;
        }
        return visible.Select(x => new StorageMapRow(x,
            x.ParentFolderId is int parent && byId.TryGetValue(parent, out var p) && p.ParentFolderId is not null
                ? Total(x.Id, []) : counts.GetValueOrDefault(x.Id))).ToArray();
    }
}
