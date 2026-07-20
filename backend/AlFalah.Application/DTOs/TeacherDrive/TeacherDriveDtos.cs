using System.Security.Claims;
using AlFalah.Application.DTOs.EvidenceMatrix;

namespace AlFalah.Application.DTOs.TeacherDrive;

public sealed record TeacherDriveStatusDto(
    bool IsMicrosoftLinked,
    bool IsDriveConfigured,
    string? FolderDisplayName,
    string ConnectionState,
    string TeacherDisplayName);

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
    string? SubmissionStatus);

public sealed record DriveItemsPageDto(
    IReadOnlyList<DriveItemDto> Items,
    string? NextPageToken,
    int TotalInPage);

public sealed record DriveBreadcrumbDto(string ItemId, string Name);

public sealed record FilePreviewDto(string PreviewUrl, string WebUrl, string Name, string? MimeType);

public sealed record RecentFileDto(string ItemId, string Name, string? Extension, long? Size, DateTimeOffset? UploadedAtUtc, string? WebUrl);

public sealed record LinkMicrosoftAccountResultDto(bool IsLinked, string Message);

public sealed record DriveFolderMappingDto(int TeacherId, int SchoolId, string DriveId, string RootItemId, string FolderDisplayName, string? RootWebUrl, bool IsActive);

public sealed record TeacherMicrosoftAccountAdminDto(int TeacherId, string MicrosoftEmail, bool IsLinked, string? ObjectId, DateTimeOffset? LinkedAtUtc);

public sealed record UpsertDriveFolderMappingRequest(string DriveId, string RootItemId, string FolderDisplayName, string? RootWebUrl, bool IsActive);

public sealed record DriveQuery(string? ParentItemId, string? Search, string? SortBy, string? SortDirection, string? PageToken);

public sealed record UploadFileRequest(Stream Content, string FileName, string? ContentType, long Length, string? ParentItemId, int TaskId, string RequestId);

public sealed record UploadFileResultDto(long SubmissionId, DriveItemDto Item);

public sealed record EvidenceUploadReservationDto(long OperationId, int AcademicYearId, UploadFileResultDto? ExistingResult);

public sealed record EntraIdentity(string TenantId, string ObjectId, string Email, ClaimsPrincipal Principal);
