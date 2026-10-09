using System.Security.Cryptography;
using System.Text;
using AlFalah.Application.Common;
using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;
using Microsoft.Extensions.Options;

namespace AlFalah.Application.Storage;

// The school root is configured separately. Setup only creates its known children.
public sealed class StorageSetupService(IStorageSetupRepository repository, IStorageRepository scopes,
    IStorageLibraryRepository library, IStorageAuthorizationService authorization, ICurrentUserService user, IStorageProvider provider,
    ITeacherDriveMappingService mappings, IOptions<StorageOptions> options) : IStorageSetupService
{
    private static readonly (string Code, string Name)[] Domains =
    [ ("1", "الإدارة المدرسية"), ("2", "التعليم والتعلم"), ("3", "نواتج التعلم"), ("4", "البيئة المدرسية") ];
    private int School => user.ActiveSchoolId ?? throw new UnauthorizedSchoolAccessException("اختر المدرسة أولًا.");
    private async Task AuthorizeAsync(CancellationToken ct)
    {
        if (!options.Value.AdministrationEnabled || !options.Value.ReadModelEnabled)
            throw new StorageUnavailableException("مساحة الملفات غير مفعّلة.");
        await authorization.RequireManagerAsync(School, ct);
        await authorization.RequireSchoolPermissionAsync(School, PermissionNames.StorageManageSchool, ct);
    }
    private async Task<StorageDriveRoot> AuthorizedRootAsync(CancellationToken ct)
    {
        await AuthorizeAsync(ct);
        var root = await scopes.GetSchoolDriveRootAsync(School, ct) ?? throw new StorageUnavailableException("اربط Drive المدرسة أولًا.");
        provider.ResetRequestCache();
        if (await provider.MetadataAsync(School, root.RootItemId, ct) is not { IsFolder: true, Trashed: false })
            throw new StorageUnavailableException("مجلد المدرسة الرئيسي غير متاح على Drive.");
        return root;
    }

    internal static string TeacherName(SetupTeacher teacher, IReadOnlyList<SetupTeacher> teachers)
    {
        var display = teacher.Name.Trim();
        if (display.Length > 190) display = display[..190].TrimEnd();
        if (teachers.Count(t => string.Equals(t.Name.Trim(), teacher.Name.Trim(), StringComparison.Ordinal)) > 1)
            display += $" ({teacher.Id})";
        return ValidatedStorageUpload.SafeName(display);
    }
    private static IEnumerable<(string Key, string ParentKey, string Name, StorageFolderKind Kind, int? TeacherId)> Structure(
        IReadOnlyList<SetupTeacher> teachers)
    {
        yield return ("library", "root", "مكتبة المدرسة", StorageFolderKind.SchoolLibrary, null);
        foreach (var domain in Domains)
        {
            yield return ($"domain:{domain.Code}", "library", $"{domain.Code} - {domain.Name}", StorageFolderKind.SchoolLibrary, null);
            foreach (var standard in RequirementCatalogService.Standards.Where(s => s.Code.StartsWith(domain.Code + ".", StringComparison.Ordinal)))
                yield return ($"standard:{standard.Code}", $"domain:{domain.Code}", $"{standard.Code} - {standard.Name}", StorageFolderKind.SchoolLibrary, null);
        }
        yield return ("teachers-root", "root", "ملفات المعلمين", StorageFolderKind.Teacher, null);
        yield return ("archive", "root", "أرشيف الزيارات", StorageFolderKind.VisitArchive, null);
        foreach (var teacher in teachers)
            yield return ($"visit-teacher:{teacher.Id}", "archive", TeacherName(teacher, teachers), StorageFolderKind.VisitArchive, teacher.Id);
    }

    private async Task<IReadOnlyList<GoogleDriveFile>> ChildrenAsync(StorageDriveRoot root, string parent, CancellationToken ct)
    {
        var items = new List<GoogleDriveFile>();
        string? page = null;
        do
        {
            var result = await provider.ChildrenAsync(School, parent, root.DriveId, page, ct);
            items.AddRange(result.Files.Where(f => !f.Trashed && f.Parents.Contains(parent)));
            page = result.NextPageToken;
        } while (page != null);
        return items;
    }

    private async Task EnsureOutsideTeacherGrantsAsync(StorageDriveRoot root, string itemId, CancellationToken ct)
    {
        foreach (var grant in await library.TeacherRootsAsync(School, ct))
            if (grant.DriveId != root.DriveId ||
                await provider.IsWithinAsync(School, grant.RootItemId, itemId, ct) ||
                await provider.IsWithinAsync(School, itemId, grant.RootItemId, ct))
                throw new StorageConflictException();
    }

    private static (string State, GoogleDriveFile? Match) Match(IReadOnlyList<GoogleDriveFile> children, string name)
    {
        var matches = children.Where(f => string.Equals(f.Name, name, StringComparison.Ordinal)).ToArray();
        if (matches.Length > 1 || matches.Length == 1 && !matches[0].IsFolder) return ("Conflict", null);
        return matches.Length == 1 ? ("Exists", matches[0]) : ("Missing", null);
    }

    private static (string State, GoogleDriveFile? Match) TeacherMatch(IReadOnlyList<GoogleDriveFile> children,
        SetupTeacher teacher, IReadOnlyList<SetupTeacher> teachers)
    {
        var canonical = Match(children, TeacherName(teacher, teachers));
        if (canonical.State != "Missing") return canonical;
        if (teachers.Count(t => string.Equals(t.Name, teacher.Name, StringComparison.Ordinal)) != 1)
            return ("Missing", null);
        var granted = teachers.Where(t => t.FolderItemId != null).Select(t => t.FolderItemId).ToHashSet(StringComparer.Ordinal);
        var legacy = children.Where(f => f.IsFolder && !granted.Contains(f.Id) &&
            (f.Name == teacher.Name || f.Name == "ملف المعلم - " + teacher.Name ||
                f.Name == $"ملف المعلم - {teacher.Name} - {teacher.Id}")).ToArray();
        if (legacy.Length == 1) return ("Exists", legacy[0]);
        if (legacy.Length > 1) return ("Conflict", null);
        // A likely pre-existing file with a different naming convention needs human review.
        if (children.Any(f => f.IsFolder && !granted.Contains(f.Id) &&
            f.Name != "مكتبة المدرسة" && f.Name != "أرشيف الزيارات" &&
            f.Name.Contains(teacher.Name, StringComparison.OrdinalIgnoreCase))) return ("NeedsReview", null);
        return ("Missing", null);
    }

    public async Task<StorageSetupPlan> PlanAsync(CancellationToken ct = default)
    {
        var root = await AuthorizedRootAsync(ct);
        var teacherRows = await repository.TeachersAsync(School, ct);
        var available = new Dictionary<string, string> { ["root"] = root.RootItemId };
        var trackedRows = new Dictionary<string, int?> { ["root"] = null };
        var folders = new List<SetupFolderItem>();
        var recoveryIssues = new List<SetupRecoveryIssue>();
        var childrenCache = new Dictionary<string, IReadOnlyList<GoogleDriveFile>>();
        async Task<IReadOnlyList<GoogleDriveFile>> Children(string parent)
        {
            if (!childrenCache.TryGetValue(parent, out var items)) childrenCache[parent] = items = await ChildrenAsync(root, parent, ct);
            return items;
        }
        foreach (var node in Structure(teacherRows))
        {
            if (!available.TryGetValue(node.ParentKey, out var parent))
            { folders.Add(new(node.Key, node.Name, node.ParentKey, "ParentPending")); continue; }
            var match = Match(await Children(parent), node.Name);
            var tracked = node.Key == "teachers-root" ? null : node.ParentKey == "root"
                ? await repository.TrackedRootAsync(School, node.Kind, ct)
                : trackedRows.TryGetValue(node.ParentKey, out var parentRow) && parentRow is int parentId
                    ? node.TeacherId is int teacherId
                        ? await repository.TrackedArchiveTeacherAsync(School, parentId, teacherId, ct)
                        : await repository.TrackedChildAsync(School, parentId, node.Name, ct) : null;
            if (node.TeacherId != null && match.State == "Missing" && tracked != null &&
                await provider.MetadataAsync(School, tracked.DriveItemId, ct) is { IsFolder: true, Trashed: false } live &&
                live.Parents.Contains(parent)) match = ("NeedsRename", live);
            var needsRecovery = false;
            if (tracked != null &&
                (await provider.MetadataAsync(School, tracked.DriveItemId, ct) is null or { Trashed: true }) &&
                await repository.HasProtectedDataAsync(School, tracked.Id, ct))
            {
                needsRecovery = true;
                var impact = await repository.RecoveryImpactAsync(School, tracked.Id, ct);
                recoveryIssues.Add(new(node.Key, node.Name, impact.FolderCount, impact.FileCount, impact.OperationCount,
                    "فصل الربط القديم سيُخفي الملفات والروابط السابقة من المكتبة الجديدة. ستبقى سجلاتها محفوظة للمراجعة، ولن تُستعاد الملفات المحذوفة من Drive تلقائيًا."));
            }
            folders.Add(new(node.Key, node.Name, node.ParentKey, needsRecovery ? "NeedsConfirmation" : match.State,
                needsRecovery ? "يحتوي الربط القديم على بيانات محفوظة ويحتاج موافقة المدير قبل إنشاء بديل." :
                match.State == "Conflict" ? "يوجد أكثر من عنصر بهذا الاسم أو أن العنصر ليس مجلدًا." : null));
            if (match.Match != null) { available[node.Key] = match.Match.Id; trackedRows[node.Key] = tracked?.Id; }
        }
        var schoolChildren = await Children(root.RootItemId);
        var teachers = new List<SetupTeacherItem>();
        foreach (var teacher in teacherRows)
        {
            if (teacher.FolderItemId != null)
            {
                var meta = await provider.MetadataAsync(School, teacher.FolderItemId, ct);
                var tracked = await repository.TrackedTeacherRootAsync(School, teacher.Id, ct);
                var orphaned = meta is { IsFolder: true, Trashed: false } &&
                    !await provider.IsWithinAsync(School, root.RootItemId, meta.Id, ct) &&
                    await HasOnlyMissingParentsAsync(meta, ct);
                var protectedOld = (meta is null or { Trashed: true } || orphaned) &&
                    (await repository.HasTeacherEvidenceAsync(School, teacher.Id, ct) ||
                    tracked != null && await repository.HasProtectedDataAsync(School, tracked.Id, ct));
                if (protectedOld)
                {
                    var impact = tracked == null ? new SetupRecoveryImpact(0, 0, 0) :
                        await repository.RecoveryImpactAsync(School, tracked.Id, ct);
                    recoveryIssues.Add(new($"teacher:{teacher.Id}", teacher.Name, impact.FolderCount, impact.FileCount,
                        impact.OperationCount, "سيُفصل منح المجلد القديم ويُنشأ ملف جديد للمعلم. تبقى الشواهد والملفات السابقة مسجلة للمراجعة ولا تُنقل تلقائيًا."));
                }
                var withinSchool = meta is { IsFolder: true, Trashed: false } &&
                    await provider.IsWithinAsync(School, root.RootItemId, meta.Id, ct);
                var inTeacherCollection = withinSchool && available.TryGetValue("teachers-root", out var teacherCollection) &&
                    meta!.Parents.Contains(teacherCollection) && meta.Name == TeacherName(teacher, teacherRows);
                teachers.Add(new(teacher.Id, teacher.Name, protectedOld ? "NeedsConfirmation" :
                    inTeacherCollection ? "Linked" : withinSchool ? "NeedsMove" : "InvalidGrant",
                    protectedOld ? "يحتاج موافقة المدير لفصل الربط القديم." :
                    meta is { IsFolder: true, Trashed: false } ? null : "المنح الحالي يشير إلى مجلد غير متاح؛ راجعه يدويًا."));
            }
            else
            {
                var teacherChildren = available.TryGetValue("teachers-root", out var teacherParent)
                    ? await Children(teacherParent) : schoolChildren;
                var match = TeacherMatch(teacherChildren, teacher, teacherRows);
                if (match.State == "Missing" && teacherParent != null)
                    match = TeacherMatch(schoolChildren, teacher, teacherRows);
                teachers.Add(new(teacher.Id, teacher.Name, match.State,
                    match.State == "Conflict" ? "يوجد اسم مكرر على Drive؛ راجعه قبل الربط." :
                    match.State == "NeedsReview" ? "وجدنا مجلدًا محتملًا لهذا المعلم؛ اربطه يدويًا قبل الإنشاء." : null));
            }
        }
        return new(folders, teachers, await repository.ArchiveEnabledAsync(School, ct), true, recoveryIssues);
    }

    private static string FolderKey(string parent, string identity) => "folder:" +
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(parent + "\n" + identity)));

    private async Task<(GoogleDriveFile Folder, bool Created)> EnsureAsync(StorageDriveRoot root, string parent, string name, CancellationToken ct,
        string? identity = null)
    {
        var key = FolderKey(parent, identity ?? name);
        var operation = (await repository.ReservationsForFolderAsync(School, parent, name, ct)).SingleOrDefault();
        if (operation != null) name = operation.DisplayName;
        var match = Match(await ChildrenAsync(root, parent, ct), name);
        if (match.State == "Conflict") throw new StorageConflictException();
        if (operation is { Status: "Completed" } && operation.ProviderItemId != match.Match?.Id)
        {
            var old = await provider.MetadataAsync(School, operation.ProviderItemId, ct);
            if (old is { IsFolder: true, Trashed: false }) throw new StorageConflictException();
            // The previously completed provider ID was deleted externally. Keep its audit
            // record and reserve a new stable ID; Drive may never reuse a deleted ID.
            var repairKey = FolderKey(parent, (identity ?? name) + ":repair:" + operation.Id);
            operation = new StorageOperation { SchoolId = School, ActorUserId = user.UserId!, Action = "CreateFolder",
                RequestKey = repairKey, Fingerprint = repairKey[7..], DriveId = root.DriveId,
                ParentItemId = parent, DisplayName = name,
                ProviderItemId = match.Match?.Id ?? await provider.AllocateIdAsync(School, ct),
                Status = match.Match is null ? "Pending" : "Completed" };
            await repository.ReserveAsync(operation, ct);
        }
        if (match.Match is not null)
        {
            if (operation != null && operation.ProviderItemId != match.Match.Id) throw new StorageConflictException();
            if (operation != null && operation.Status != "Completed")
            { operation.Status = "Completed"; await repository.SaveReservationAsync(operation, ct); }
            return (match.Match, false);
        }
        if (operation is null)
        {
            var hash = key[7..];
            operation = new StorageOperation { SchoolId = School, ActorUserId = user.UserId!, Action = "CreateFolder",
                RequestKey = key, Fingerprint = hash, DriveId = root.DriveId, ParentItemId = parent,
                DisplayName = name, ProviderItemId = await provider.AllocateIdAsync(School, ct) };
            await repository.ReserveAsync(operation, ct);
        }
        if (operation.ParentItemId != parent || operation.DisplayName != name || operation.DriveId != root.DriveId)
            throw new StorageConflictException();
        provider.ResetRequestCache();
        var result = await provider.MetadataAsync(School, operation.ProviderItemId, ct);
        var created = result is null;
        if (result is null)
        {
            result = await provider.CreateFolderAsync(School, operation.ProviderItemId, parent, name, ct);
            provider.ResetRequestCache();
        }
        if (!result.IsFolder || result.Trashed || result.Name != name || !result.Parents.Contains(parent))
            throw new StorageUnavailableException("فشل التحقق من المجلد المنشأ على Drive.");
        operation.Status = "Completed";
        await repository.SaveReservationAsync(operation, ct);
        return (result, created);
    }

    private async Task<bool> HasOnlyMissingParentsAsync(GoogleDriveFile folder, CancellationToken ct)
    {
        // Drive can still return a grandchild after an ancestor was deleted directly.
        // A live chain (including a deliberate move) must be reviewed, not replaced.
        return await BrokenParentsAsync(folder, new HashSet<string> { folder.Id }, ct);
    }

    private async Task<bool> BrokenParentsAsync(GoogleDriveFile folder, HashSet<string> visited, CancellationToken ct)
    {
        if (folder.Parents.Count == 0) return false;
        foreach (var parentId in folder.Parents)
        {
            if (!visited.Add(parentId)) return false;
            var parent = await provider.MetadataAsync(School, parentId, ct);
            if (parent is null or { Trashed: true }) continue;
            if (!parent.IsFolder || !await BrokenParentsAsync(parent, visited, ct)) return false;
        }
        return true;
    }

    public Task<StorageSetupResult> ApplyAsync(bool folders, bool teachers, CancellationToken ct = default,
        IReadOnlyList<string>? recoveryKeys = null) =>
        ApplyCoreAsync(folders, teachers, null, recoveryKeys, ct);

    public Task<StorageSetupResult> ApplyWithProgressAsync(bool folders, bool teachers,
        Func<StorageSetupProgress, Task> progress, CancellationToken ct = default,
        IReadOnlyList<string>? recoveryKeys = null) =>
        ApplyCoreAsync(folders, teachers, progress, recoveryKeys, ct);

    private async Task<StorageSetupResult> ApplyCoreAsync(bool folders, bool teachers,
        Func<StorageSetupProgress, Task>? progress, IReadOnlyList<string>? recoveryKeys, CancellationToken ct)
    {
        if (!folders && !teachers) throw new ArgumentException("اختر المجلدات المطلوب إعدادها.");
        var root = await AuthorizedRootAsync(ct);
        var allTeacherRows = await repository.TeachersAsync(School, ct);
        var teacherRows = teachers ? allTeacherRows : [];
        var total = (folders ? Structure(allTeacherRows).Count() : 0) + teacherRows.Count + 2;
        var completed = 0;
        async Task Report(string stage, string label)
        {
            if (progress is not null) await progress(new(stage, label, completed, total));
        }
        await Report("scan", "تم التحقق من اتصال Drive وجذر المدرسة");
        completed++;
        var created = 0; var linked = 0; var already = 0;
        var warnings = new List<string>();
        if (!await repository.ExclusiveAsync(School, async token =>
        {
            var known = new Dictionary<string, (string ItemId, int? RowId)> { ["root"] = (root.RootItemId, null) };
            if (folders)
            {
                foreach (var node in Structure(allTeacherRows))
                {
                    await Report("folders", node.Name);
                    if (!known.TryGetValue(node.ParentKey, out var parent))
                    { completed++; await Report("folders", node.Name); continue; }
                    try
                    {
                        var tracked = node.Key == "teachers-root" ? null : node.ParentKey == "root"
                            ? await repository.TrackedRootAsync(School, node.Kind, token)
                            : parent.RowId is int parentId
                                ? node.TeacherId is int teacherId
                                    ? await repository.TrackedArchiveTeacherAsync(School, parentId, teacherId, token)
                                    : await repository.TrackedChildAsync(School, parentId, node.Name, token) : null;
                        GoogleDriveFile target;
                        bool wasCreated;
                        if (tracked != null)
                        {
                            var current = await provider.MetadataAsync(School, tracked.DriveItemId, token);
                            if (current is { IsFolder: true, Trashed: false } && current.Parents.Contains(parent.ItemId))
                            {
                                target = current; wasCreated = false;
                                if (node.TeacherId != null && target.Name != node.Name)
                                {
                                    if (Match(await ChildrenAsync(root, parent.ItemId, token), node.Name).State != "Missing")
                                        throw new StorageConflictException();
                                    target = await provider.RenameAsync(School, target.Id, node.Name, token);
                                    provider.ResetRequestCache();
                                    await repository.RebindFolderAsync(tracked, root.DriveId, target.Id, target.Name, user.UserId!, token);
                                }
                            }
                            else if (current is null or { Trashed: true } ||
                                current is { IsFolder: true, Trashed: false } &&
                                await HasOnlyMissingParentsAsync(current, token))
                            {
                                if (await repository.HasProtectedDataAsync(School, tracked.Id, token))
                                {
                                    if (recoveryKeys?.Contains(node.Key, StringComparer.Ordinal) != true)
                                        throw new StorageUnavailableException("المجلد محذوف من Drive وله ملفات أو عمليات محفوظة؛ يلزم مراجعة الأثر والموافقة على فصل الربط القديم.");
                                    // Prepare the replacement before detaching historical links. A duplicate
                                    // name or failed Drive create must leave the existing database links intact.
                                    (target, wasCreated) = await EnsureAsync(root, parent.ItemId, node.Name, token);
                                    await repository.DeactivateSubtreeAsync(School, tracked.Id, user.UserId!, token);
                                    warnings.Add($"فُصل الربط القديم لـ«{node.Name}» بعد موافقة المدير. السجلات التاريخية محفوظة وتحتاج مراجعة.");
                                    tracked = null;
                                }
                                else (target, wasCreated) = await EnsureAsync(root, parent.ItemId, node.Name, token);
                                if (tracked != null)
                                    await repository.RebindFolderAsync(tracked, root.DriveId, target.Id, target.Name, user.UserId!, token);
                                linked++;
                            }
                            else throw new StorageUnavailableException("المجلد المسجل نُقل أو تغيّر نوعه على Drive؛ راجعه قبل إعادة الربط.");
                        }
                        else (target, wasCreated) = await EnsureAsync(root, parent.ItemId, node.Name, token);
                        if (node.ParentKey == "root" && node.Key != "teachers-root") await EnsureOutsideTeacherGrantsAsync(root, target.Id, token);
                        if (wasCreated) created++; else already++;
                        if (node.Key == "teachers-root")
                        { known[node.Key] = (target.Id, null); completed++; await Report("folders", node.Name); continue; }
                        tracked ??= await repository.TrackedFolderAsync(School, target.Id, token);
                        if (tracked == null)
                        {
                            tracked = new StorageFolder { SchoolId = School, ParentFolderId = parent.RowId, Kind = node.Kind,
                                OwnerTeacherId = node.TeacherId, DisplayName = target.Name, DriveId = root.DriveId,
                                DriveItemId = target.Id, CreatedByUserId = user.UserId };
                            await repository.TrackAsync(tracked, token); linked++;
                        }
                        if (tracked.Kind != node.Kind || tracked.ParentFolderId != parent.RowId || tracked.OwnerTeacherId != node.TeacherId)
                            throw new StorageConflictException();
                        known[node.Key] = (target.Id, tracked.Id);
                    }
                    catch (StorageConflictException) { warnings.Add($"المجلد «{node.Name}» موجود باسم مكرر أو مرتبط بشكل متعارض؛ لم يُنشأ بديل له."); }
                    catch (StorageUnavailableException e) { warnings.Add($"تعذّر إعداد «{node.Name}»: {e.Message}"); }
                    if (!known.ContainsKey(node.Key))
                    {
                        foreach (var child in Structure(allTeacherRows).Where(n => n.ParentKey == node.Key)) warnings.Add($"تجاوزنا «{child.Name}» حتى يُراجع المجلد الأب.");
                    }
                    completed++;
                    await Report("folders", node.Name);
                }
            }
            if (teachers)
            {
                var teacherParent = known.TryGetValue("teachers-root", out var preparedParent)
                    ? await provider.MetadataAsync(School, preparedParent.ItemId, token)
                    : (await EnsureAsync(root, root.RootItemId, "ملفات المعلمين", token)).Folder;
                if (teacherParent is not { IsFolder: true, Trashed: false } ||
                    !teacherParent.Parents.Contains(root.RootItemId))
                    throw new StorageUnavailableException("مجلد ملفات المعلمين غير متاح تحت جذر المدرسة.");
                foreach (var teacher in teacherRows)
                {
                    await Report("teachers", teacher.Name);
                    var trackedTeacher = await repository.TrackedTeacherRootAsync(School, teacher.Id, token);
                    GoogleDriveFile? current = null;
                    var detachOldTeacher = false;
                    if (teacher.FolderItemId != null)
                    {
                        current = await provider.MetadataAsync(School, teacher.FolderItemId, token);
                        if (current is { Trashed: false } &&
                            (!current.IsFolder || !await provider.IsWithinAsync(School, root.RootItemId, current.Id, token)) &&
                            !await HasOnlyMissingParentsAsync(current, token))
                        { warnings.Add($"ملف المعلم «{teacher.Name}» نُقل أو تغيّر نوعه؛ راجع المنح قبل إعادة الربط."); completed++; await Report("teachers", teacher.Name); continue; }
                        if ((current is null or { Trashed: true } || !await provider.IsWithinAsync(School, root.RootItemId, current.Id, token)) &&
                            (await repository.HasTeacherEvidenceAsync(School, teacher.Id, token) ||
                            trackedTeacher != null && await repository.HasProtectedDataAsync(School, trackedTeacher.Id, token))
                           )
                        {
                            if (recoveryKeys?.Contains($"teacher:{teacher.Id}", StringComparer.Ordinal) != true)
                            { warnings.Add($"ملف المعلم «{teacher.Name}» محذوف وله شواهد أو ملفات مسجلة؛ يحتاج موافقة على فصل الربط القديم."); completed++; await Report("teachers", teacher.Name); continue; }
                            detachOldTeacher = true;
                        }
                        if (current is null or { Trashed: true } ||
                            !await provider.IsWithinAsync(School, root.RootItemId, current.Id, token)) current = null;
                    }
                    var name = TeacherName(teacher, teacherRows);
                    try
                    {
                        var match = TeacherMatch(await ChildrenAsync(root, teacherParent.Id, token), teacher, teacherRows);
                        if (match.State is "Conflict" or "NeedsReview")
                        { warnings.Add($"ملف المعلم «{teacher.Name}» يحتاج مراجعة الاسم الموجود على Drive؛ لم ننشئ نسخة أخرى."); completed++; await Report("teachers", teacher.Name); continue; }
                        if (current != null && match.Match != null && match.Match.Id != current.Id)
                        { warnings.Add($"يوجد مجلد آخر باسم «{name}» داخل ملفات المعلمين؛ راجعه قبل النقل."); completed++; await Report("teachers", teacher.Name); continue; }
                        var legacy = current == null && match.Match == null
                            ? TeacherMatch(await ChildrenAsync(root, root.RootItemId, token), teacher, teacherRows) : default;
                        if (legacy.State is "Conflict" or "NeedsReview")
                        { warnings.Add($"ملف المعلم «{teacher.Name}» يحتاج مراجعة مجلد قديم مشابه قبل إنشاء نسخة جديدة."); completed++; await Report("teachers", teacher.Name); continue; }
                        var folder = current ?? match.Match ?? legacy.Match;
                        var wasCreated = false;
                        if (folder == null) (folder, wasCreated) = await EnsureAsync(root, teacherParent.Id, name, token, $"teacher:{teacher.Id}");
                        else
                        {
                            if (!folder.Parents.Contains(teacherParent.Id))
                            {
                                if (!await provider.IsWithinAsync(School, root.RootItemId, folder.Id, token) || folder.Parents.Count != 1)
                                    throw new StorageUnavailableException("مجلد المعلم خارج نطاق المدرسة أو له أكثر من موضع؛ راجعه يدويًا.");
                                folder = await provider.MoveAsync(School, folder.Id, folder.Parents[0], teacherParent.Id, token);
                                provider.ResetRequestCache();
                            }
                            if (folder.Name != name)
                            { folder = await provider.RenameAsync(School, folder.Id, name, token); provider.ResetRequestCache(); }
                        }
                        if (!detachOldTeacher && trackedTeacher != null && (trackedTeacher.DriveItemId != folder.Id || trackedTeacher.DisplayName != folder.Name))
                            await repository.RebindFolderAsync(trackedTeacher, root.DriveId, folder.Id, folder.Name, user.UserId!, token);
                        await mappings.UpsertAsync(teacher.Id, new UpsertDriveFolderMappingRequest(folder.Id), token);
                        if (detachOldTeacher)
                        {
                            if (trackedTeacher != null)
                                await repository.DeactivateSubtreeAsync(School, trackedTeacher.Id, user.UserId!, token);
                            warnings.Add($"فُصل منح «{teacher.Name}» القديم بعد موافقة المدير. تبقى الشواهد القديمة للمراجعة.");
                        }
                        if (wasCreated) created++; else already++;
                        linked++;
                    }
                    catch (StorageConflictException) { warnings.Add($"ملف المعلم «{teacher.Name}» موجود باسم مكرر؛ لم ننشئ نسخة أخرى."); }
                    catch (StorageUnavailableException e) { warnings.Add($"ملف المعلم «{teacher.Name}»: {e.Message}"); }
                    catch (InvalidOperationException e) { warnings.Add($"تعذّر ربط ملف «{teacher.Name}»: {e.Message}"); }
                    completed++;
                    await Report("teachers", teacher.Name);
                }
            }
        }, ct)) throw new StorageConflictException();
        await Report("verify", "التحقق من بنية الملفات والروابط");
        var plan = await PlanAsync(ct);
        completed++;
        await Report("complete", warnings.Count == 0 ? "اكتمل الإعداد بنجاح" : "اكتمل الإعداد مع عناصر تحتاج مراجعة");
        return new(plan, created, linked, already, warnings);
    }

    public async Task<VisitArchiveActivation> ArchiveStatusAsync(CancellationToken ct = default)
    {
        await AuthorizeAsync(ct);
        return new(await repository.ArchiveEnabledAsync(School, ct));
    }

    public async Task<VisitArchiveActivation> SetArchiveEnabledAsync(bool enabled, CancellationToken ct = default)
    {
        if (!enabled)
        {
            // Stopping external writes must remain possible even when Drive is unavailable.
            await AuthorizeAsync(ct);
            await repository.SetArchiveEnabledAsync(School, false, user.UserId!, ct);
            return new(false);
        }

        var root = await AuthorizedRootAsync(ct);
        if (!await repository.ExclusiveAsync(School, async token =>
        {
            var tracked = await repository.TrackedRootAsync(School, StorageFolderKind.VisitArchive, token);
            var current = tracked is null ? null : await provider.MetadataAsync(School, tracked.DriveItemId, token);
            GoogleDriveFile folder;
            if (tracked is null) folder = (await EnsureAsync(root, root.RootItemId, "أرشيف الزيارات", token)).Folder;
            else if (current is { IsFolder: true, Trashed: false } && current.Parents.Contains(root.RootItemId)) folder = current;
            else if (current is null or { Trashed: true })
            {
                if (await repository.HasProtectedDataAsync(School, tracked.Id, token))
                    throw new StorageUnavailableException("مجلد الأرشيف محذوف وله تقارير مسجلة؛ راجعها قبل إنشاء بديل.");
                folder = (await EnsureAsync(root, root.RootItemId, "أرشيف الزيارات", token)).Folder;
                await repository.RebindFolderAsync(tracked, root.DriveId, folder.Id, folder.Name, user.UserId!, token);
            }
            else throw new StorageUnavailableException("مجلد الأرشيف المسجل نُقل أو تغيّر نوعه على Drive.");
            if (!folder.IsFolder || folder.Trashed || !folder.Parents.Contains(root.RootItemId))
                throw new StorageUnavailableException("مجلد الأرشيف غير متاح في جذر المدرسة.");
            if (folder.CanAddChildren == false)
                throw new StorageUnavailableException("حساب المدرسة لا يملك صلاحية إضافة ملفات داخل مجلد الأرشيف.");
            await EnsureOutsideTeacherGrantsAsync(root, folder.Id, token);
            var existing = await repository.TrackedFolderAsync(School, folder.Id, token);
            if (existing is null)
                await repository.TrackAsync(new StorageFolder { SchoolId = School, Kind = StorageFolderKind.VisitArchive,
                    DisplayName = folder.Name, DriveId = root.DriveId, DriveItemId = folder.Id, CreatedByUserId = user.UserId }, token);
            else if (existing.Kind != StorageFolderKind.VisitArchive || existing.ParentFolderId != null || existing.OwnerTeacherId != null)
                throw new StorageConflictException();
            await repository.SetArchiveEnabledAsync(School, true, user.UserId!, token);
        }, ct)) throw new StorageConflictException();
        return new(true);
    }
}
