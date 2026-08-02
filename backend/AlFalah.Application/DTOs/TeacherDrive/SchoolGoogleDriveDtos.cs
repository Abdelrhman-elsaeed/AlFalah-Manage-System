using AlFalah.Domain.Enums;

namespace AlFalah.Application.DTOs.TeacherDrive;

/// <summary>
/// The manager-visible view of the school's Google Drive connection. It reports whether a
/// credential is stored but never returns any part of it — not even masked.
/// </summary>
public sealed record SchoolGoogleDriveSettingsDto(
    int SchoolId,
    bool IsConfigured,
    bool IsEnabled,
    GoogleDriveCredentialType? CredentialType,
    string? SchoolGoogleEmail,
    string? ImpersonatedUserEmail,
    string? OAuthClientId,
    string? SharedDriveId,
    string? RootFolderId,
    string? RootFolderDisplayName,
    bool HasStoredCredential,
    DateTimeOffset? ConnectedAtUtc);

/// <summary>
/// Secret-bearing fields are nullable on purpose: sending null keeps whatever is already
/// stored, so a manager can rename the root folder without re-pasting the service-account
/// key. They are write-only — no response ever echoes them back.
/// </summary>
public sealed record ConfigureSchoolGoogleDriveRequest(
    GoogleDriveCredentialType CredentialType,
    string SchoolGoogleEmail,
    string? ServiceAccountJson,
    string? ImpersonatedUserEmail,
    string? OAuthClientId,
    string? OAuthClientSecret,
    string? OAuthRefreshToken,
    string? SharedDriveId,
    string RootFolderId,
    string RootFolderDisplayName,
    bool IsEnabled);
