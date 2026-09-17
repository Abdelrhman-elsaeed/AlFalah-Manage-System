import { TimetableDay } from './timetable.models';

export type SwapSearchScope = 'SameDay' | 'WholeTimetable';

export interface SwapLesson {
  entryId: number; teacherId: number; teacherName: string; classroom: string; subject: string;
  periods: number[]; entryIds: number[]; start: string; end: string; roomId: number | null;
}
export interface SwapPreview {
  entryId: number; teacherName: string; classroom: string; subject: string; fromPeriod: number;
  toPeriod: number; fromTime: string; toTime: string; toTeacherName: string; roomId: number | null;
  fromDay: TimetableDay; toDay: TimetableDay;
}
export interface SwapCandidate {
  id: string; kind: string; color: 'Green' | 'Yellow' | 'Red'; label: string;
  errors: string[]; warnings: string[]; preview: SwapPreview[];
}
export interface SwapCandidates {
  timetableId: number; revision: number; date: string; sourceEntryId: number; mode: string;
  expiresAt: string; canOverride: boolean; candidates: SwapCandidate[];
}
export interface InlineCandidateCell {
  anchorEntryId: number; entryIds: number[]; teacherId: number; day: TimetableDay; period: number;
  color: 'Green' | 'Yellow' | 'Red'; directProposalId: string | null;
  alternativeProposalIds: string[]; reasonSummary: string | null;
}
export interface InlineSwapCandidates {
  timetableId: number; revision: number; date: string; sourceEntryId: number; sourceEntryIds: number[];
  scope: SwapSearchScope; expiresAt: string; canOverride: boolean;
  cells: InlineCandidateCell[]; proposals: SwapCandidate[];
}
export interface SubstitutionHistory {
  id: number; kind: string; date: string; beforeRevision: number; afterRevision: number;
  requestedBy: string; approvedBy: string; reason: string | null; confirmedAt: string;
}
export interface DailySubstitution {
  timetableId: number; title: string; revision: number; isPublished: boolean; canManage: boolean;
  canOverride: boolean; lessons: SwapLesson[]; history: SubstitutionHistory[];
}
