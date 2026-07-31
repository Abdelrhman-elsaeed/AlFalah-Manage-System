/** Mirrors AlFalah.Domain.Enums.GoogleDriveCredentialType. */
export enum GoogleDriveCredentialType {
  ServiceAccount = 1,
  OAuthRefreshToken = 2
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
 * Secret fields are optional on purpose: omitting one keeps whatever the server already has,
 * so renaming the root folder does not require re-pasting the service-account key.
 */
export interface ConfigureSchoolGoogleDriveRequest {
  credentialType: GoogleDriveCredentialType;
  schoolGoogleEmail: string;
  serviceAccountJson?: string | null;
  impersonatedUserEmail?: string | null;
  oAuthClientId?: string | null;
  oAuthClientSecret?: string | null;
  oAuthRefreshToken?: string | null;
  sharedDriveId?: string | null;
  rootFolderId: string;
  rootFolderDisplayName: string;
  isEnabled: boolean;
}
