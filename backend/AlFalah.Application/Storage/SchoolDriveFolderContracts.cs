using AlFalah.Application.DTOs.TeacherDrive;

namespace AlFalah.Application.Storage;

// These provider identifiers are confined to the manager's connection settings.
public sealed record SchoolDriveBrowseRequest(string? ParentItemId = null, string? PageToken = null, string? Search = null);
public sealed record SchoolDriveBrowseItem(string ItemId, string Name, bool IsFolder, long? Size, string MimeType, bool CanSelect);
public sealed record SchoolDriveFolderPage(string CurrentFolderId, string CurrentFolderName, bool IsAccountRoot,
    bool CanSelectCurrent, IReadOnlyList<DriveBreadcrumbDto> Breadcrumbs, IReadOnlyList<SchoolDriveBrowseItem> Items, string? NextPageToken);
public sealed record SchoolDriveSetupConnection(string? SharedDriveId, bool HasCredential, DateTimeOffset UpdatedAtUtc);
public sealed record SchoolDriveFolderBoundary(string ItemId, bool OtherSchool, bool TeacherGrant);
public sealed record SchoolDriveRootSelection(string ItemId, string Name, string? SharedDriveId);

public interface ISchoolDriveSetupRepository
{
    Task<bool> CanConfigureAsync(string userId, int schoolId, CancellationToken ct);
    Task<SchoolDriveSetupConnection?> ConnectionAsync(int schoolId, CancellationToken ct);
    Task<IReadOnlyList<SchoolDriveFolderBoundary>> BoundariesAsync(int schoolId, CancellationToken ct);
}

// Metadata GETs only. No upload, download, sharing, deletion or folder creation.
public interface IGoogleDriveSetupReader
{
    Task<GoogleDriveFile?> FolderAsync(int schoolId, string itemId, CancellationToken ct);
    Task<GoogleDriveFileList> ChildrenAsync(int schoolId, GoogleDriveListRequest request, CancellationToken ct);
}
public interface ISchoolDriveFolderService
{
    Task<SchoolDriveFolderPage> BrowseAsync(SchoolDriveBrowseRequest request, CancellationToken ct = default);
    Task<SchoolDriveRootSelection> ValidateRootAsync(string itemId, CancellationToken ct = default);
}
