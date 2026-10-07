using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "SchoolFileStorage";
    public bool AdministrationEnabled { get; set; } = false;
    // Activate only after schema/backfill/live Drive gates; defaults deliberately remain OFF.
    public bool ReadModelEnabled { get; set; } = false;
    public bool ArchiveWorkerEnabled { get; set; } = false;
    public bool ArchiveExternalWritesEnabled { get; set; } = false;
}

public sealed record GrantStorageDelegationRequest(string GranteeUserId, DateTimeOffset StartsAt, DateTimeOffset? ExpiresAt, string Reason);
public sealed record RevokeStorageDelegationRequest(string Reason, string RowVersion);
public sealed record StorageDelegationDto(int Id, string GranteeUserId, string GrantedByManagerUserId,
    DateTimeOffset StartsAt, DateTimeOffset? ExpiresAt, DateTimeOffset? RevokedAt, string Reason,
    string? RevocationReason, string? RevokedByManagerUserId, string RowVersion);
public sealed record StorageActorScope(string UserId, int SchoolId, string? ManagerUserId, bool IsMember);
// Internal authorization result; raw provider IDs never form an API response.
public sealed record StorageFileAccess(int Id, int SchoolId, int? OwnerTeacherId, string? OwnerUserId,
    bool OwnerIsActiveInSchool, string DriveId, string DriveItemId, StoredFileSourceKind SourceKind,
    StoredFileAvailability Availability, bool IsDeleted, int FolderId = 0);
public sealed record StorageDriveRoot(string DriveId, string RootItemId);

public interface IStorageRepository
{
    Task<StorageActorScope?> GetActorScopeAsync(string userId, int schoolId, CancellationToken ct);
    Task<bool> HasPermissionAsync(string userId, int schoolId, string permission, CancellationToken ct);
    Task<IReadOnlyList<string>> GrantedPermissionsAsync(string userId, int schoolId, IReadOnlyList<string> permissions, CancellationToken ct);
    Task<bool> HasDelegationAsync(string userId, int schoolId, DateTimeOffset now, CancellationToken ct);
    Task<bool> HasOverlappingDelegationAsync(int schoolId, string userId, DateTimeOffset start, DateTimeOffset? end, CancellationToken ct);
    Task<IReadOnlyList<StorageDelegation>> GetDelegationsAsync(int schoolId, CancellationToken ct);
    Task<StorageDelegation?> GetDelegationAsync(int schoolId, int id, CancellationToken ct);
    Task AddDelegationAsync(StorageDelegation delegation, CancellationToken ct);
    Task RevokeDelegationAsync(StorageDelegation delegation, byte[] expectedVersion, CancellationToken ct);
    Task<T> InSerializableTransactionAsync<T>(Func<Task<T>> operation, CancellationToken ct);
    Task<StorageFileAccess?> GetFileAccessAsync(int schoolId, int id, CancellationToken ct);
    Task<StorageDriveRoot?> GetSchoolDriveRootAsync(int schoolId, CancellationToken ct);
    Task<StorageDriveRoot?> GetTeacherDriveRootAsync(int schoolId, int teacherId, CancellationToken ct);
}

public interface IStorageDriveBoundary
{
    Task EnsureWithinAsync(int schoolId, StorageDriveRoot root, string itemId, CancellationToken ct);
}

public interface IStorageAuthorizationService
{
    Task<StorageAccessDto> AccessAsync(int schoolId, bool own, bool allowManagerWithoutView, CancellationToken ct = default);
    Task<StorageActorScope> RequireScopeAsync(int schoolId, CancellationToken ct = default);
    Task<StorageActorScope> RequireManagerAsync(int schoolId, CancellationToken ct = default);
    Task RequireSchoolPermissionAsync(int schoolId, string permission, CancellationToken ct = default);
    Task<StorageFileAccess> RequireFileAsync(int schoolId, int fileId, bool mutation = false, CancellationToken ct = default, bool metadataOnly = false);
}

public interface IStorageDelegationService
{
    Task<IReadOnlyList<StorageDelegationDto>> ListAsync(CancellationToken ct = default);
    Task<StorageDelegationDto> GrantAsync(GrantStorageDelegationRequest request, CancellationToken ct = default);
    Task<StorageDelegationDto> RevokeAsync(int id, RevokeStorageDelegationRequest request, CancellationToken ct = default);
}

public sealed class StorageConflictException : Exception
{
    public StorageConflictException() : base("تعارض الطلب مع تعديل آخر أو تفويض قائم. حدّث البيانات ثم أعد المحاولة.") { }
}
