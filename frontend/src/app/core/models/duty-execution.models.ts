export interface DutyStudent { studentId: number; enrollmentId: number; classroomId: number; name: string; classroom: string; grade: number; studentNumber: string; }
export interface DutyClass { id: number; label: string; grade: number; count: number; }
export interface DutyPage<T> { items: T[]; page: number; pageSize: number; totalCount: number; }
export interface DutyRoster { students: DutyPage<DutyStudent>; classes: DutyClass[]; }
export interface DutyReportLine { studentId: number; enrollmentId: number; time: string; action: string; notes: string; studentName?: string; classroom?: string; }
export interface DutyReportContent { noLateStudents: boolean; noViolations: boolean; violations: DutyReportLine[]; observations: string; lastDeparture: string | null; dutyNotes?: string; substituteNotes?: string; }
export interface DutyDraft { id: number; version: number; assignmentRevision: number; authorUserId: string; content: DutyReportContent; savedAt: string; submittedAt: string | null; correctionReason: string | null; }
export interface DutyNoticeStatus { revision: number; state: string; recipients: number; dashboardDelivered: number; messagesDelivered: number; dashboardRead: number; messagesRead: number; }
export interface DutyLate { id: number; revision: number; studentId: number; enrollmentId: number; classroomId: number; studentName: string; classroom: string; date: string; observedAt: string; dismissalAt: string; departureAt: string | null; action: string; notes: string; isRetracted: boolean; notices: DutyNoticeStatus[]; }
export interface DutyDocument { id: number; kind: string; assignmentRevision: number; authorUserId: string; signedAt: string; signatureHash: string; previousDocumentId: number | null; correctionReason: string | null; }
export interface DutyExecution { assignmentId: number; assignmentRevision: number; date: string; canExecute: boolean; canManage: boolean;
  header: { school: string; city: string; header: string | null; footer: string | null; logoUrl: string | null; manager: string; year: string; semester: number; originalTeacher: string; actualTeacher: string; isSubstitute: boolean };
  dismissalAt: string | null; timingException: string | null; signatureData: string | null; acknowledged: boolean; draft: DutyDraft | null; lateStudents: DutyLate[]; documents: DutyDocument[]; priorDrafts: DutyDraft[]; }
export interface DutyDismissalStatistics { uniqueStudents: number; incidents: number; items: DutyPage<DutyLate>; }
export interface DutyGuardianNotice { notificationId: number; factId: number; revision: number; studentName: string; date: string; content: string; messageRoute: string | null; isRead: boolean; at: string; }
