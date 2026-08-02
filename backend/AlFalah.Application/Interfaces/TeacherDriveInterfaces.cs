using AlFalah.Application.DTOs.EvidenceMatrix;
using AlFalah.Application.DTOs.TeacherDrive;

namespace AlFalah.Application.Interfaces;

/// <summary>
/// Mints short-lived Google OAuth access tokens for a school's stored credential.
/// Implementations cache tokens in memory; the credential plaintext never leaves them.
/// </summary>
public interface IGoogleDriveTokenService
{
    Task<string> GetAccessTokenAsync(int schoolId, CancellationToken cancellationToken = default);

    /// <summary>Drops the cached token so the next call re-authenticates. Called after a credential change.</summary>
    void InvalidateCachedToken(int schoolId);
}

/// <summary>
/// The thin Google Drive v3 transport. Everything above it is provider-agnostic policy,
/// which is what lets the whole permission and ledger flow be tested without a network.
/// </summary>
public interface IGoogleDriveClient
{
    /// <summary>Returns null when the file does not exist (or is no longer visible to the school credential).</summary>
    Task<GoogleDriveFile?> GetFileAsync(int schoolId, string fileId, CancellationToken cancellationToken = default);

    Task<GoogleDriveFileList> ListChildrenAsync(int schoolId, GoogleDriveListRequest request, CancellationToken cancellationToken = default);

    Task<GoogleDriveFile> UploadAsync(int schoolId, GoogleDriveUploadRequest request, CancellationToken cancellationToken = default);

    /// <summary>Opens the file's bytes for streaming. The caller owns the returned stream.</summary>
    Task<DriveFileContentDto> DownloadAsync(int schoolId, string fileId, CancellationToken cancellationToken = default);

    /// <summary>Moves the file to the credential owner's trash. Returns false when it was already gone.</summary>
    Task<bool> TrashAsync(int schoolId, string fileId, string? sharedDriveId, CancellationToken cancellationToken = default);
}

/// <summary>Resolves the signed-in local user to the teacher whose evidence folder they may use.</summary>
public interface ITeacherDriveIdentityService
{
    Task<TeacherDriveIdentity> ResolveCurrentTeacherAsync(CancellationToken cancellationToken = default);
    Task<TeacherDriveStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);
}

public interface ITeacherDriveMappingService
{
    Task<DriveFolderMappingDto> GetForTeacherAsync(int teacherId, CancellationToken cancellationToken = default);
    Task<DriveFolderMappingDto?> FindForTeacherAsync(int teacherId, CancellationToken cancellationToken = default);
    Task<DriveFolderMappingDto> UpsertAsync(int teacherId, UpsertDriveFolderMappingRequest request, CancellationToken cancellationToken = default);
    Task RevokeAsync(int teacherId, CancellationToken cancellationToken = default);
}

public interface ISchoolGoogleDriveService
{
    Task<SchoolGoogleDriveSettingsDto> GetForCurrentSchoolAsync(CancellationToken cancellationToken = default);
    Task<SchoolGoogleDriveSettingsDto> ConfigureForCurrentSchoolAsync(ConfigureSchoolGoogleDriveRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Runs the Google OAuth 2.0 authorization-code flow so no one has to obtain a refresh token
/// by hand. The school's own OAuth client (id + secret) is still configured through
/// <see cref="ISchoolGoogleDriveService"/>; this covers only the consent round trip.
///
/// The two halves have deliberately different trust models — see the implementation.
/// </summary>
public interface IGoogleDriveOAuthService
{
    /// <summary>
    /// Builds the consent URL for the caller's school. Requires an authenticated school
    /// manager, and requires that the school's OAuth client id and secret are already stored.
    /// </summary>
    Task<GoogleAuthUrlDto> GetAuthUrlForCurrentSchoolAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchanges Google's one-time authorization code for a refresh token and stores it
    /// encrypted against the school named in <paramref name="state"/>.
    ///
    /// Called from Google's browser redirect, which carries no JWT: the school and the acting
    /// manager are taken from <paramref name="state"/> and from nowhere else.
    /// </summary>
    Task<GoogleOAuthConnectionResultDto> ExchangeAuthCodeAsync(string code, string state, CancellationToken cancellationToken = default);
}

public interface IGoogleDriveBrowserService
{
    Task<DriveItemsPageDto> ListAsync(DriveQuery query, CancellationToken cancellationToken = default);
    Task<DriveItemDto> GetItemAsync(string itemId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DriveBreadcrumbDto>> GetBreadcrumbAsync(string? itemId, CancellationToken cancellationToken = default);

    /// <summary>Streams a file the signed-in teacher is granted, enforcing folder isolation first.</summary>
    Task<DriveFileContentDto> DownloadAsync(string itemId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecentFileDto>> GetRecentAsync(CancellationToken cancellationToken = default);
}

public interface IGoogleDriveUploadService
{
    Task<UploadFileResultDto> UploadAsync(UploadFileRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(long submissionId, CancellationToken cancellationToken = default);
}

public interface IEvidenceSubmissionService
{
    Task<EvidenceUploadCatalogDto> GetUploadCatalogAsync(CancellationToken cancellationToken = default);
    Task<EvidenceUploadReservationDto> ReserveUploadAsync(int teacherId, int schoolId, int taskId, string requestId, CancellationToken cancellationToken = default);
    Task<UploadFileResultDto> RecordCompletedUploadAsync(long operationId, int teacherId, int schoolId, string driveId, string parentItemId, DriveItemDto item, CancellationToken cancellationToken = default);
    Task MarkUploadFailedAsync(long operationId, string reason, CancellationToken cancellationToken = default);
    Task MarkDeletedAsync(int teacherId, long submissionId, string? deletedByUserId, CancellationToken cancellationToken = default);
}
