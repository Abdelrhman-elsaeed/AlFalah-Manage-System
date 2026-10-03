using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlFalah.Application.Common;
using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;
using Microsoft.Extensions.Options;

namespace AlFalah.Application.Storage;

public sealed class StorageLibraryService(IStorageLibraryRepository repository, IStorageRepository scopes,
    IStorageAuthorizationService authorization, ICurrentUserService user, IStorageProvider provider,
    IEvidenceSubmissionService submissions, IOptions<StorageOptions> options) : IStorageLibraryService
{
    private int School => user.ActiveSchoolId ?? throw new UnauthorizedSchoolAccessException("اختر المدرسة أولًا.");
    private void Enabled()
    {
        provider.ResetRequestCache();
        if (!options.Value.ReadModelEnabled) throw new KeyNotFoundException("مساحة الملفات غير مفعّلة.");
    }
    private async Task<StorageTeacher?> ActorAsync(bool own, bool mutation, CancellationToken ct)
    {
        Enabled();
        var scope = await authorization.RequireScopeAsync(School, ct);
        if (!own)
        {
            await authorization.RequireSchoolPermissionAsync(School, mutation ? PermissionNames.StorageManageSchool : PermissionNames.StorageViewSchool, ct);
            return null;
        }
        if (!await scopes.HasPermissionAsync(scope.UserId, School, mutation ? PermissionNames.StorageManageOwn : PermissionNames.StorageViewOwn, ct))
            throw new UnauthorizedSchoolAccessException("لا تملك صلاحية ملفات المعلم.");
        var teacher = await repository.FindTeacherAsync(School, scope.UserId, ct)
            ?? throw new KeyNotFoundException("لا يوجد مجلد ممنوح لك. تواصل مع مدير المدرسة.");
        var school = await SchoolRootAsync(ct);
        if (school.DriveId != teacher.Root.DriveId || teacher.Root.RootItemId == school.RootItemId ||
            !await provider.IsWithinAsync(School, school.RootItemId, teacher.Root.RootItemId, ct)) throw Denied();
        return teacher;
    }
    private async Task<StorageDriveRoot> SchoolRootAsync(CancellationToken ct) =>
        await scopes.GetSchoolDriveRootAsync(School, ct) ?? throw new StorageUnavailableException();

    private async Task<StorageFolder> RootAsync(StorageTeacher? teacher, CancellationToken ct)
    {
        var root = await repository.FindRootAsync(School, teacher?.Id, ct);
        if (root is null && teacher is not null)
        {
            root = await repository.FindProviderFolderAsync(School, teacher.Root.RootItemId, ct);
            root ??= await repository.AddFolderAsync(new() { SchoolId = School, OwnerTeacherId = teacher.Id, Kind = StorageFolderKind.Teacher,
                DisplayName = teacher.DisplayName, DriveId = teacher.Root.DriveId, DriveItemId = teacher.Root.RootItemId,
                CreatedByUserId = user.UserId }, ct);
        }
        return root ?? throw new KeyNotFoundException("لم تُهيّأ مكتبة المدرسة بعد.");
    }
    private async Task<StorageFolderAccess> FolderAsync(int? id, StorageTeacher? teacher, CancellationToken ct)
    {
        var folder = id is null ? await RootAsync(teacher, ct) : await repository.FindFolderAsync(School, id.Value, ct)
            ?? throw new KeyNotFoundException("المجلد غير متاح.");
        if (folder.Kind == StorageFolderKind.VisitArchive || folder.OwnerTeacherId != teacher?.Id) throw Denied();
        var school = await SchoolRootAsync(ct);
        var rootFolder = await RootAsync(teacher, ct);
        var root = teacher?.Root ?? new StorageDriveRoot(rootFolder.DriveId, rootFolder.DriveItemId);
        if (folder.DriveId != school.DriveId || root.DriveId != school.DriveId ||
            !await provider.IsWithinAsync(School, school.RootItemId, root.RootItemId, ct) ||
            !await provider.IsWithinAsync(School, root.RootItemId, folder.DriveItemId, ct)) throw Denied();
        var meta = await provider.MetadataAsync(School, folder.DriveItemId, ct);
        if (meta is null || meta.Trashed || !meta.IsFolder) throw new KeyNotFoundException("المجلد غير متاح على Drive.");
        if (teacher is null) await EnsureNoTeacherOverlapAsync(root.RootItemId, ct);
        return new(folder, school, root);
    }
    private async Task EnsureNoTeacherOverlapAsync(string candidate, CancellationToken ct)
    {
        var school = await SchoolRootAsync(ct);
        foreach (var grant in await repository.TeacherRootsAsync(School, ct))
        {
            if (grant.DriveId != school.DriveId || grant.RootItemId == school.RootItemId ||
                !await provider.IsWithinAsync(School, school.RootItemId, grant.RootItemId, ct) ||
                await provider.IsWithinAsync(School, grant.RootItemId, candidate, ct) ||
                await provider.IsWithinAsync(School, candidate, grant.RootItemId, ct))
                throw new UnauthorizedSchoolAccessException("توجد منحة معلم متداخلة مع المكتبة أو خارج جذر المدرسة. يلزم مراجعة المنح قبل التفعيل.");
        }
    }
    public async Task<StorageContextDto> ContextAsync(bool own, CancellationToken ct = default)
    {
        Enabled();
        await authorization.RequireScopeAsync(School, ct);
        var context = await repository.ContextAsync(School, ct);
        if (!own) await authorization.RequireSchoolPermissionAsync(School, PermissionNames.StorageViewSchool, ct);
        try
        {
            var teacher = await ActorAsync(own, false, ct);
            var root = await repository.FindRootAsync(School, teacher?.Id, ct);
            if (root is null && teacher is not null) root = await RootAsync(teacher, ct);
            var school = await SchoolRootAsync(ct);
            var meta = await provider.MetadataAsync(School, school.RootItemId, ct);
            if (meta is null || !meta.IsFolder || meta.Trashed) throw new StorageUnavailableException();
            if (root is not null) await FolderAsync(root.Id, teacher, ct);
            var canManage = own && await scopes.HasPermissionAsync(user.UserId!, School, PermissionNames.StorageManageOwn, ct);
            if (!own)
            {
                try { await authorization.RequireSchoolPermissionAsync(School, PermissionNames.StorageManageSchool, ct); canManage = true; }
                catch (UnauthorizedSchoolAccessException) { canManage = false; }
            }
            return context with { CanManage = canManage, IsTeacher = own, ConnectionState = root is null ? "LibraryNotInitialized" : "Connected", RootFolderId = root?.Id };
        }
        catch (KeyNotFoundException) when (own) { return context with { IsTeacher = true, ConnectionState = "FolderNotAssigned" }; }
        catch (InvalidOperationException) { return context with { IsTeacher = own, ConnectionState = "Unavailable" }; }
        catch (HttpRequestException) { return context with { IsTeacher = own, ConnectionState = "Unavailable" }; }
        catch (System.Security.Cryptography.CryptographicException) { return context with { IsTeacher = own, ConnectionState = "Unavailable" }; }
        catch (StorageUnavailableException) { return context with { IsTeacher = own, ConnectionState = "Unavailable" }; }
    }
    private static void Page(int page, int size)
    {
        if (page < 1 || page > 100000 || size < 1 || size > 100) throw new ArgumentException("حد الصفحة من 1 إلى 100 عنصر.");
    }
    public async Task<StoragePage<StorageFolderDto>> FoldersAsync(bool own, int? parent, int page, int pageSize, CancellationToken ct = default)
    {
        Page(page, pageSize);
        var teacher = await ActorAsync(own, false, ct);
        var access = await FolderAsync(parent, teacher, ct);
        var result = await repository.FoldersAsync(School, teacher?.Id, parent, page, pageSize, ct);
        var visible = new List<StorageFolderDto>();
        foreach (var item in result.Items)
        {
            var folder = await repository.FindFolderAsync(School, item.Id, ct);
            if (folder is null || !await provider.IsWithinAsync(School, access.AuthorizedRoot.RootItemId, folder.DriveItemId, ct)) continue;
            var metadata = await provider.MetadataAsync(School, folder.DriveItemId, ct);
            if (metadata is { IsFolder: true, Trashed: false }) visible.Add(item);
        }
        return result with { Items = visible };
    }
    public async Task<StoragePage<StorageFileListDto>> FilesAsync(bool own, StorageListRequest request, CancellationToken ct = default)
    {
        Page(request.Page, request.PageSize);
        if (request.Sort is not ("name" or "size" or "date") || request.Search?.Length > 200) throw new ArgumentException("معايير البحث أو الفرز غير صالحة.");
        var teacher = await ActorAsync(own, false, ct);
        var folder = await FolderAsync(request.FolderId, teacher, ct);
        request = request with { FolderId = folder.Folder.Id, Search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim() };
        var page = await repository.FilesAsync(School, teacher?.Id, request, ct);
        var items = new List<StorageFileListDto>();
        foreach (var row in page.Items)
        {
            var meta = await provider.MetadataAsync(School, row.DriveItemId, ct);
            if (meta is null || meta.Trashed)
            {
                await SetAvailabilityAsync(row.Dto.StoredFileId, true, ct);
                items.Add(row.Dto with { State = "MissingFromDrive" });
            }
            else if (await provider.IsWithinAsync(School, folder.AuthorizedRoot.RootItemId, meta.Id, ct)) items.Add(row.Dto);
            else await SetAvailabilityAsync(row.Dto.StoredFileId, true, ct);
            // A moved item outside the current root is never disclosed through list results.
        }
        return new(items, page.Total, page.Page, page.PageSize);
    }
    public async Task<StorageFileDetailsDto> DetailsAsync(int id, CancellationToken ct = default)
    {
        Enabled();
        var access = await authorization.RequireFileAsync(School, id, ct: ct, metadataOnly: true);
        var metadata = await provider.MetadataAsync(School, access.DriveItemId, ct);
        if (metadata is null || metadata.Trashed) await SetAvailabilityAsync(id, true, ct);
        else
        {
            await EnsureLiveFileBoundaryAsync(access, ct);
            if (access.Availability == StoredFileAvailability.Missing) await SetAvailabilityAsync(id, false, ct);
            await authorization.RequireFileAsync(School, id, ct: ct);
        }
        return await repository.DetailsAsync(School, id, ct) ?? throw new KeyNotFoundException("الملف غير متاح.");
    }
    public async Task<StorageDiscoveryPageDto> DiscoverAsync(bool own, int folderId, string? token, CancellationToken ct = default)
    {
        if (token?.Length > 2048) throw new ArgumentException("رمز الصفحة غير صالح.");
        var teacher = await ActorAsync(own, false, ct);
        var folder = await FolderAsync(folderId, teacher, ct);
        var page = await provider.ChildrenAsync(School, folder.Folder.DriveItemId, folder.Folder.DriveId, token, ct);
        return await repository.IndexDiscoveredFoldersAsync(folder.Folder, page, user.UserId!, ct);
    }
    public async Task<DriveFileContentDto> ContentAsync(int id, CancellationToken ct = default)
    {
        Enabled();
        await authorization.RequireScopeAsync(School, ct);
        // Identity authorization precedes Drive calls; missing resources fail closed.
        var access = await authorization.RequireFileAsync(School, id, ct: ct, metadataOnly: true);
        var meta = await provider.MetadataAsync(School, access.DriveItemId, ct);
        if (meta is null || meta.Trashed)
        {
            await SetAvailabilityAsync(id, true, ct);
            throw new KeyNotFoundException("الملف مفقود من Drive.");
        }
        await EnsureLiveFileBoundaryAsync(access, ct);
        if (access.Availability == StoredFileAvailability.Missing) await SetAvailabilityAsync(id, false, ct);
        await authorization.RequireFileAsync(School, id, ct: ct);
        var content = await provider.ContentAsync(School, access.DriveItemId, ct);
        var details = await repository.DetailsAsync(School, id, ct) ?? throw new KeyNotFoundException();
        return content with { FileName = details.File.DisplayName, ContentType = details.File.MimeType ?? "application/octet-stream" };
    }
    private async Task EnsureLiveFileBoundaryAsync(StorageFileAccess access, CancellationToken ct)
    {
        var own = access.OwnerIsActiveInSchool && access.OwnerUserId == user.UserId && access.SourceKind == StoredFileSourceKind.TeacherUpload;
        var root = own
            ? await scopes.GetTeacherDriveRootAsync(School, access.OwnerTeacherId!.Value, ct)
            : await SchoolRootAsync(ct);
        if (root is null || !await provider.IsWithinAsync(School, root.RootItemId, access.DriveItemId, ct))
        {
            await SetAvailabilityAsync(access.Id, true, ct);
            throw Denied();
        }
    }
    public async Task<StorageFolderDto> CreateFolderAsync(CreateStorageFolderRequest request, CancellationToken ct = default)
    {
        await ActorAsync(false, true, ct);
        var name = request.ParentFolderId == null ? "مكتبة المدرسة" : ValidatedStorageUpload.SafeName(request.DisplayName);
        var school = await SchoolRootAsync(ct);
        var parent = request.ParentFolderId is null ? null : await FolderAsync(request.ParentFolderId, null, ct);
        if (parent is null && await repository.FindRootAsync(School, null, ct) is { } existingRoot)
        {
            await FolderAsync(existingRoot.Id, null, ct);
            return FolderDto(existingRoot);
        }
        var parentItem = parent?.Folder.DriveItemId ?? school.RootItemId;
        ValidateKey(request.RequestKey);
        // Stable, school-wide identity prevents two managers/delegates provisioning duplicate roots.
        var key = "folder:" + Hash(new { parentItem, name });
        var operation = await repository.FindOperationAsync(School, user.UserId!, key, ct);
        if (operation is null)
        {
            // Before creating, reject grants that encompass the school root.
            if (parent is null)
                foreach (var grant in await repository.TeacherRootsAsync(School, ct))
                    if (await provider.IsWithinAsync(School, grant.RootItemId, school.RootItemId, ct)) throw Denied();
            operation = new() { SchoolId = School, ActorUserId = user.UserId!, RequestKey = key, Action = "CreateFolder",
                Fingerprint = key[7..], DisplayName = name, ProviderItemId = await provider.AllocateIdAsync(School, ct),
                ParentItemId = parentItem, DriveId = school.DriveId, FolderId = parent?.Folder.Id };
            await repository.AddOperationAsync(operation, ct);
            try { await provider.CreateFolderAsync(School, operation.ProviderItemId, parentItem, name, ct); }
            catch { await AttentionAsync(operation.Id); throw new StorageUnavailableException(); }
        }
        return FolderDto(await CompleteFolderAsync(operation, ct));
    }
    private async Task<StorageFolder> CompleteFolderAsync(StorageOperation op, CancellationToken ct)
    {
        var meta = await provider.MetadataAsync(School, op.ProviderItemId, ct);
        if (meta is null || meta.Trashed || !meta.IsFolder || !meta.Parents.Contains(op.ParentItemId)) throw new StorageUnavailableException();
        await EnsureNoTeacherOverlapAsync(meta.Id, ct);
        var folder = await repository.FindProviderFolderAsync(School, meta.Id, ct);
        folder ??= await repository.AddFolderAsync(new() { SchoolId = School, ParentFolderId = op.FolderId,
            Kind = StorageFolderKind.SchoolLibrary, DisplayName = op.DisplayName, DriveId = op.DriveId,
            DriveItemId = meta.Id, CreatedByUserId = op.ActorUserId }, ct);
        op.Status = "Completed"; op.ErrorCode = null;
        await repository.SaveOperationAsync(op, ct);
        return folder;
    }
    public async Task<StorageFolderDto> MoveFolderAsync(int id, MoveStorageFolderRequest request, CancellationToken ct = default)
    {
        await ActorAsync(false, true, ct);
        return await scopes.InSerializableTransactionAsync(async () =>
        {
            var source = await FolderAsync(id, null, ct);
            var target = await FolderAsync(request.ParentFolderId, null, ct);
            if (source.Folder.ParentFolderId is null || id == target.Folder.Id ||
                await provider.IsWithinAsync(School, source.Folder.DriveItemId, target.Folder.DriveItemId, ct))
                throw new ArgumentException("لا يمكن نقل الجذر أو إنشاء دورة في شجرة المجلدات.");
            if (await repository.HasProtectedDescendantsAsync(School, id, ct)) throw Protected();
            var version = Expected(request.RowVersion, source.Folder.RowVersion);
            var old = await repository.FindFolderAsync(School, source.Folder.ParentFolderId.Value, ct) ?? throw Denied();
            await provider.MoveAsync(School, source.Folder.DriveItemId, old.DriveItemId, target.Folder.DriveItemId, ct);
            source.Folder.ParentFolderId = target.Folder.Id; source.Folder.UpdatedByUserId = user.UserId;
            await repository.SaveFolderAsync(source.Folder, version, ct);
            return FolderDto(source.Folder);
        }, ct);
    }
    public async Task<StorageUploadDto> UploadAsync(StorageUploadRequest request, CancellationToken ct = default)
    {
        var teacher = await ActorAsync(request.Own, true, ct);
        var folder = await FolderAsync(request.ParentFolderId, teacher, ct);
        ValidateKey(request.RequestKey);
        await using var upload = await ValidatedStorageUpload.ReadAsync(request.Content, request.FileName, request.Length, ct);
        var fingerprint = Hash(new { upload.FileName, upload.Size, upload.SHA256, folder.Folder.Id, request.LegacyTaskId, request.Own });
        var operation = await repository.FindOperationAsync(School, user.UserId!, request.RequestKey, ct);
        if (operation is not null)
        {
            if (operation.Fingerprint != fingerprint || operation.Action != "Upload") throw new StorageConflictException();
            return await ReconcileAsync(operation.Id, ct);
        }
        operation = new() { SchoolId = School, ActorUserId = user.UserId!, RequestKey = request.RequestKey, Fingerprint = fingerprint,
            FolderId = folder.Folder.Id, OwnerTeacherId = teacher?.Id, DisplayName = upload.FileName, Size = upload.Size,
            MimeType = upload.MimeType, SHA256 = upload.SHA256, DriveId = folder.Folder.DriveId,
            ParentItemId = folder.Folder.DriveItemId, ProviderItemId = await provider.AllocateIdAsync(School, ct), LegacyTaskId = request.LegacyTaskId };
        await scopes.InSerializableTransactionAsync(async () =>
        {
            if (request.LegacyTaskId is not null)
            {
                if (teacher is null) throw Denied();
                var reservation = await submissions.ReserveUploadAsync(teacher.Id, School, request.LegacyTaskId.Value, request.RequestKey, ct);
                if (reservation.ExistingResult is not null) throw new StorageConflictException();
                operation.LegacyOperationId = reservation.OperationId;
            }
            await repository.AddOperationAsync(operation, ct);
            return true;
        }, ct); // Both reservations commit before sending the stream.
        try
        {
            var created = await provider.UploadAsync(School, operation.ProviderItemId, operation.ParentItemId, upload.FileName, upload.MimeType, upload.Content, ct);
            if (created.Id != operation.ProviderItemId || created.Size != operation.Size || created.MimeType != operation.MimeType ||
                !created.Parents.Contains(operation.ParentItemId) || created.Trashed) throw new StorageUnavailableException();
            return await CompleteUploadAsync(operation, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await AttentionAsync(operation.Id);
            throw;
        }
        catch (StorageProviderRejectedException rejection)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                operation.Status = "Failed"; operation.ErrorCode = "ProviderRejected";
                await repository.SaveOperationAsync(operation, timeout.Token);
                if (operation.LegacyOperationId is not null) await submissions.MarkUploadFailedAsync(operation.LegacyOperationId.Value, "ProviderRejected", timeout.Token);
            }
            catch { /* The Pending reservation remains safe if SQL is also unavailable. */ }
            throw new StorageUnavailableException(rejection.Message);
        }
        catch
        {
            // A lost HTTP response is ambiguous. Never automatically create another item.
            // If SQL is unavailable even here, the committed Pending operation still survives.
            await AttentionAsync(operation.Id);
            throw new StorageUnavailableException();
        }
    }
    private async Task AttentionAsync(int id)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            var persisted = await repository.GetOperationAsync(School, id, timeout.Token);
            if (persisted is null || persisted.Status == "Completed") return;
            persisted.Status = "NeedsAttention"; persisted.ErrorCode = "ProviderOrPersistenceUnavailable";
            await repository.SaveOperationAsync(persisted, timeout.Token);
        }
        catch { /* The original Pending row is durable; do not mask cancellation/SQL failure. */ }
    }
    private Task<bool> SetAvailabilityAsync(int id, bool missing, CancellationToken ct) =>
        scopes.InSerializableTransactionAsync(async () =>
        {
            var target = await repository.MutationTargetAsync(School, id, ct);
            await repository.SetAvailabilityAsync(School, id, missing, ct);
            if (target?.File.LegacySubmissionId is not null)
                await submissions.MarkAvailabilityAsync(target.File.OwnerTeacherId!.Value, target.File.LegacySubmissionId.Value, missing, ct);
            return true;
        }, ct);
    public async Task<StorageUploadDto> ReconcileAsync(int id, CancellationToken ct = default)
    {
        Enabled();
        await authorization.RequireScopeAsync(School, ct);
        var op = await repository.GetOperationAsync(School, id, ct) ?? throw new KeyNotFoundException();
        var teacher = await ActorAsync(op.OwnerTeacherId != null, true, ct);
        if (op.ActorUserId != user.UserId || op.OwnerTeacherId != teacher?.Id) throw Denied();
        await FolderAsync(op.FolderId, teacher, ct);
        if (op.Status == "Failed") return UploadDto(op);
        if (op.Status == "Completed")
        {
            if (op.StoredFileId is not null) await authorization.RequireFileAsync(School, op.StoredFileId.Value, ct: ct);
            return UploadDto(op);
        }
        var item = await provider.MetadataAsync(School, op.ProviderItemId, ct);
        if (item is null || item.Trashed)
        {
            op.Status = "NeedsAttention"; op.ErrorCode = "ProviderResultUnknown";
            await repository.SaveOperationAsync(op, ct);
            return UploadDto(op);
        }
        if (!item.Parents.Contains(op.ParentItemId) || item.Name != op.DisplayName ||
            (op.Action == "Upload" && (item.Size != op.Size || item.MimeType != op.MimeType))) throw Denied();
        if (op.Action == "CreateFolder") { await CompleteFolderAsync(op, ct); return UploadDto(op); }
        return await CompleteUploadAsync(op, ct);
    }
    private async Task<StorageUploadDto> CompleteUploadAsync(StorageOperation op, CancellationToken ct)
    {
        if (op.LegacyOperationId is not null)
        {
            var result = await submissions.RecordCompletedUploadAsync(op.LegacyOperationId.Value, op.OwnerTeacherId!.Value, School,
                op.DriveId, op.ParentItemId, LegacyItem(op), ct);
            op.LegacySubmissionId = result.SubmissionId;
            await repository.SaveOperationAsync(op, ct);
        }
        await repository.CompleteUploadAsync(op, ct);
        return UploadDto(op);
    }
    public Task RenameAsync(int id, RenameStorageFileRequest request, CancellationToken ct = default) => MutateAsync(id, request.DisplayName, request.RowVersion, false, ct);
    public async Task DeleteAsync(int id, DeleteStorageFileRequest request, CancellationToken ct = default)
    {
        Enabled();
        await authorization.RequireScopeAsync(School, ct);
        var existing = await scopes.GetFileAccessAsync(School, id, ct);
        if (existing?.IsDeleted == true)
        {
            if (existing.OwnerIsActiveInSchool && existing.OwnerUserId == user.UserId)
            {
                await ActorAsync(true, true, ct);
            }
            else await ActorAsync(false, true, ct);
            return;
        }
        await MutateAsync(id, null, request.RowVersion, true, ct);
    }
    private async Task MutateAsync(int id, string? name, string rowVersion, bool delete, CancellationToken ct)
    {
        Enabled();
        await authorization.RequireFileAsync(School, id, true, ct);
        await scopes.InSerializableTransactionAsync(async () =>
        {
            var target = await repository.MutationTargetAsync(School, id, ct) ?? throw new KeyNotFoundException();
            if (target.Protected) throw Protected();
            var version = Expected(rowVersion, target.File.RowVersion);
            if (delete)
            {
                await provider.TrashAsync(School, target.Version.DriveItemId, target.Version.DriveId, ct);
                target.File.IsDeleted = true; target.File.DeletedAtUtc = DateTimeOffset.UtcNow; target.File.DeletedByUserId = user.UserId;
                target.Version.Availability = StoredFileAvailability.Deleted;
            }
            else
            {
                name = ValidatedStorageUpload.SafeName(name!);
                if (!string.Equals(Path.GetExtension(name), Path.GetExtension(target.File.DisplayName), StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("لا يمكن تغيير امتداد الملف.");
                await provider.RenameAsync(School, target.Version.DriveItemId, name, ct);
                // Version identity/DriveFileName is immutable; only the asset's display label changes.
                target.File.DisplayName = name;
            }
            await repository.SaveMutationAsync(target, version, user.UserId!, delete, ct);
            if (target.File.LegacySubmissionId is not null)
            {
                if (delete) await submissions.MarkDeletedAsync(target.File.OwnerTeacherId!.Value, target.File.LegacySubmissionId.Value, user.UserId, ct);
                else await submissions.MarkRenamedAsync(target.File.OwnerTeacherId!.Value, target.File.LegacySubmissionId.Value,
                    new(target.Version.DriveItemId, target.File.DisplayName, false, null, target.Version.FileExtension, target.Version.MimeType,
                        target.Version.SizeInBytes, DateTimeOffset.UtcNow, null, null, null, null), ct);
            }
            return true;
        }, ct);
    }
    public async Task<UploadFileResultDto> LegacyUploadAsync(UploadFileRequest request, CancellationToken ct = default)
    {
        var teacher = await ActorAsync(true, true, ct);
        var root = await RootAsync(teacher, ct);
        var destination = root;
        if (!string.IsNullOrWhiteSpace(request.ParentItemId) && request.ParentItemId != root.DriveItemId)
        {
            if (!await provider.IsWithinAsync(School, teacher!.Root.RootItemId, request.ParentItemId, ct)) throw Denied();
            destination = await repository.FindProviderFolderAsync(School, request.ParentItemId, ct)
                ?? await IndexTeacherFolderAsync(request.ParentItemId, teacher, root.Id, ct);
        }
        var result = await UploadAsync(new(request.Content, request.FileName, request.Length, destination.Id, request.RequestId, true, request.TaskId), ct);
        if (result.Status != "Completed") throw new StorageUnavailableException();
        var op = await repository.GetOperationAsync(School, result.OperationId, ct) ?? throw new KeyNotFoundException();
        return new(op.LegacySubmissionId!.Value, LegacyItem(op) with { SubmissionId = op.LegacySubmissionId, SubmissionStatus = nameof(EvidenceReviewStatus.PendingReview) });
    }
    private async Task<StorageFolder> IndexTeacherFolderAsync(string id, StorageTeacher teacher, int parentId, CancellationToken ct)
    {
        var meta = await provider.MetadataAsync(School, id, ct) ?? throw Denied();
        if (!meta.IsFolder || meta.Trashed) throw Denied();
        return await repository.AddFolderAsync(new() { SchoolId = School, OwnerTeacherId = teacher.Id, Kind = StorageFolderKind.Teacher,
            DisplayName = meta.Name, DriveId = teacher.Root.DriveId, DriveItemId = id, ParentFolderId = parentId, CreatedByUserId = user.UserId }, ct);
    }
    public async Task<DriveItemDto> LegacyRenameAsync(long submissionId, string name, CancellationToken ct = default)
    {
        var teacher = await ActorAsync(true, true, ct);
        var id = await repository.FindLegacyFileAsync(School, submissionId, ct) ?? throw new KeyNotFoundException("يلزم فهرسة الملف قبل التعديل.");
        var access = await scopes.GetFileAccessAsync(School, id, ct) ?? throw Denied();
        if (access.OwnerTeacherId != teacher!.Id) throw Denied();
        var details = await DetailsAsync(id, ct);
        await RenameAsync(id, new(name, details.File.RowVersion), ct);
        return new(access.DriveItemId, name, false, null, Path.GetExtension(name), details.File.MimeType,
            details.File.Size, DateTimeOffset.UtcNow, null, null, null, null, submissionId);
    }
    public async Task LegacyDeleteAsync(long submissionId, CancellationToken ct = default)
    {
        var teacher = await ActorAsync(true, true, ct);
        var id = await repository.FindLegacyFileAsync(School, submissionId, ct) ?? throw new KeyNotFoundException("يلزم فهرسة الملف قبل الحذف.");
        var access = await scopes.GetFileAccessAsync(School, id, ct) ?? throw Denied();
        if (access.OwnerTeacherId != teacher!.Id) throw Denied();
        if (access.IsDeleted) return;
        var details = await DetailsAsync(id, ct);
        await DeleteAsync(id, new(details.File.RowVersion), ct);
    }
    private static StorageFolderDto FolderDto(StorageFolder f) => new(f.Id, f.ParentFolderId, f.DisplayName, f.Kind.ToString(), Convert.ToBase64String(f.RowVersion));
    private static StorageUploadDto UploadDto(StorageOperation o) => new(o.Id, o.StoredFileId, o.VersionId, o.Status, o.DisplayName, o.Size, o.MimeType, o.CreatedAtUtc, o.ErrorCode);
    private static DriveItemDto LegacyItem(StorageOperation o) => new(o.ProviderItemId, o.DisplayName, false, null, Path.GetExtension(o.DisplayName), o.MimeType, o.Size, o.CreatedAtUtc, null, null, null, null);
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 128 || key.Any(c => c < 33 || c > 126)) throw new ArgumentException("معرّف طلب الرفع مطلوب ويجب ألا يتجاوز 128 حرفًا.");
    }
    private static byte[] Expected(string encoded, byte[] actual)
    {
        byte[] value;
        try { value = Convert.FromBase64String(encoded); }
        catch (FormatException) { throw new ArgumentException("رقم نسخة البيانات غير صالح."); }
        if (!value.SequenceEqual(actual)) throw new StorageConflictException();
        return value;
    }
    private static UnauthorizedSchoolAccessException Denied() => new("المجلد أو الملف خارج نطاق الوصول المصرّح.");
    private static BusinessRuleException Protected() => new("الملف معتمد أو تاريخي ومحمي. يلزم طلب تغيير بدل التعديل أو الحذف المباشر.");
}
