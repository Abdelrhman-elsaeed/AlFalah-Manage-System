namespace AlFalah.Application.DTOs.TeacherDrive;

/// <summary>
/// The Google consent URL a school manager is sent to, plus the opaque <c>state</c> that ties
/// the eventual callback back to this school and this manager.
///
/// <see cref="State"/> is already embedded in <see cref="AuthorizationUrl"/> and is surfaced
/// only so a caller can correlate the round trip in its own logs — it is not a secret, but it
/// is integrity-protected, so a client cannot usefully construct or edit one.
/// </summary>
public sealed record GoogleAuthUrlDto(
    string AuthorizationUrl,
    string State,
    DateTimeOffset StateExpiresAtUtc);

/// <summary>
/// Outcome of a completed authorization-code exchange. Carries no part of either token: the
/// refresh token goes straight to encrypted storage, and the access token that came back with
/// it is discarded in favour of the short-lived one <c>GoogleDriveTokenService</c> mints and
/// caches per request.
/// </summary>
public sealed record GoogleOAuthConnectionResultDto(
    int SchoolId,
    bool RefreshTokenStored,
    DateTimeOffset ConnectedAtUtc);
