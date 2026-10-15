using AlFalah.Domain.Entities.Storage;

namespace AlFalah.Application.Storage;

public sealed record DriveBrowserItem(string Handle, string Name, bool IsFolder, long? Size, string MimeType,
    DateTimeOffset? ModifiedAt, bool CanModify, string? Protection, int? StoredFileId = null, int? FolderId = null);
public sealed record DriveBrowserCrumb(string Handle, string Name);
public sealed record DriveBrowserPage(string Name, string? Handle, IReadOnlyList<DriveBrowserCrumb> Breadcrumbs,
    IReadOnlyList<DriveBrowserItem> Items, string? NextPageToken, bool CanManage, int? WritableFolderId = null);
public sealed record DriveRegisteredFile(string Item, int Id, int FolderId);
public sealed record DriveLibraryReference(int FolderId, string Folder, string? File);
public sealed record DriveBrowserResolution(string Handle, DriveBrowserItem? OpenFile);
public sealed record DriveImpactRequest(string Handle, string Action, string? Name = null, string? Destination = null);
public sealed record DriveImpactDto(string Ticket, string Name, string Action, int Files, int Folders,
    IReadOnlyList<string> LinkedTo, int LinkedCount, int SignedDocuments, string Warning);
public sealed record DriveMutationRequest(string Ticket, string RequestKey);
public sealed record DriveMutationDto(int Id, string Status, string Action, string Name, string? ErrorCode);
// Protected, expiring handles are bound to school, configured root and current actor.
public sealed record DriveBrowserHandle(int School, string Root, string Item, string Actor, DateTimeOffset Expires);
public sealed record DriveMutationPlan(DriveBrowserHandle Handle, string Action, string? Name,
    string? Destination, string Parent, IReadOnlyList<string> Items, string LedgerFingerprint, string ProviderVersion, string OriginalName);
public sealed record DriveLedgerImpact(string Fingerprint, IReadOnlyList<string> LinkedTo, int LinkedCount,
    int SignedDocuments, bool FixedIdentity, bool Busy);
public sealed record DriveMutationOperation(StorageOperation Operation, DriveMutationPlan Plan);
public sealed record DriveFolderPlacement(int Id, int? Owner, Domain.Enums.StorageFolderKind Kind);
public sealed record DriveContentProof(string Item, string Hash, long Length, bool Signed = false);
public sealed record DriveExpectedLocation(string Item, string Name, string? Parent);
public interface IStorageDriveHandles
{
    string ProtectHandle(DriveBrowserHandle value);
    DriveBrowserHandle ReadHandle(string value);
    string ProtectPlan(DriveMutationPlan value);
    DriveMutationPlan ReadPlan(string value);
}
public interface IStorageDriveBrowserRepository
{
    Task<IReadOnlyList<DriveRegisteredFile>> RegisteredFilesAsync(int school, IReadOnlyList<string> items, CancellationToken ct);
    Task<DriveLibraryReference?> LibraryReferenceAsync(int school, int? folder, int? file, CancellationToken ct);
    Task<DriveLedgerImpact> ImpactAsync(int school, IReadOnlyList<string> providerIds, CancellationToken ct);
    Task<IReadOnlyList<string>> ProtectionAsync(int school, IReadOnlyList<string> providerIds, CancellationToken ct);
    Task<DriveFolderPlacement?> DestinationAsync(int school, string item, CancellationToken ct);
    Task<IReadOnlyList<DriveFolderPlacement>> PlacementsAsync(int school, IReadOnlyList<string> items, CancellationToken ct);
    Task<IReadOnlyList<DriveContentProof>> ProofsAsync(int school, IReadOnlyList<string> items, CancellationToken ct);
    Task<IReadOnlyList<DriveExpectedLocation>> LocationsAsync(int school, IReadOnlyList<string> items, string root, CancellationToken ct);
    Task<IReadOnlyList<DriveMutationDto>> DeletedAsync(int school, CancellationToken ct);
    Task<DriveMutationOperation?> OperationAsync(int school, string actor, string key, CancellationToken ct);
    Task<DriveMutationOperation?> OperationAsync(int school, int id, CancellationToken ct);
    Task<IReadOnlyList<DriveMutationDto>> PendingAsync(int school, CancellationToken ct);
    Task<DriveMutationOperation> ReserveAsync(int school, string actor, string key, DriveMutationPlan plan, string fingerprint, string drive, CancellationToken ct);
    Task CompleteAsync(DriveMutationOperation operation, string actor, CancellationToken ct);
    Task UncertainAsync(StorageOperation operation, string code, CancellationToken ct);
    Task ExclusiveAsync(int school, Func<Task> action, CancellationToken ct);
}
public interface IStorageDriveBrowserService
{
    Task<DriveBrowserResolution> ResolveAsync(int? folder, int? file, CancellationToken ct);
    Task<DriveBrowserPage> BrowseAsync(string? handle, string? pageToken, CancellationToken ct);
    Task<DTOs.TeacherDrive.DriveFileContentDto> ContentAsync(string handle, CancellationToken ct);
    Task<DriveImpactDto> ImpactAsync(DriveImpactRequest request, CancellationToken ct);
    Task<DriveMutationDto> MutateAsync(DriveMutationRequest request, CancellationToken ct);
    Task<DriveMutationDto> ReconcileAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<DriveMutationDto>> PendingAsync(CancellationToken ct);
    Task<IReadOnlyList<DriveMutationDto>> DeletedAsync(CancellationToken ct);
    Task<DriveMutationDto> RestoreAsync(int deleteOperation, CancellationToken ct);
}
