/** Mirrors the enum names emitted by the API's JsonStringEnumConverter. */
export enum GoogleDriveCredentialType {
  ServiceAccount = 'ServiceAccount',
  OAuthRefreshToken = 'OAuthRefreshToken'
}

/**
 * What the server is willing to tell a manager about the connection. No field here carries
 * any part of the credential — `hasStoredCredential` is the only signal that one exists.
 */
export interface SchoolGoogleDriveSettings {
  schoolId: number;
  isConfigured: boolean;
  isEnabled: boolean;
  credentialType?: GoogleDriveCredentialType | null;
  schoolGoogleEmail?: string | null;
  impersonatedUserEmail?: string | null;
  oAuthClientId?: string | null;
  sharedDriveId?: string | null;
  rootFolderId?: string | null;
  rootFolderDisplayName?: string | null;
  hasStoredCredential: boolean;
  connectedAtUtc?: string | null;
}

/**
 * The Google consent URL to navigate to. `state` is echoed back only for correlation — it is
 * already inside `authorizationUrl`.
 */
export interface GoogleAuthUrl {
  authorizationUrl: string;
  state: string;
  stateExpiresAtUtc: string;
}

/**
 * Secret fields are optional on purpose: omitting one keeps whatever the server already has,
 * so renaming the root folder does not require re-pasting the service-account key.
 *
 * `oAuthRefreshToken` is intentionally absent: it is no longer entered by hand. The consent
 * flow behind `SchoolGoogleDriveService.authUrl()` is what stores it.
 */
export interface ConfigureSchoolGoogleDriveRequest {
  credentialType: GoogleDriveCredentialType;
  schoolGoogleEmail: string;
  serviceAccountJson?: string | null;
  impersonatedUserEmail?: string | null;
  oAuthClientId?: string | null;
  oAuthClientSecret?: string | null;
  sharedDriveId?: string | null;
  rootFolderId: string;
  rootFolderDisplayName: string;
  isEnabled: boolean;
}
