export enum ViolationSeverity { Error = 1, Warning = 2 }
export enum ViolationRuleCode {
  TeacherDoubleBooking = 1, ClassroomDoubleBooking, RoomDoubleBooking, UnavailableSlot,
  NonStudyDayPlacement, MissingAssignment, ScheduleCountMismatch, BrokenPairedBlock,
  DisallowedDay, ViolatedFixedSlot, ExcessiveConsecutive, UnfairLastPeriods, ExcessiveGaps,
  SubjectConcentration, EarlyPreferenceMissed, SubjectClusterImbalance
}
export interface ReviewOption { id: number; name: string; }
export interface ReviewTimetableOption { id: number; title: string; revision: number; isPublished: boolean; }
export interface ValidationFinding {
  id: number; ruleCode: ViolationRuleCode; ruleNameAr: string; severity: ViolationSeverity;
  messageAr: string; classroomId: number | null; classroomName: string | null;
  instructorProfileId: number | null; teacherName: string | null; subjectId: number | null;
  subjectName: string | null; day: number | null; period: number | null; isOverridden: boolean;
  overrideReason: string | null; overriddenByUserId: string | null; overriddenAt: string | null; canRepair: boolean;
}
export interface ReviewEntry {
  id: number; instructorProfileId: number; classroomId: number | null; subjectId: number | null;
  teacherName: string; classroomName: string | null; subjectName: string | null;
  day: number; period: number; roomId: number | null;
}
export interface TimetableReviewResult {
  analysisRunId: number; timetableId: number; title: string; timetableRevision: number;
  setupRevision: number | null; bellScheduleRevisionId: number | null; isPublished: boolean; completedAt: string;
  hardViolationCount: number; warningCount: number; canPublish: boolean; canManage: boolean; canOverride: boolean;
  findings: ValidationFinding[]; entries: ReviewEntry[];
  periods: { day: number; period: number; start: string; end: string }[];
  breaks: { day: number; name: string; start: string; end: string }[];
  unavailable: { instructorProfileId: number; day: number; period: number }[];
  teachers: ReviewOption[]; classrooms: ReviewOption[]; subjects: ReviewOption[];
}
export interface RepairMovement {
  entryId: number; fromDay: number; fromPeriod: number; fromTeacherId: number;
  toDay: number; toPeriod: number; toTeacherId: number;
}
export interface RepairProposal {
  id: string; analysisRunId: number; findingId: number; timetableRevision: number;
  kind: number; rank: number; descriptionAr: string; confidence: string;
  predictedErrors: number; predictedWarnings: number; movements: RepairMovement[];
}
export interface OverrideViolationRequest { reason: string; }
