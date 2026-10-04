using AlFalah.Application.Common;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;
using Microsoft.Extensions.Options;

namespace AlFalah.Application.Storage;

public sealed class EvidenceWorkflowContext(IEvidenceRepository repository, IStorageRepository scopes,
    IStorageAuthorizationService authorization, ICurrentUserService user, IStorageProvider provider, IOptions<StorageOptions> options)
{
    public int School => user.ActiveSchoolId ?? throw new UnauthorizedSchoolAccessException("اختر المدرسة أولًا.");
    public string Actor => user.UserId ?? throw new UnauthorizedAccessException();
    public void Enabled()
    {
        if (!options.Value.ReadModelEnabled) throw new KeyNotFoundException("مساحة الملفات غير مفعّلة.");
        provider.ResetRequestCache();
    }
    public async Task ReadAsync(CancellationToken ct)
    {
        Enabled();
        await authorization.RequireScopeAsync(School, ct);
        try { await authorization.RequireSchoolPermissionAsync(School, PermissionNames.StorageViewSchool, ct); }
        catch (UnauthorizedSchoolAccessException)
        {
            if (!await scopes.HasPermissionAsync(Actor, School, PermissionNames.StorageViewOwn, ct)) throw;
        }
    }
    public Task ManageAsync(CancellationToken ct) => SchoolPermissionAsync(PermissionNames.StorageManageSchool, ct);
    public Task ReviewAsync(CancellationToken ct) => SchoolPermissionAsync(PermissionNames.StorageReviewEvidence, ct);
    public async Task SchoolPermissionAsync(string permission, CancellationToken ct)
    {
        Enabled(); await authorization.RequireSchoolPermissionAsync(School, permission, ct);
    }
    public async Task YearAsync(int year, CancellationToken ct)
    {
        if (!await repository.YearExistsAsync(year, ct)) throw new KeyNotFoundException("السنة الدراسية غير موجودة.");
    }
    public async Task<StoredFile> FileAsync(int id, bool mutation, bool live, CancellationToken ct, bool history = false, bool ownOnly = false)
    {
        Enabled(); await authorization.RequireScopeAsync(School, ct);
        var access = await scopes.GetFileAccessAsync(School, id, ct) ?? throw new KeyNotFoundException("الملف غير متاح.");
        if (access.SourceKind is StoredFileSourceKind.VisitArchive or StoredFileSourceKind.HistoricalImport)
            throw new UnauthorizedSchoolAccessException("هذا الأصل محمي بسياسة الأرشيف.");
        var own = access.OwnerIsActiveInSchool && access.OwnerUserId == Actor;
        if (ownOnly && !own) throw new UnauthorizedSchoolAccessException("الملف ليس ملكك.");
        if (own)
        {
            if (!await scopes.HasPermissionAsync(Actor, School, mutation ? PermissionNames.StorageManageOwn : PermissionNames.StorageViewOwn, ct))
                throw new UnauthorizedSchoolAccessException("لا تملك صلاحية الملف.");
        }
        else await authorization.RequireSchoolPermissionAsync(School, mutation ? PermissionNames.StorageManageSchool : PermissionNames.StorageViewSchool, ct);
        var schoolRoot = await scopes.GetSchoolDriveRootAsync(School, ct) ?? throw new StorageUnavailableException();
        var root = access.OwnerTeacherId != null ? await scopes.GetTeacherDriveRootAsync(School, access.OwnerTeacherId.Value, ct) : schoolRoot;
        if (root is null || root.DriveId != schoolRoot.DriveId || access.DriveId != root.DriveId ||
            !await provider.IsWithinAsync(School, schoolRoot.RootItemId, root.RootItemId, ct))
            throw new UnauthorizedSchoolAccessException("منحة الملف غير متاحة.");
        var file = await repository.FileAsync(School, id, ct) ?? throw new KeyNotFoundException();
        if (file.IsDeleted && !history) throw new KeyNotFoundException("الملف محذوف.");
        if (live)
        {
            var v = await repository.VersionAsync(School, id, file.CurrentVersionId ?? 0, ct) ?? throw new KeyNotFoundException();
            await RequireAvailableAsync(file, v, root, ct);
        }
        return file;
    }
    public async Task RequireAvailableAsync(StoredFile file, StoredFileVersion version, StorageDriveRoot root, CancellationToken ct)
    {
        var meta = await provider.MetadataAsync(School, version.DriveItemId, ct);
        var available = version.DriveId == root.DriveId && meta is { Trashed: false, IsFolder: false } &&
            await provider.IsWithinAsync(School, root.RootItemId, version.DriveItemId, ct);
        var availability = available ? StoredFileAvailability.Available : StoredFileAvailability.Missing;
        if (version.Availability != availability)
        {
            version.Availability = availability;
            version.MissingFromDriveAtUtc = available ? null : DateTimeOffset.UtcNow;
            await repository.SaveAsync(Actor, School, "Storage.VersionAvailability", version.Id.ToString(), null, null, ct);
        }
        if (!available) throw new KeyNotFoundException("النسخة غير متاحة داخل النطاق المصرّح.");
    }
    public static byte[] Expected(string encoded, byte[] actual)
    {
        byte[] value;
        try { value = Convert.FromBase64String(encoded); }
        catch (FormatException) { throw new ArgumentException("رقم نسخة البيانات غير صالح."); }
        if (value.Length == 0 || !value.SequenceEqual(actual)) throw new StorageConflictException();
        return value;
    }
    public static string Reason(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 1000) throw new ArgumentException("السبب مطلوب وبحد أقصى 1000 حرف.");
        return value.Trim();
    }
}
