namespace AlFalah.Application.DTOs.TeacherDrive;

/// <summary>
/// What the teacher's client is allowed to know about its own access. It deliberately
/// carries no Google folder id — those are administrator-only identifiers.
/// </summary>
public sealed record TeacherDriveStatusDto(
    bool IsSchoolDriveEnabled,
    bool IsFolderAssigned,
    string? FolderDisplayName,
    string ConnectionState,
    string TeacherDisplayName);

/// <summary>Connection states surfaced by <see cref="TeacherDriveStatusDto.ConnectionState"/>.</summary>
public static class TeacherDriveConnectionState
{
    /// <summary>The signed-in user is not an active teacher, so evidence files do not apply to them.</summary>
    public const string NotATeacher = "NotATeacher";

    /// <summary>The school manager has not connected a Google Drive account yet.</summary>
    public const string SchoolNotConfigured = "SchoolNotConfigured";

    /// <summary>The school is connected but no administrator has granted this teacher a folder.</summary>
    public const string FolderNotAssigned = "FolderNotAssigned";

    public const string Connected = "Connected";
}

public sealed record DriveItemDto(
    string ItemId,
    string Name,
    bool IsFolder,
    int? ChildCount,
    string? Extension,
    string? MimeType,
    long? Size,
    DateTimeOffset? LastModifiedAt,
    string? LastModifiedBy,
    string? WebUrl,
    string? ETag,
    string? SubmissionStatus,
    long? SubmissionId = null);

public sealed record DriveItemsPageDto(
    IReadOnlyList<DriveItemDto> Items,
    string? NextPageToken,
    int TotalInPage);

public sealed record DriveBreadcrumbDto(string ItemId, string Name);

/// <summary>
/// File bytes streamed back through the API.
///
/// Drive's own <c>webViewLink</c> is useless to our users: the files belong to the school
/// credential and neither teachers nor managers hold a Google session, so following that
/// link only ever produces Google's "Request access" page. Serving the bytes ourselves is
/// also what keeps folder isolation in force on the way out.
/// </summary>
public sealed record DriveFileContentDto(Stream Content, string FileName, string ContentType, long? Length);

public sealed record RecentFileDto(string ItemId, string Name, string? Extension, long? Size, DateTimeOffset? UploadedAtUtc, string? WebUrl);

/// <summary>The resolved teacher behind a teacher-drive request.</summary>
public sealed record TeacherDriveIdentity(int TeacherId, int SchoolId, string TeacherDisplayName);

public sealed record DriveFolderMappingDto(int TeacherId, int SchoolId, string DriveId, string RootItemId, string FolderDisplayName, string? RootWebUrl, bool IsActive);

/// <summary>A folder shown to an administrator while choosing a teacher's evidence root.</summary>
public sealed record AdminDriveFolderItemDto(
    string ItemId,
    string Name,
    bool IsAssigned,
    bool IsAssignedToCurrentTeacher,
    string? AssignedTeacherName);

public sealed record AdminDriveFolderPageDto(
    string CurrentFolderId,
    string CurrentFolderName,
    bool IsSchoolRoot,
    IReadOnlyList<AdminDriveFolderItemDto> Folders,
    string? NextPageToken);

public sealed record BrowseAdminDriveFoldersRequest(string? ParentItemId, string? PageToken);

/// <summary>
/// The grant an administrator makes — the folder id is the only thing they supply.
///
/// <c>DriveId</c> is intentionally absent: the server takes it from the school's connected
/// drive so a grant can never point outside it. The display name and web link are absent for
/// the same reason turned practical — the server already fetches the folder from Drive to
/// validate it, so it reads the real name and link from that response instead of trusting (or
/// bothering) a human to retype them.
/// </summary>
public sealed record UpsertDriveFolderMappingRequest(string RootItemId);

public sealed record DriveQuery(string? ParentItemId, string? Search, string? SortBy, string? SortDirection, string? PageToken);

public sealed record UploadFileRequest(Stream Content, string FileName, string? ContentType, long Length, string? ParentItemId, int TaskId, string RequestId);

public sealed record UploadFileResultDto(long SubmissionId, DriveItemDto Item);

public sealed record RenameEvidenceSubmissionRequest(string Name);

public sealed record EvidenceUploadReservationDto(long OperationId, int AcademicYearId, UploadFileResultDto? ExistingResult);

// ─── Google Drive v3 transport shapes ────────────────────────────────────────

public sealed record GoogleDriveFile(
    string Id,
    string Name,
    string MimeType,
    long? Size,
    DateTimeOffset? ModifiedTime,
    string? LastModifiedBy,
    string? WebViewLink,
    string? Version,
    IReadOnlyList<string> Parents,
    bool Trashed)
{
    public const string FolderMimeType = "application/vnd.google-apps.folder";
    public bool IsFolder => string.Equals(MimeType, FolderMimeType, StringComparison.Ordinal);
}

public sealed record GoogleDriveFileList(IReadOnlyList<GoogleDriveFile> Files, string? NextPageToken);

public sealed record GoogleDriveListRequest(
    string ParentFolderId,
    string? NameContains,
    string OrderBy,
    int PageSize,
    string? PageToken,
    string? SharedDriveId);

public sealed record GoogleDriveUploadRequest(
    Stream Content,
    string FileName,
    string ContentType,
    string ParentFolderId,
    string? SharedDriveId);
