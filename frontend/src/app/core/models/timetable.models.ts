export type TimetableSemester = 1 | 2;
export type TimetableDay = 1 | 2 | 3 | 4 | 5 | 6;
export type TimetableEntryType = 1 | 2;
export type TimetablePdfColorMode = 'color' | 'monochrome';

export interface TimetableAcademicYear {
  id: number;
  code: string;
  nameAr: string;
  isActive: boolean;
}

export interface TimetableOption {
  value: number;
  labelAr: string;
}

export interface TimetableTeacher {
  instructorProfileId: number;
  userId: string;
  fullName: string;
  employeeNumber: string | null;
  subject: string | null;
  classes: string[];
  isCurrentUser: boolean;
}

export interface TimetableModerator {
  userId: string;
  fullName: string;
  isGranted: boolean;
}

export interface TimetableCapabilities {
  canManage: boolean;
  canDelegate: boolean;
  canViewVersions: boolean;
}

export interface TimetableCatalog {
  schoolId: number;
  schoolName: string;
  academicYears: TimetableAcademicYear[];
  semesters: TimetableOption[];
  days: TimetableOption[];
  periodCount: number;
  teachers: TimetableTeacher[];
  moderators: TimetableModerator[];
  capabilities: TimetableCapabilities;
}

export interface TimetableEntry {
  instructorProfileId: number;
  day: TimetableDay;
  period: number;
  entryType: TimetableEntryType;
  classLabel: string | null;
  subject: string | null;
}

export interface TimetableTeacherSummary {
  instructorProfileId: number;
  lessonCount: number;
  standbyCount: number;
}

export interface SchoolTimetable {
  id: number;
  schoolId: number;
  academicYearId: number;
  academicYearName: string;
  semester: TimetableSemester;
  semesterLabelAr: string;
  title: string;
  isPublished: boolean;
  publishedAt: string | null;
  revision: number;
  updatedAt: string;
  entries: TimetableEntry[];
  teacherSummaries: TimetableTeacherSummary[];
  capabilities: TimetableCapabilities;
}

export interface CreateTimetableRequest {
  academicYearId: number;
  semester: TimetableSemester;
  title: string;
}

export interface SaveTimetableRequest {
  title: string;
  revision: number;
  entries: TimetableEntry[];
}

export interface TimetableVersion {
  id: number;
  versionNumber: number;
  changeKind: number;
  changeKindLabelAr: string;
  title: string;
  createdAt: string;
  createdByFullName: string;
  restoredFromVersionNumber: number | null;
}

export interface TimetableImportResult {
  timetable: SchoolTimetable;
  importedEntryCount: number;
  warnings: string[];
}
