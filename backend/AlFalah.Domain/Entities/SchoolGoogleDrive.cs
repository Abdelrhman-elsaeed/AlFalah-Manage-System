using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities;

/// <summary>
/// The single school-owned Google Drive that contains every teacher evidence folder.
///
/// The school supplies ONE credential; the application acts as that credential for all
/// Drive traffic and enforces per-teacher folder isolation itself through
/// <see cref="TeacherDriveFolder"/>. Teachers therefore need no Google account and no
/// second sign-in — the ledger, the audit log and the evidence matrix are what record
/// which teacher uploaded which file.
/// </summary>
public sealed class SchoolGoogleDrive
{
    public int Id { get; set; }
    public int SchoolId { get; set; }

    public GoogleDriveCredentialType CredentialType { get; set; } = GoogleDriveCredentialType.ServiceAccount;

    /// <summary>Owner of the root folder. Informational only — never used to authenticate.</summary>
    public string SchoolGoogleEmail { get; set; } = string.Empty;

    /// <summary>
    /// Data-Protection ciphertext of the secret: the service-account JSON key, or the
    /// OAuth refresh token. Plaintext never leaves <c>GoogleDriveTokenService</c> and is
    /// never returned by any API.
    /// </summary>
    public string ProtectedCredential { get; set; } = string.Empty;

    /// <summary>OAuth client id. Required for <see cref="GoogleDriveCredentialType.OAuthRefreshToken"/> only.</summary>
    public string? OAuthClientId { get; set; }

    /// <summary>Data-Protection ciphertext of the OAuth client secret. Refresh-token grant only.</summary>
    public string? ProtectedOAuthClientSecret { get; set; }

    /// <summary>
    /// Workspace user the service account impersonates via domain-wide delegation. When
    /// set, uploaded files are owned by this user instead of the service account, which
    /// keeps them inside the school's Workspace storage quota.
    /// </summary>
    public string? ImpersonatedUserEmail { get; set; }

    /// <summary>
    /// Google shared-drive id when the evidence tree lives on a shared drive; null when it
    /// lives in an ordinary My Drive.
    ///
    /// Optional for every credential type. Note that a service account owns no storage quota,
    /// so while it can browse and download from a My Drive folder shared with it, an upload it
    /// performs there is refused by Google with <c>storageQuotaExceeded</c> — a shared drive or
    /// <see cref="ImpersonatedUserEmail"/> is what makes writes work.
    /// </summary>
    public string? SharedDriveId { get; set; }

    /// <summary>Google folder id of the school-wide evidence root. Every teacher folder must sit beneath it.</summary>
    public string RootFolderId { get; set; } = string.Empty;

    public string RootFolderDisplayName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset ConnectedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public School School { get; set; } = null!;
}
