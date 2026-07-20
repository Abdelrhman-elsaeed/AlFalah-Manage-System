export type EvidenceCellStatus = 1 | 2 | 3 | 4 | 5 | 6;

export interface AcademicYear { id: number; code: string; nameAr: string; isActive: boolean; }
export interface EvidenceTask { id: number; code: string; nameAr: string; category: string; categorySortOrder: number; sortOrder: number; }
export interface EvidenceMatrixCell { taskId: number; status: EvidenceCellStatus; isChecked: boolean; activeFilesCount: number; }
export interface EvidenceMatrixTeacherRow { teacherId: number; teacherName: string; schoolId: number; schoolName: string; completedTasksCount: number; cells: EvidenceMatrixCell[]; }
export interface EvidenceMatrix { academicYear: AcademicYear; tasks: EvidenceTask[]; rows: EvidenceMatrixTeacherRow[]; totalTasks: number; }
export interface EvidenceSubmissionFile { submissionId: number; fileName: string; fileExtension?: string; sizeInBytes: number; webUrl?: string; reviewStatus: number; isDeleted: boolean; isMissingFromDrive: boolean; uploadedAtUtc: string; reviewNote?: string; }
export interface EvidenceCellFiles { teacherId: number; taskId: number; academicYearId: number; status: EvidenceCellStatus; files: EvidenceSubmissionFile[]; }
export interface EvidenceMatrixFilter { schoolId?: number; academicYearId?: number; teacherId?: number; category?: string; completionStatus?: EvidenceCellStatus; }
