/**
 * The manager-only view of a teacher's Google Drive evidence folder grant.
 * Mirrors AlFalah.Application.DTOs.TeacherDrive.DriveFolderMappingDto.
 */
export interface DriveFolderMapping {
  teacherId: number;
  schoolId: number;
  driveId: string;
  rootItemId: string;
  folderDisplayName: string;
  rootWebUrl?: string | null;
  isActive: boolean;
}

/**
 * The folder ID is the only administrator-supplied value. The server reads the folder name
 * and web link from Google Drive and activates the grant after validating the folder.
 */
export interface UpsertDriveFolderMappingRequest {
  rootItemId: string;
}
