export interface DutyTeacher { profileId: number; userId: string; name: string; isEligible?: boolean; }
export interface DutyTerm { id: number; yearId: number; year: string; semester: number; from: string; to: string; }
export interface DutyRange { from: string; to: string; reason: string; }
export interface DutyExclusion extends DutyRange { userId: string; }
export interface DutySettings {
  termId: number; from: string; to: string; weekdays: number[]; teacherIds: string[];
  exclusions: DutyExclusion[]; closures: DutyRange[]; regenerateFrom: string; regenerateTo: string;
  acceptAttendanceCalendarMismatch: boolean;
}
export interface DutyDay {
  id: number; date: string; originalTeacherId: string; originalTeacher: string; teacherId: string; teacher: string;
  revision: number; protected: boolean; locked: boolean; exceptionCode: string | null; attendance: string | null; isMine: boolean; changeKind?: string | null;
}
export interface DutyHistory { id: number; kind: string; date: string; otherDate: string | null; before: string; after: string;
  otherBefore: string | null; otherAfter: string | null; reason: string; actor: string; at: string; relatedChangeId: number | null; unresolved: boolean; beforeTeacherId: string; afterTeacherId: string; }
export interface DutyFairness { userId: string; name: string; planned: number; effective: number; covered: number; completed: number; pendingCompensation: number; }
export interface DutyPreviewDay { date: string; teacherId: string; teacher: string; before: string | null; preserved: boolean; exceptionCode: string | null; }
export interface DutyPreview { digest: string; days: DutyPreviewDay[]; fairness: DutyFairness[]; warnings: string[]; changedDates: number; }
export interface DutyWorkspace {
  canManage: boolean; school: string; today: string; userId: string; terms: DutyTerm[]; termId: number | null; planRevision: number;
  settings: DutySettings | null; teachers: DutyTeacher[]; suggestedWeekdays: number[]; days: DutyDay[]; history: DutyHistory[];
  fairness: DutyFairness[]; pendingRetries: number; closures: DutyRange[];
}
export interface DutyChange { assignmentId: number; expectedRevision: number; teacherId: string; otherAssignmentId: number | null;
  reason: string; mode: string; acceptException: boolean; resolveChangeId: number | null; requestId: string; }
