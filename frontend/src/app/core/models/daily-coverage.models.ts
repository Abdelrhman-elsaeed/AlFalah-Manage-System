export interface CoverageNeed {
  id: number; revision: number; timetableId: number; sourceEntryId: number; date: string; originalTeacher: string;
  classroom: string; subject: string; periods: number[]; startsAt: string; endsAt: string; timeZoneId: string;
  state: string; isRequired: boolean; substitutionId: number | null; assignedTeacherId: number | null;
  assignedTeacher: string | null; reviewReason: string | null; acknowledgedAt: string | null; submittedAt: string | null;
  narrative: string; notes: string; correctionReason: string | null;
}
export interface CoverageTeacher { id: number; name: string; executedPeriods: number; pendingPeriods: number; }
export interface CoverageOption { teacherId: number; name: string; color: string; errors: string[]; warnings: string[]; executedPeriods: number; pendingPeriods: number; }
export interface CoverageBoard { date: string; timetableId: number | null; timetableRevision: number | null; canManage: boolean; canOverride: boolean; issue: string | null; now: string; needs: CoverageNeed[]; teachers: CoverageTeacher[]; }
export interface CoverageChoice { needId: number; teacherId: number; }
export interface CoveragePreviewRequest { date: string; mode: 'Manual' | 'Fair'; choices: CoverageChoice[]; expiresAt: string; }
export interface CoverageProposalItem { needId: number; revision: number; teacherId: number; teacherName: string; color: string; warnings: string[]; }
export interface CoveragePreview { proposalId: string; expiresAt: string; mode: string; assignments: CoverageProposalItem[]; uncoveredNeedIds: number[]; }
export interface CoverageApproval { requestId: string; preview: CoveragePreviewRequest; proposalId: string; reason: string | null; }
export interface CoverageAction { requestId: string; revision: number; substitutionId: number; reason?: string; }
export interface CoverageRecord { id: number; kind: string; actor: string; at: string; narrative: string; notes: string; reason: string | null; snapshotHash: string; signatureHash: string | null; signatureData: string | null; }
export interface CoverageTask { need: CoverageNeed; canAcknowledge: boolean; canExecute: boolean; canEditDraft: boolean; blockedReason: string | null; accountSignature: string | null; history: CoverageRecord[]; }
