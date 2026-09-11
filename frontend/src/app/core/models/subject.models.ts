import { BellSchedule } from './bell-schedule.models';

export interface Subject { id: number; name: string; color: string; revision: number; }
export interface SubjectRoom { id: number; name: string; }
export interface SubjectClassroom { id: number; name: string; stage: number; gradeLevel: number; }
export interface SubjectRules {
  individualPeriodCount: number; pairedBlockCount: number; timePreference: 'None' | 'Early' | 'Late';
  earliestPeriodSequence: number | null; latestPreferredPeriodSequence: number | null;
  allowedDays: number[]; fixedSlots: { day: number; period: number }[]; roomIds: number[]; preferredRoomId: number | null;
}
export interface SubjectRequirement { id: number; subjectId: number; classroomId: number; classroomName: string; revision: number; totalWeeklyPeriods: number; rules: SubjectRules; }
export interface SubjectOverview { subjects: Subject[]; classrooms: SubjectClassroom[]; rooms: SubjectRoom[]; requirements: SubjectRequirement[]; schedule: BellSchedule | null; }
export interface AllocateSubject { subjectId: number; classes: { classroomId: number; revision: number }[]; rules: SubjectRules; overwriteExisting: boolean; }
export interface SubjectBulkResult { results: { classroomId: number; classroomName: string; status: 'Created' | 'Updated' | 'Skipped'; message: string | null }[]; }
