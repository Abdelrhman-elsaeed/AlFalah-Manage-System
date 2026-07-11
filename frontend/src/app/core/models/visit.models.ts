// Visit TypeScript models (Phase 4) — mirror AlFalah.Application.DTOs.Visits.*

export interface VisitListItem {
  id: number;
  schoolId: number;
  schoolName: string;
  instructorId: string;
  instructorFullName: string;
  createdByUserId: string;
  createdByFullName: string;
  rubricVersionId: number;
  rubricVersionNumber: number;
  visitCategory: string;          // enum int as string
  visitCategoryLabelAr: string;
  visitSequence: string;          // enum int as string
  visitSequenceLabelAr: string;
  status: string;                 // enum int as string
  statusLabelAr: string;
  visitDate: string;
  subject?: string | null;
  gradeClass?: string | null;
  createdAt: string;
  submittedAt?: string | null;
  scoredStandardsCount: number;
  totalStandardsCount: number;
}

export interface VisitScore {
  id: number;
  visitId: number;
  rubricStandardId: number;
  standardCode: string;
  standardTextAr: string;
  rubricDomainId: number;
  domainCode: string;
  domainNameAr: string;
  score: number | null;
  evidenceNote?: string | null;
}

export interface VisitDomainAverage {
  id: number;
  rubricDomainId: number;
  domainCode: string;
  domainNameAr: string;
  averageScore: number;
}

export interface VisitStrength {
  domainCode: string;
  domainNameAr: string;
  averageScore: number;
}

export interface VisitImprovement {
  domainCode: string;
  domainNameAr: string;
  averageScore: number;
}

export interface VisitPriorityStandard {
  domainCode: string;
  standardCode: string;
  standardTextAr: string;
  score: number;
}

export interface VisitAnalysis {
  id: number;
  visitId: number;
  overallScore: number;
  performanceLevelAr: string;
  strengths: VisitStrength[];
  improvementAreas: VisitImprovement[];
  priorityStandards: VisitPriorityStandard[];
  domainAverages: VisitDomainAverage[];
  computedAt: string;
}

export interface VisitDetail {
  id: number;
  schoolId: number;
  schoolName: string;
  instructorId: string;
  instructorFullName: string;
  createdByUserId: string;
  createdByFullName: string;
  rubricVersionId: number;
  rubricVersionNumber: number;
  visitCategory: string;
  visitCategoryLabelAr: string;
  visitSequence: string;
  visitSequenceLabelAr: string;
  status: string;
  statusLabelAr: string;
  visitDate: string;
  subject?: string | null;
  gradeClass?: string | null;
  notes?: string | null;
  createdAt: string;
  updatedAt: string;
  submittedAt?: string | null;
  // Phase 5 — approval / reopen workflow
  approvedByUserId?: string | null;
  approvedByFullName?: string | null;
  approvedAt?: string | null;
  rejectionReason?: string | null;
  reopenReason?: string | null;
  reopenedByUserId?: string | null;
  reopenedByFullName?: string | null;
  reopenedAt?: string | null;
  isReadOnly: boolean;
  scores: VisitScore[];
  analysis?: VisitAnalysis | null;
}

// Write DTOs

export interface CreateVisitRequest {
  instructorId: string;
  visitCategory: number;        // VisitCategory enum int
  visitSequence: number;        // VisitSequence enum int
  visitDate: string;
  subject?: string | null;
  gradeClass?: string | null;
  notes?: string | null;
  scores?: VisitScoreInput[];
}

export interface UpdateVisitRequest {
  visitCategory: number;
  visitSequence: number;
  visitDate: string;
  subject?: string | null;
  gradeClass?: string | null;
  notes?: string | null;
  scores: VisitScoreInput[];
}

export interface VisitScoreInput {
  rubricStandardId: number;
  score: number | null;
  evidenceNote?: string | null;
}

export interface VisitListQuery {
  page?: number;
  pageSize?: number;
  status?: number;
  instructorId?: string;
  visitCategory?: number;
  fromDate?: string;
  toDate?: string;
  sortBy?: string;
  sortDesc?: boolean;
}

// ─── Reference enums (frontend mirrors) ──────────────────────────────────────

export interface VisitCategoryOption {
  value: number;
  labelAr: string;
}

export interface VisitSequenceOption {
  value: number;
  labelAr: string;
}

export interface VisitStatusOption {
  value: number;
  labelAr: string;
}

// Enums — verbatim from docs/11
export const VISIT_CATEGORIES: VisitCategoryOption[] = [
  { value: 1, labelAr: 'استطلاعية / توجيهية' },
  { value: 2, labelAr: 'زيارة صفية أو دورية' },
  { value: 3, labelAr: 'زيارة تبادلية' },
  { value: 4, labelAr: 'زيارة التثبيت / الترسيم للمعلمين الجدد' },
  { value: 5, labelAr: 'زيارة المتابعة والدعم' },
  { value: 6, labelAr: 'زيارة مفاجئة / تفتيشية' },
  { value: 7, labelAr: 'زيارة طارئة' },
  { value: 8, labelAr: 'زيارة التحقق / متابعة قانونية' },
  { value: 9, labelAr: 'زيارة اللجان المركزية' }
];

export const VISIT_SEQUENCES: VisitSequenceOption[] = [
  { value: 1, labelAr: 'أولى' },
  { value: 2, labelAr: 'ثانية' },
  { value: 3, labelAr: 'ثالثة' },
  { value: 4, labelAr: 'متابعة' }
];

export const VISIT_STATUSES: VisitStatusOption[] = [
  { value: 1, labelAr: 'مسودة' },
  { value: 2, labelAr: 'مُرسلة' },
  { value: 3, labelAr: 'بانتظار الاعتماد' },
  { value: 4, labelAr: 'معتمدة' },
  { value: 5, labelAr: 'مرفوضة للتعديل' },
  { value: 6, labelAr: 'مُعاد فتحها' },
  { value: 7, labelAr: 'قيد المراجعة بعد شكوى' },
  { value: 8, labelAr: 'ملغاة' }
];

// ─── Phase 5: Approval workflow + Instructor visibility ─────────────────────

/** Body for POST /api/v1/visits/{id}/reject — reason required. */
export interface RejectVisitRequest {
  reason: string;
}

/** Body for POST /api/v1/visits/{id}/reopen — reason required. */
export interface ReopenVisitRequest {
  reason: string;
}

/** Response of GET /api/v1/visits/{id}/view-status (manager / moderator). */
export interface ReportViewStatus {
  visitId: number;
  hasBeenViewed: boolean;
  firstViewedAt?: string | null;
  lastViewedAt?: string | null;
  viewCount: number;
}

/** Response of GET /api/v1/visits/{id}/report (instructor only — approved). */
export interface InstructorReport {
  visitId: number;
  instructorId: string;
  instructorFullName: string;
  schoolId: number;
  schoolName: string;
  rubricVersionId: number;
  rubricVersionNumber: number;
  visitCategory: string;
  visitCategoryLabelAr: string;
  visitSequence: string;
  visitSequenceLabelAr: string;
  status: string;
  statusLabelAr: string;
  visitDate: string;
  subject?: string | null;
  gradeClass?: string | null;
  submittedAt?: string | null;
  approvedAt?: string | null;
  approvedByFullName?: string | null;
  scores: VisitScore[];
  analysis?: VisitAnalysis | null;
}