using AlFalah.Application.Common;
using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;

namespace AlFalah.Application.Storage;

public sealed class SchoolDriveFolderService(ISchoolDriveSetupRepository repository, ICurrentUserService user,
    IGoogleDriveSetupReader drive) : ISchoolDriveFolderService
{
    private int School => user.ActiveSchoolId ?? throw Denied();
    private async Task<SchoolDriveSetupConnection> AuthorizeAsync(CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.UserId is null || !await repository.CanConfigureAsync(user.UserId, School, ct)) throw Denied();
        var connection = await repository.ConnectionAsync(School, ct);
        if (connection is null || !connection.HasCredential) throw new BusinessRuleException("اربط حساب Google أولًا، ثم اختر مجلد المدرسة.");
        return connection;
    }
    private async Task ReauthorizeAsync(SchoolDriveSetupConnection original, CancellationToken ct)
    {
        if ((await AuthorizeAsync(ct)).UpdatedAtUtc != original.UpdatedAtUtc) throw new StorageConflictException();
    }
    public async Task<SchoolDriveFolderPage> BrowseAsync(SchoolDriveBrowseRequest request, CancellationToken ct = default)
    {
        ValidateId(request.ParentItemId, optional: true);
        if (request.PageToken?.Length > 2048 || request.Search?.Length > 200) throw new ArgumentException("معايير البحث غير صالحة.");
        var connection = await AuthorizeAsync(ct);
        var cache = new Dictionary<string, GoogleDriveFile?>(StringComparer.Ordinal);
        var accountRoot = await FolderAsync("root", cache, ct);
        var parent = await FolderAsync(request.ParentItemId ?? connection.SharedDriveId ?? accountRoot.Id, cache, ct);
        var boundaries = await repository.BoundariesAsync(School, ct);
        await EnsureNotForeignAsync(parent.Id, boundaries, cache, ct);
        var page = await drive.ChildrenAsync(School, new(parent.Id, request.Search, "folder,name", 50, request.PageToken, parent.SharedDriveId), ct);
        var items = new List<SchoolDriveBrowseItem>();
        foreach (var item in page.Files.Where(x => !x.Trashed && x.Parents.Contains(parent.Id)))
        {
            // A configured root belonging to another school is never disclosed here.
            if (boundaries.Any(x => x.OtherSchool && x.ItemId == item.Id)) continue;
            items.Add(new(item.Id, item.Name, item.IsFolder, item.Size, item.MimeType,
                item.IsFolder && item.CanAddChildren == true && item.Id != accountRoot.Id));
        }
        var crumbs = new List<DriveBreadcrumbDto>();
        var current = parent;
        var seen = new HashSet<string>();
        while (seen.Add(current.Id) && crumbs.Count < 32)
        {
            crumbs.Insert(0, new(current.Id, current.Id == accountRoot.Id ? "ملفاتي" : current.Name));
            if (current.Id == accountRoot.Id || current.Parents.Count == 0) break;
            var ancestor = await GetAsync(current.Parents[0], cache, ct);
            if (ancestor is null || ancestor.Trashed || !ancestor.IsFolder) break;
            current = ancestor;
        }
        await ReauthorizeAsync(connection, ct);
        return new(parent.Id, parent.Id == accountRoot.Id ? "ملفاتي" : parent.Name, parent.Id == accountRoot.Id,
            parent.Id != accountRoot.Id && parent.CanAddChildren == true, crumbs, items, page.NextPageToken);
    }
    public async Task<SchoolDriveRootSelection> ValidateRootAsync(string itemId, CancellationToken ct = default)
    {
        ValidateId(itemId);
        var connection = await AuthorizeAsync(ct);
        var cache = new Dictionary<string, GoogleDriveFile?>(StringComparer.Ordinal);
        var root = await FolderAsync("root", cache, ct);
        var selected = await FolderAsync(itemId, cache, ct);
        if (selected.Id == root.Id || selected.CanAddChildren != true)
            throw new ArgumentException("اختر مجلدًا مخصصًا للمدرسة ويمكن للحساب إضافة ملفات بداخله.");
        var boundaries = await repository.BoundariesAsync(School, ct);
        await EnsureNotForeignAsync(selected.Id, boundaries, cache, ct);
        foreach (var boundary in boundaries)
        {
            if (boundary.OtherSchool)
            {
                if (await WithinAsync(selected.Id, boundary.ItemId, cache, ct)) throw new ArgumentException("المجلد يتداخل مع مجلد مدرسة أخرى.");
            }
            else if (boundary.ItemId == selected.Id && boundary.TeacherGrant ||
                !await WithinAsync(selected.Id, boundary.ItemId, cache, ct))
                throw new ArgumentException("المجلد المختار يجب أن يحتوي مجلدات المدرسة ومنح المعلمين الحالية، ولا يكون منحة معلم.");
        }
        await ReauthorizeAsync(connection, ct);
        return new(selected.Id, selected.Name, selected.SharedDriveId);
    }
    private async Task EnsureNotForeignAsync(string item, IReadOnlyList<SchoolDriveFolderBoundary> boundaries,
        Dictionary<string, GoogleDriveFile?> cache, CancellationToken ct)
    {
        foreach (var other in boundaries.Where(x => x.OtherSchool))
            if (await WithinAsync(other.ItemId, item, cache, ct)) throw Denied();
    }
    private async Task<bool> WithinAsync(string root, string item, Dictionary<string, GoogleDriveFile?> cache, CancellationToken ct)
    {
        var seen = new HashSet<string>();
        while (seen.Add(item) && seen.Count <= 64)
        {
            if (item == root) return true;
            var metadata = await GetAsync(item, cache, ct);
            if (metadata is null || metadata.Trashed || metadata.Parents.Count != 1) return false;
            item = metadata.Parents[0];
        }
        return false;
    }
    private async Task<GoogleDriveFile?> GetAsync(string item, Dictionary<string, GoogleDriveFile?> cache, CancellationToken ct)
    {
        if (!cache.TryGetValue(item, out var value)) cache[item] = value = await drive.FolderAsync(School, item, ct);
        return value;
    }
    private async Task<GoogleDriveFile> FolderAsync(string item, Dictionary<string, GoogleDriveFile?> cache, CancellationToken ct)
    {
        var value = await GetAsync(item, cache, ct);
        return value is { IsFolder: true, Trashed: false } ? value : throw new KeyNotFoundException("المجلد غير متاح في الحساب المرتبط.");
    }
    private static void ValidateId(string? id, bool optional = false)
    {
        if (optional && id is null) return;
        if (string.IsNullOrWhiteSpace(id) || id.Length > 256 || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
            throw new ArgumentException("معرّف المجلد غير صالح.");
    }
    private static UnauthorizedSchoolAccessException Denied() => new("اختيار مجلد المدرسة متاح لمدير المدرسة أو مدير المنصة داخل المدرسة المختارة فقط.");
}
