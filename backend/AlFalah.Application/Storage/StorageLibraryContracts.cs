using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.Storage;

public sealed record StorageListRequest(int? FolderId = null, string? Search = null, bool Global = false,
    string Sort = "name", bool Descending = false, int Page = 1, int PageSize = 25);
public sealed record StoragePage<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
public sealed record StorageFolderDto(int Id, int? ParentFolderId, string DisplayName, string Kind, string RowVersion);
public sealed record StorageDiscoveryItemDto(int? FolderId, int? StoredFileId, string DisplayName, bool IsFolder, long? Size, string? MimeType, string State);
public sealed record StorageDiscoveryPageDto(IReadOnlyList<StorageDiscoveryItemDto> Items, string? NextPageToken);
public sealed record StorageFileListDto(int StoredFileId, int FolderId, string DisplayName, long Size,
    string? MimeType, DateTimeOffset UploadedAt, string State, bool IsProtected, string RowVersion);
public sealed record StorageVersionDto(int VersionId, int VersionNumber, long Size, string? MimeType,
    DateTimeOffset UploadedAt, string Availability);
public sealed record StorageFileDetailsDto(StorageFileListDto File, IReadOnlyList<StorageVersionDto> Versions);
public sealed record StorageUploadDto(int OperationId, int? StoredFileId, int? VersionId, string Status,
    string DisplayName, long Size, string MimeType, DateTimeOffset UploadedAt, string? ErrorCode);
public sealed record StorageContextDto(int SchoolId, string SchoolName, int? AcademicYearId, string? AcademicYearName,
    bool CanManage, bool IsTeacher, string ConnectionState, int? RootFolderId);
public sealed record CreateStorageFolderRequest(int? ParentFolderId, string DisplayName, string RequestKey);
public sealed record MoveStorageFolderRequest(int ParentFolderId, string RowVersion);
public sealed record RenameStorageFileRequest(string DisplayName, string RowVersion);
public sealed record DeleteStorageFileRequest(string RowVersion);
public sealed record StorageUploadRequest(Stream Content, string FileName, long Length, int? ParentFolderId,
    string RequestKey, bool Own, int? LegacyTaskId = null, int? ChangeRequestId = null);
// Provider IDs are confined to these internal projections.
public sealed record StorageFolderAccess(StorageFolder Folder, StorageDriveRoot SchoolRoot, StorageDriveRoot AuthorizedRoot);
public sealed record StorageReadRow(StorageFileListDto Dto, string DriveItemId, StoredFileSourceKind SourceKind);
public sealed record StorageMutationTarget(StoredFile File, StoredFileVersion Version, bool Protected);
public sealed record StorageTeacher(int Id, string UserId, StorageDriveRoot Root, string DisplayName);

public interface IStorageLibraryRepository
{
    Task RecordContentReadAsync(int schoolId, int fileId, string actor, CancellationToken ct);
    Task<StorageTeacher?> FindTeacherAsync(int schoolId, string userId, CancellationToken ct);
    Task<StorageContextDto> ContextAsync(int schoolId, CancellationToken ct);
    Task<IReadOnlyList<StorageDriveRoot>> TeacherRootsAsync(int schoolId, CancellationToken ct);
    Task<StorageFolder?> FindFolderAsync(int schoolId, int id, CancellationToken ct);
    Task<StorageFolder?> FindProviderFolderAsync(int schoolId, string itemId, CancellationToken ct);
    Task<StorageFolder?> FindRootAsync(int schoolId, int? teacherId, CancellationToken ct);
    Task<StorageFolder> AddFolderAsync(StorageFolder folder, CancellationToken ct);
    Task SaveFolderAsync(StorageFolder folder, byte[] expectedVersion, CancellationToken ct);
    Task<StoragePage<StorageFolderDto>> FoldersAsync(int schoolId, int? owner, int? parent, int page, int pageSize, CancellationToken ct);
    Task<StoragePage<StorageReadRow>> FilesAsync(int schoolId, int? owner, StorageListRequest request, CancellationToken ct);
    Task<StorageFileDetailsDto?> DetailsAsync(int schoolId, int id, CancellationToken ct);
    Task<StorageMutationTarget?> MutationTargetAsync(int schoolId, int id, CancellationToken ct);
    Task<bool> HasProtectedDescendantsAsync(int schoolId, int folderId, CancellationToken ct);
    Task SetAvailabilityAsync(int schoolId, int id, bool missing, CancellationToken ct);
    Task<StorageOperation?> FindOperationAsync(int schoolId, string actor, string key, CancellationToken ct);
    Task<StorageOperation?> GetOperationAsync(int schoolId, int id, CancellationToken ct);
    Task AddOperationAsync(StorageOperation operation, CancellationToken ct);
    Task SaveOperationAsync(StorageOperation operation, CancellationToken ct);
    Task CompleteUploadAsync(StorageOperation operation, CancellationToken ct);
    Task SaveMutationAsync(StorageMutationTarget target, byte[] expectedVersion, string actor, bool delete, CancellationToken ct);
    Task<int?> FindLegacyFileAsync(int schoolId, long submissionId, CancellationToken ct);
    Task<StorageDiscoveryPageDto> IndexDiscoveredFoldersAsync(StorageFolder parent, GoogleDriveFileList page, string actor, CancellationToken ct);
}

