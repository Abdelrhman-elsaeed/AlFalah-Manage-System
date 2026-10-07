using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.DTOs.Visits;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.Storage;

public sealed record VisitApprovalSnapshot(int SchemaVersion, VisitV2DetailDto Report, VisitV2PdfAssetSources Assets);
public sealed record VisitArchiveQuery(DateTimeOffset? From = null, DateTimeOffset? To = null,
    string? TeacherId = null, string? Status = null, int Page = 1, int PageSize = 25);
public sealed record RetryVisitArchiveRequest(int ApprovalRevision, bool RecreateMissing = false, string? Reason = null);
public sealed record VisitArchiveVersionDto(int VersionId, int VersionNumber, DateTimeOffset UploadedAtUtc, string Availability, long Size);
public sealed record VisitArchiveTeacherDto(string UserId, string Name);
public sealed record VisitArchiveRevisionDto(int ApprovalRevision, string Status, string ApprovalSource,
    DateTimeOffset ApprovedAtUtc, int Attempts, DateTimeOffset? LastAttemptAtUtc, DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? NextAttemptAtUtc, string? ErrorCode, bool IsCurrent, bool CanRetry,
    IReadOnlyList<VisitArchiveVersionDto> Versions);
public sealed record VisitArchiveDto(int VisitId, string InstructorName, int ApprovalRevision,
    bool IsApproved, bool CanManage, IReadOnlyList<VisitArchiveRevisionDto> Revisions);
public sealed record VisitArchiveOperationsDto(bool WorkerEnabled, bool ExternalWritesEnabled, bool Ready);
// Internal repository projections. Provider IDs and snapshots never cross HTTP.
public sealed record ArchiveVisitScope
{
    public int VisitId { get; init; }
    public int SchoolId { get; init; }
    public string InstructorId { get; init; } = "";
    public string InstructorName { get; init; } = "";
    public string CreatorId { get; init; } = "";
    public VisitStatus Status { get; init; }
    public int ApprovalRevision { get; init; }
}
public sealed record ArchiveArtifactRow
{
    public required VisitArchiveArtifact Artifact { get; init; }
    public required StoredFileVersion Version { get; init; }
}
public sealed record ArchiveReadBatch(IReadOnlyList<VisitArchiveOperation> Operations, IReadOnlyList<ArchiveArtifactRow> Artifacts, IReadOnlyList<StoredFileVersion> Versions);
public interface IVisitArchiveCaptureService
{
    Task<VisitV2PdfAssetSources> PrepareAsync(int visitId, string approverId, CancellationToken ct);
    Task CaptureAsync(VisitV2DetailDto report, VisitV2PdfAssetSources assets, int revision, string source, CancellationToken ct);
    Task MarkHistoricalAsync(int visitId, CancellationToken ct);
    Task<VisitApprovalSnapshot?> SnapshotAsync(int schoolId, int visitId, int revision, CancellationToken ct);
}
public interface IVisitArchiveAssetFreezer
{
    Task<VisitV2PdfAssetSources> FreezeAsync(VisitV2PdfAssetSources sources, CancellationToken ct);
}
public interface IVisitArchiveRepository
{
    Task<VisitV2PdfAssetSources> ApprovalAssetsAsync(int visitId, string approverId, CancellationToken ct);
    Task StageAsync(VisitArchiveOperation operation, CancellationToken ct);
    Task MarkHistoricalAsync(int visitId, CancellationToken ct);
    Task<ArchiveVisitScope?> VisitAsync(int schoolId, int visitId, CancellationToken ct);
    Task<IReadOnlyList<VisitArchiveOperation>> OperationsAsync(int schoolId, int visitId, CancellationToken ct);
    Task<string?> SnapshotJsonAsync(int schoolId, int visitId, int revision, CancellationToken ct);
    Task<ArchiveReadBatch> ReadBatchAsync(int schoolId, IReadOnlyList<int> visitIds, CancellationToken ct);
    Task<StoragePage<ArchiveVisitScope>> ListAsync(int schoolId, VisitArchiveQuery query, CancellationToken ct);
    Task<IReadOnlyList<ArchiveArtifactRow>> ArtifactsAsync(int schoolId, int visitId, CancellationToken ct);
    Task<IReadOnlyList<VisitArchiveVersionDto>> VersionsAsync(int schoolId, int fileId, CancellationToken ct);
    Task<StoredFileVersion?> VersionAsync(int schoolId, int fileId, int versionId, CancellationToken ct);
    Task<IReadOnlyList<VisitArchiveTeacherDto>> TeachersAsync(int schoolId, CancellationToken ct);
    Task<IReadOnlyList<string>> ProtectedProviderIdsAsync(int schoolId, IReadOnlyList<string> itemIds, CancellationToken ct);
    Task<IReadOnlyList<int>> DueAsync(DateTimeOffset now, int limit, CancellationToken ct);
    Task<bool> ExclusiveAsync(string resource, Func<CancellationToken, Task> action, CancellationToken ct);
    Task<VisitArchiveOperation?> ClaimAsync(int id, DateTimeOffset now, Guid token, CancellationToken ct);
    Task RenewAsync(int id, Guid token, DateTimeOffset now, CancellationToken ct);
    Task SaveAsync(VisitArchiveOperation operation, string action, CancellationToken ct);
    Task FailAsync(int id, Guid token, string code, DateTimeOffset now, CancellationToken ct);
    Task<StorageFolder?> ArchiveFolderAsync(int schoolId, CancellationToken ct);
    Task AddArchiveFolderAsync(StorageFolder folder, CancellationToken ct);
    Task CompleteAsync(VisitArchiveOperation operation, CancellationToken ct);
    Task SetAvailabilityAsync(ArchiveArtifactRow row, bool missing, DateTimeOffset now, CancellationToken ct);
    Task RetryAsync(int schoolId, int visitId, RetryVisitArchiveRequest request, string actor, CancellationToken ct);
    Task<IReadOnlyList<ArchiveArtifactRow>> ReconcileBatchAsync(int afterId, int limit, CancellationToken ct);
}
public interface IVisitArchiveService
{
    Task<VisitArchiveOperationsDto> OperationsStatusAsync(CancellationToken ct = default);
    Task<IReadOnlyList<VisitArchiveTeacherDto>> TeachersAsync(CancellationToken ct = default);
    Task<StoragePage<VisitArchiveDto>> ListAsync(VisitArchiveQuery query, CancellationToken ct = default);
    Task<VisitArchiveDto> GetAsync(int visitId, CancellationToken ct = default);
    Task<VisitArchiveDto> RetryAsync(int visitId, RetryVisitArchiveRequest request, CancellationToken ct = default);
    Task<DriveFileContentDto> ContentAsync(int visitId, int revision, int? versionId, CancellationToken ct = default);
}
public interface IVisitArchiveProcessor
{
    Task<int> ProcessBatchAsync(CancellationToken ct = default);
    Task<int> ReconcileAsync(CancellationToken ct = default);
}
