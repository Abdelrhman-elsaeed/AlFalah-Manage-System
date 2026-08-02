namespace AlFalah.Domain.Enums;

/// <summary>
/// How a school's Google Drive credential authenticates. Both variants end up as a
/// short-lived OAuth access token; only the grant differs.
/// </summary>
public enum GoogleDriveCredentialType
{
    /// <summary>
    /// A Google Cloud service-account JSON key. Signs a JWT assertion itself, so no
    /// interactive consent is ever required. Optionally impersonates a Workspace user
    /// through domain-wide delegation.
    /// </summary>
    ServiceAccount = 1,

    /// <summary>
    /// A long-lived refresh token belonging to the school's own Google account,
    /// obtained once by an administrator. Works with a plain (non-Workspace) account.
    /// </summary>
    OAuthRefreshToken = 2
}
