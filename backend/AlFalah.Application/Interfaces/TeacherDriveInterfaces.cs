using System.Security.Claims;
using AlFalah.Application.DTOs.EvidenceMatrix;
using AlFalah.Application.DTOs.TeacherDrive;

namespace AlFalah.Application.Interfaces;

public interface IMicrosoftGraphTokenService
{
    Task<string> GetForUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}

public interface ITeacherMicrosoftAccountService
{
    Task<TeacherDriveStatusDto> GetStatusAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task<LinkMicrosoftAccountResultDto> LinkAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task<(int TeacherId, int SchoolId, string TeacherDisplayName)> ResolveLinkedTeacherAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task<TeacherMicrosoftAccountAdminDto> ConfigureExpectedEmailAsync(int teacherId, string microsoftEmail, CancellationToken cancellationToken = default);
}

public interface ITeacherDriveMappingService
{
    Task<DriveFolderMappingDto> GetForTeacherAsync(int teacherId, CancellationToken cancellationToken = default);
    Task<DriveFolderMappingDto> UpsertAsync(int teacherId, UpsertDriveFolderMappingRequest request, CancellationToken cancellationToken = default);
}

public interface ISchoolMicrosoftDriveService
{
    Task<SchoolMicrosoftDriveSettingsDto> GetForCurrentSchoolAsync(CancellationToken cancellationToken = default);
    Task<SchoolMicrosoftDriveSettingsDto> ConfigureForCurrentSchoolAsync(ConfigureSchoolMicrosoftDriveRequest request, CancellationToken cancellationToken = default);
}

public interface IOneDriveBrowserService
{
    Task<DriveItemsPageDto> ListAsync(ClaimsPrincipal principal, DriveQuery query, CancellationToken cancellationToken = default);
    Task<DriveItemDto> GetItemAsync(ClaimsPrincipal principal, string itemId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DriveBreadcrumbDto>> GetBreadcrumbAsync(ClaimsPrincipal principal, string? itemId, CancellationToken cancellationToken = default);
    Task<FilePreviewDto> GetPreviewAsync(ClaimsPrincipal principal, string itemId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecentFileDto>> GetRecentAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}

public interface IOneDriveUploadService
{
    Task<UploadFileResultDto> UploadAsync(ClaimsPrincipal principal, UploadFileRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(ClaimsPrincipal principal, long submissionId, CancellationToken cancellationToken = default);
}

public interface IEvidenceSubmissionService
{
    Task<EvidenceUploadCatalogDto> GetUploadCatalogAsync(CancellationToken cancellationToken = default);
    Task<EvidenceUploadReservationDto> ReserveUploadAsync(int teacherId, int schoolId, int taskId, string requestId, CancellationToken cancellationToken = default);
    Task<UploadFileResultDto> RecordCompletedUploadAsync(long operationId, int teacherId, int schoolId, string driveId, string parentItemId, DriveItemDto item, CancellationToken cancellationToken = default);
    Task MarkUploadFailedAsync(long operationId, string reason, CancellationToken cancellationToken = default);
    Task MarkDeletedAsync(int teacherId, long submissionId, string? deletedByUserId, CancellationToken cancellationToken = default);
}
