export interface SchoolMicrosoftDriveSettings {
  schoolId: number; isConfigured: boolean; isEnabled: boolean; tenantId?: string | null;
  schoolMicrosoftEmail?: string | null; driveId?: string | null; rootItemId?: string | null;
  rootFolderDisplayName?: string | null; connectedAtUtc?: string | null;
}
export interface ConfigureSchoolMicrosoftDriveRequest {
  tenantId: string; schoolMicrosoftEmail: string; driveId: string; rootItemId: string;
  rootFolderDisplayName: string; isEnabled: boolean;
}
