using AlFalah.Application.Common;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.Storage;

public sealed class StorageAuthorizationService(
    IStorageRepository repository, ICurrentUserService currentUser, IStorageDriveBoundary driveBoundary,
    TimeProvider time) : IStorageAuthorizationService
{
    private static readonly HashSet<string> OperationalPermissions =
    [PermissionNames.StorageViewSchool, PermissionNames.StorageManageSchool, PermissionNames.StorageReviewEvidence,
     PermissionNames.StorageViewArchive, PermissionNames.StorageRetryArchive];

    public async Task<StorageActorScope> RequireScopeAsync(int schoolId, CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
            throw new UnauthorizedAccessException("يلزم تسجيل الدخول.");
        if (currentUser.ActiveSchoolId != schoolId)
            throw Denied();
        var scope = await repository.GetActorScopeAsync(currentUser.UserId, schoolId, ct);
        if (scope is null || !scope.IsMember) throw Denied();
        return scope;
    }

    public async Task<StorageActorScope> RequireManagerAsync(int schoolId, CancellationToken ct = default)
    {
        var scope = await RequireScopeAsync(schoolId, ct);
        if (scope.ManagerUserId != scope.UserId ||
            !await repository.HasPermissionAsync(scope.UserId, schoolId, PermissionNames.StorageDelegate, ct))
            throw Denied();
        return scope;
    }

    public async Task RequireSchoolPermissionAsync(int schoolId, string permission, CancellationToken ct = default)
    {
        var scope = await RequireScopeAsync(schoolId, ct);
        if (permission == PermissionNames.StorageDelegate)
        {
            await RequireManagerAsync(schoolId, ct);
            return;
        }
        if (!OperationalPermissions.Contains(permission)) throw Denied();
        if (scope.ManagerUserId == scope.UserId && await repository.HasPermissionAsync(scope.UserId, schoolId, permission, ct)) return;
        if (await repository.HasDelegationAsync(scope.UserId, schoolId, time.GetUtcNow(), ct)) return;
        throw Denied();
    }

    public async Task<StorageFileAccess> RequireFileAsync(int schoolId, int fileId, bool mutation = false, CancellationToken ct = default)
    {
        var scope = await RequireScopeAsync(schoolId, ct);
        var file = await repository.GetFileAccessAsync(schoolId, fileId, ct) ?? throw new KeyNotFoundException("الملف غير متاح.");
        if (file.IsDeleted || file.Availability is StoredFileAvailability.Deleted or StoredFileAvailability.Missing or StoredFileAvailability.UploadIncomplete)
            throw new KeyNotFoundException("الملف غير متاح.");
        var own = file.OwnerIsActiveInSchool && file.OwnerUserId == scope.UserId && file.SourceKind == StoredFileSourceKind.TeacherUpload;
        StorageDriveRoot? root;
        if (own)
        {
            var permission = mutation ? PermissionNames.StorageManageOwn : PermissionNames.StorageViewOwn;
            if (!await repository.HasPermissionAsync(scope.UserId, schoolId, permission, ct)) throw Denied();
            root = await repository.GetTeacherDriveRootAsync(schoolId, file.OwnerTeacherId!.Value, ct);
        }
        else
        {
            // Visit report visibility needs the independent visit policy added in S5.
            if (file.SourceKind == StoredFileSourceKind.VisitArchive) throw Denied();
            await RequireSchoolPermissionAsync(schoolId, mutation ? PermissionNames.StorageManageSchool : PermissionNames.StorageViewSchool, ct);
            root = await repository.GetSchoolDriveRootAsync(schoolId, ct);
        }
        if (root is null || root.DriveId != file.DriveId) throw Denied();
        if (own)
        {
            // A teacher grant may itself have been moved outside the school in Drive.
            var schoolRoot = await repository.GetSchoolDriveRootAsync(schoolId, ct);
            if (schoolRoot is null || schoolRoot.DriveId != file.DriveId) throw Denied();
            await driveBoundary.EnsureWithinAsync(schoolId, schoolRoot, root.RootItemId, ct);
        }
        await driveBoundary.EnsureWithinAsync(schoolId, root, file.DriveItemId, ct);
        return file;
    }

    private static UnauthorizedSchoolAccessException Denied() => new("لا تملك صلاحية الوصول إلى تخزين هذه المدرسة.");
}
