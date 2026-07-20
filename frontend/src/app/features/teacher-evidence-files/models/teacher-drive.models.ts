export interface TeacherDriveStatus { isMicrosoftLinked: boolean; isDriveConfigured: boolean; folderDisplayName?: string; connectionState: string; teacherDisplayName: string; }
export interface DriveItem { itemId: string; name: string; isFolder: boolean; childCount?: number; extension?: string; mimeType?: string; size?: number; lastModifiedAt?: string; lastModifiedBy?: string; webUrl?: string; eTag?: string; submissionStatus?: string; }
export interface DriveItemsPage { items: DriveItem[]; nextPageToken?: string; totalInPage: number; }
export interface DriveBreadcrumb { itemId: string; name: string; }
export interface RecentFile { itemId: string; name: string; extension?: string; size?: number; uploadedAtUtc?: string; webUrl?: string; }
export interface EvidenceTask { id: number; code: string; nameAr: string; category: string; categorySortOrder: number; sortOrder: number; }
export interface AcademicYear { id: number; code: string; nameAr: string; isActive: boolean; }
export interface EvidenceUploadCatalog { academicYear: AcademicYear; tasks: EvidenceTask[]; }
