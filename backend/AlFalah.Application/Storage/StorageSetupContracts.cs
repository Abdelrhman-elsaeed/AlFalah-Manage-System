using AlFalah.Domain.Entities.Storage;

namespace AlFalah.Application.Storage;

public sealed record SetupFolderItem(string Key, string Name, string ParentKey, string State, string? Note = null);
public sealed record SetupTeacherItem(int TeacherId, string Name, string State, string? Note = null);
public sealed record SetupRecoveryIssue(string Key, string Name, int FolderCount, int FileCount, int OperationCount,
    string Warning);
public sealed record StorageSetupPlan(IReadOnlyList<SetupFolderItem> Folders, IReadOnlyList<SetupTeacherItem> Teachers,
    bool ArchiveEnabled, bool ArchiveCapabilityReady, IReadOnlyList<SetupRecoveryIssue>? RecoveryIssues = null);
public sealed record StorageSetupResult(StorageSetupPlan Plan, int Created, int Linked, int AlreadyPresent,
    IReadOnlyList<string> Warnings);
public sealed record StorageSetupProgress(string Stage, string Label, int Completed, int Total);
public sealed record VisitArchiveActivation(bool Enabled);

public interface IStorageSetupService
{
    Task<StorageSetupPlan> PlanAsync(CancellationToken ct = default);
    Task<StorageSetupResult> ApplyAsync(bool folders, bool teachers, CancellationToken ct = default,
        IReadOnlyList<string>? recoveryKeys = null);
    Task<StorageSetupResult> ApplyWithProgressAsync(bool folders, bool teachers,
        Func<StorageSetupProgress, Task> progress, CancellationToken ct = default,
        IReadOnlyList<string>? recoveryKeys = null);
    Task<VisitArchiveActivation> ArchiveStatusAsync(CancellationToken ct = default);
    Task<VisitArchiveActivation> SetArchiveEnabledAsync(bool enabled, CancellationToken ct = default);
}

public sealed record SetupTeacher(int Id, string Name, string? FolderItemId);
public sealed record SetupRecoveryImpact(int FolderCount, int FileCount, int OperationCount);

public interface IStorageSetupRepository
{
    Task<IReadOnlyList<SetupTeacher>> TeachersAsync(int schoolId, CancellationToken ct);
    Task<StorageFolder?> TrackedFolderAsync(int schoolId, string itemId, CancellationToken ct);
    Task<StorageFolder?> TrackedRootAsync(int schoolId, Domain.Enums.StorageFolderKind kind, CancellationToken ct);
    Task<StorageFolder?> TrackedChildAsync(int schoolId, int parentId, string name, CancellationToken ct);
    Task<StorageFolder?> TrackedArchiveTeacherAsync(int schoolId, int parentId, int teacherId, CancellationToken ct);
    Task<StorageFolder?> TrackedTeacherRootAsync(int schoolId, int teacherId, CancellationToken ct);
    Task<bool> HasProtectedDataAsync(int schoolId, int folderId, CancellationToken ct);
    Task<SetupRecoveryImpact> RecoveryImpactAsync(int schoolId, int folderId, CancellationToken ct);
    Task<bool> HasTeacherEvidenceAsync(int schoolId, int teacherId, CancellationToken ct);
    Task DeactivateSubtreeAsync(int schoolId, int folderId, string actor, CancellationToken ct);
    Task RebindFolderAsync(StorageFolder folder, string driveId, string itemId, string name, string actor, CancellationToken ct);
    Task TrackAsync(StorageFolder folder, CancellationToken ct);
    Task<StorageOperation?> ReservationAsync(int schoolId, string key, CancellationToken ct);
    Task<IReadOnlyList<StorageOperation>> ReservationsForFolderAsync(int schoolId, string parentItemId, string name, CancellationToken ct);
    Task ReserveAsync(StorageOperation operation, CancellationToken ct);
    Task SaveReservationAsync(StorageOperation operation, CancellationToken ct);
    Task<bool> ArchiveEnabledAsync(int schoolId, CancellationToken ct);
    Task SetArchiveEnabledAsync(int schoolId, bool enabled, string actor, CancellationToken ct);
    Task<bool> ExclusiveAsync(int schoolId, Func<CancellationToken, Task> action, CancellationToken ct);
}
