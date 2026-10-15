using AlFalah.Application.Common;
using AlFalah.Application.Duty;
using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using Microsoft.Extensions.Options;

namespace AlFalah.Application.Storage;

public sealed class StorageMapService(IStorageMapRepository repository, IStorageRepository scopes,
    IStorageLibraryRepository library, IStorageAuthorizationService authorization, ICurrentUserService user,
    IStorageProvider provider, IDutyRepository duty, IVisitArchiveService visits, IOptions<StorageOptions> options) : IStorageMapService
{
    public async Task<StorageMapDto> ReadAsync(CancellationToken ct)
    {
        var school = user.ActiveSchoolId ?? throw new UnauthorizedSchoolAccessException("اختر المدرسة أولًا.");
        if (!options.Value.ReadModelEnabled) throw new KeyNotFoundException("مساحة الملفات غير متاحة.");
        await authorization.RequireSchoolPermissionAsync(school, PermissionNames.StorageViewSchool, ct);
        var root = await scopes.GetSchoolDriveRootAsync(school, ct) ?? throw new StorageUnavailableException();
        provider.ResetRequestCache();
        if (await provider.MetadataAsync(school, root.RootItemId, ct) is not { IsFolder: true, Trashed: false } rootMeta ||
            (rootMeta.SharedDriveId ?? "") != root.DriveId) throw new StorageUnavailableException();
        var rows = await repository.RowsAsync(school, ct);
        var context = await library.ContextAsync(school, ct);
        var children = await Children(root.RootItemId);
        var canDuty = (await duty.AccessAsync(school, user.UserId!, ct))?.Manager == true;
        var canVisits = false;
        try { await visits.OperationsStatusAsync(ct); canVisits = true; }
        catch (UnauthorizedSchoolAccessException) { }
        catch (KeyNotFoundException) { }
        var spaces = new List<StorageMapSpace>();
        foreach (var (key, name, kind, allowed) in new[] {
            ("library", "مكتبة المدرسة", StorageFolderKind.SchoolLibrary, true),
            ("teachers", "ملفات المعلمين", StorageFolderKind.Teacher, true),
            ("duty", "المناوبات", StorageFolderKind.DutyArchive, canDuty),
            ("visits", "أرشيف الزيارات", StorageFolderKind.VisitArchive, canVisits) })
        {
            var tracked = rows.SingleOrDefault(x => x.Folder.ParentFolderId == null && x.Folder.Kind == kind);
            var matches = children.Where(x => x.Name == name).ToArray();
            var live = kind == StorageFolderKind.Teacher ? matches.SingleOrDefaultSafe() : children.FirstOrDefault(x => x.Id == tracked?.Folder.DriveItemId);
            var state = matches.Length > 1 ? "Conflict" : live is null ? tracked is null ? "Missing" : "Unavailable" :
                !live.IsFolder || live.Trashed || !live.Parents.Contains(root.RootItemId) || live.Name != name ||
                (live.SharedDriveId ?? "") != root.DriveId ? "Moved" : tracked is null && kind != StorageFolderKind.Teacher ? "Unindexed" : "Available";
            spaces.Add(new(key, name, state, allowed && state == "Available", tracked?.Folder.Id));
        }
        var nodes = new List<StorageMapNode>();
        var libraryRoot = spaces.Single(x => x.Key == "library");
        if (libraryRoot.CanOpen)
        {
            var folders = await Children(rows.Single(x => x.Folder.Id == libraryRoot.FolderId).Folder.DriveItemId);
            var domains = rows.Where(x => x.Folder.Kind == StorageFolderKind.SchoolLibrary && x.Folder.ParentFolderId == libraryRoot.FolderId).ToArray();
            // Provider I/O alone may run concurrently. Scoped EF queries were completed above.
            foreach (var batch in domains.Chunk(4))
            {
                var branches = await Task.WhenAll(batch.Select(async domain => {
                    var live = folders.FirstOrDefault(x => x.Id == domain.Folder.DriveItemId);
                    var valid = Valid(live, domain, rows.Single(x => x.Folder.Id == libraryRoot.FolderId).Folder.DriveItemId);
                    return (Row: domain, Valid: valid, Children: valid ? await Children(domain.Folder.DriveItemId) : []);
                }));
                foreach (var branch in branches)
                {
                    nodes.Add(new(branch.Row.Folder.Id, libraryRoot.FolderId, branch.Row.Folder.DisplayName, branch.Row.Files, branch.Valid ? "Available" : "Unavailable"));
                    if (!branch.Valid) continue;
                    foreach (var row in rows.Where(x => x.Folder.ParentFolderId == branch.Row.Folder.Id && x.Folder.Kind == StorageFolderKind.SchoolLibrary))
                        nodes.Add(new(row.Folder.Id, branch.Row.Folder.Id, row.Folder.DisplayName, row.Files,
                            Valid(branch.Children.FirstOrDefault(x => x.Id == row.Folder.DriveItemId), row, branch.Row.Folder.DriveItemId) ? "Available" : "Unavailable"));
                }
            }
        }
        await authorization.RequireSchoolPermissionAsync(school, PermissionNames.StorageViewSchool, ct);
        return new(context.SchoolName, context.AcademicYearName, DateTimeOffset.UtcNow, spaces, nodes,
            nodes.Sum(x=>x.Files)+(libraryRoot.CanOpen?rows.Single(x=>x.Folder.Id==libraryRoot.FolderId).Files:0));

        bool Valid(GoogleDriveFile? live, StorageMapRow row, string parent) => live is { IsFolder: true, Trashed: false } &&
            live.Name == row.Folder.DisplayName && live.Parents.Contains(parent) && (live.SharedDriveId ?? "") == root.DriveId;
        async Task<IReadOnlyList<GoogleDriveFile>> Children(string parent)
        {
            var result = new List<GoogleDriveFile>(); string? token = null;
            do { var page = await provider.ChildrenAsync(school, parent, root.DriveId, token, ct);
                result.AddRange(page.Files.Where(x => !x.Trashed && x.Parents.Contains(parent))); token = page.NextPageToken;
                if (result.Count > 5000) throw new StorageUnavailableException("البنية كبيرة؛ افتح المكتبة لتصفحها بالصفحات.");
            } while (token is not null);
            return result;
        }
    }
}

internal static class StorageMapCollection
{
    public static T? SingleOrDefaultSafe<T>(this IReadOnlyList<T> items) where T : class => items.Count == 1 ? items[0] : null;
}
