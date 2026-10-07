export interface StorageContext {
  schoolId: number; schoolName: string; academicYearId?: number; academicYearName?: string;
  canManage: boolean; canReviewEvidence?: boolean; isTeacher: boolean; connectionState: string; rootFolderId?: number;
}
export interface StorageAccess { canManage: boolean; canReviewEvidence: boolean; }
export interface StorageFolder { id: number; parentFolderId?: number; displayName: string; kind: string; rowVersion: string; }
export interface StorageFile {
  storedFileId: number; folderId: number; displayName: string; size: number; mimeType?: string;
  uploadedAt: string; state: string; isProtected: boolean; rowVersion: string;
}
export interface StoragePage<T> { items: T[]; total: number; page: number; pageSize: number; }
export interface StorageDetails {
  file: StorageFile;
  versions: { versionId: number; versionNumber: number; size: number; mimeType: string; uploadedAt: string; availability: string }[];
}
export interface StorageUpload {
  operationId: number; storedFileId?: number; versionId?: number; status: string;
  displayName: string; size: number; mimeType: string; uploadedAt: string; errorCode?: string;
}
export interface StorageDiscovery {
  items: { folderId?: number; storedFileId?: number; displayName: string; isFolder: boolean; size?: number; mimeType?: string; state: string }[];
  nextPageToken?: string;
}