public interface IStorageProvider
{
    void ResetRequestCache();
    Task<string> AllocateIdAsync(int schoolId, CancellationToken ct);
    Task<GoogleDriveFile?> MetadataAsync(int schoolId, string id, CancellationToken ct);
    Task<GoogleDriveFile> CreateFolderAsync(int schoolId, string id, string parent, string name, CancellationToken ct);
    Task<GoogleDriveFile> UploadAsync(int schoolId, string id, string parent, string name, string mime, Stream content, CancellationToken ct);
    Task<GoogleDriveFile> UploadArchiveAsync(int schoolId, string id, string parent, string name,
        Stream content, IReadOnlyDictionary<string, string> identity, CancellationToken ct) =>
        throw new NotSupportedException("Provider must support archive identity metadata.");
    Task<GoogleDriveFile> MoveAsync(int schoolId, string id, string oldParent, string newParent, CancellationToken ct);
    Task<GoogleDriveFile> RenameAsync(int schoolId, string id, string name, CancellationToken ct);
    Task<bool> TrashAsync(int schoolId, string id, string driveId, CancellationToken ct);
    Task<DriveFileContentDto> ContentAsync(int schoolId, string id, CancellationToken ct);
    Task<bool> IsWithinAsync(int schoolId, string root, string item, CancellationToken ct);
    Task<GoogleDriveFileList> ChildrenAsync(int schoolId, string parent, string driveId, string? token, CancellationToken ct);
}

public interface IStorageLibraryService
{
    Task<StorageContextDto> ContextAsync(bool own, CancellationToken ct = default);
    Task<StoragePage<StorageFolderDto>> FoldersAsync(bool own, int? parent, int page, int pageSize, CancellationToken ct = default);
    Task<StoragePage<StorageFileListDto>> FilesAsync(bool own, StorageListRequest request, CancellationToken ct = default);
    Task<StorageDiscoveryPageDto> DiscoverAsync(bool own, int folderId, string? token, CancellationToken ct = default);
    Task<StorageFileDetailsDto> DetailsAsync(int id, CancellationToken ct = default);
    Task<DriveFileContentDto> ContentAsync(int id, CancellationToken ct = default);
    Task<StorageFolderDto> CreateFolderAsync(CreateStorageFolderRequest request, CancellationToken ct = default);
    Task<StorageFolderDto> MoveFolderAsync(int id, MoveStorageFolderRequest request, CancellationToken ct = default);
    Task<StorageUploadDto> UploadAsync(StorageUploadRequest request, CancellationToken ct = default);
    Task<StorageUploadDto> ReconcileAsync(int id, CancellationToken ct = default);
    Task RenameAsync(int id, RenameStorageFileRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, DeleteStorageFileRequest request, CancellationToken ct = default);
    Task<UploadFileResultDto> LegacyUploadAsync(UploadFileRequest request, CancellationToken ct = default);
    Task<DriveItemDto> LegacyRenameAsync(long submissionId, string name, CancellationToken ct = default);
    Task LegacyDeleteAsync(long submissionId, CancellationToken ct = default);
}

public sealed class StorageUnavailableException : Exception
{
    public StorageUnavailableException(string? message = null) : base(message ?? "اتصال الملفات غير متاح حاليًا. أعد المحاولة لاحقًا.") { }
}
// A complete rejection response (quota/auth/rate limit), rather than an ambiguous lost response.
public sealed class StorageProviderRejectedException(string message) : InvalidOperationException(message);
