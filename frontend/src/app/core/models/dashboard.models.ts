// Phase 9 — Dashboards & Exports
//
// These models mirror the server-side DashboardDtos. The shape is camelCase
// (Angular default) and uses arrays of objects (no class instances required
// from the wire — we can just use `interface` + interfaces typed as the
// shape returned by the server).
//
// Visibility scope is enforced server-side: a Moderator receives ONLY
// his-own-visits data; a School Manager receives ONLY his-school data;
// a Main Manager receives global data with NO complaint content.

export type DashboardRoleCode = 1 | 2 | 3 | 4;
export const DashboardRole = {
  MainManager: 1 as DashboardRoleCode,
  SchoolManager: 2 as DashboardRoleCode,
  Moderator: 3 as DashboardRoleCode,
  Instructor: 4 as DashboardRoleCode
};

export interface VisitStatusCount {
  status: number;
  statusLabelAr: string;
  count: number;
}

export interface SchoolComparisonRow {
  schoolId: number;
  schoolName: string;
  city: string;
  locationDetails: string | null;
  schoolLocationId: number | null;
  schoolLocationName: string | null;
  regionName: string | null;
  latitude: number | null;
  longitude: number | null;
  visitsCount: number;
  approvedVisitsCount: number;
  averageOverallScore: number | null;
  performanceLevelAr: string | null;
  instructorsCount: number;
  moderatorsCount: number;
}

export interface SubjectPerformanceRow {
  subject: string;
  visitsCount: number;
  approvedVisitsCount: number;
  averageOverallScore: number | null;
}

export interface ModeratorPerformanceRow {
  moderatorUserId: string;
  moderatorFullName: string;
  visitsCount: number;
  approvedVisitsCount: number;
  pendingApprovalCount: number;
  averageOverallScore: number | null;
  openImprovementPlansCount: number;
}

export interface InstructorPerformanceRow {
  instructorUserId: string;
  instructorFullName: string;
  approvedVisitsCount: number;
  averageOverallScore: number | null;
  latestPerformanceLevelAr: string | null;
  openImprovementPlansCount: number;
  needsImprovement: boolean;
  /**
   * The weakest domain of the latest approved visit. The needs-improvement list
   * is selected on this, not on the overall average, so the UI names it — the
   * table otherwise showed teachers rated "جيد جداً" under a heading saying
   * they need support, with nothing on the row explaining why.
   */
  weakestDomainNameAr: string | null;
  weakestDomainScore: number | null;
}

export interface ImprovementPlanAnalytics {
  totalActive: number;
  totalCompleted: number;
  totalCancelled: number;
  totalFollowUps: number;
  plansWithAtLeastOneFollowUp: number;
  averageLatestProgressScore: number | null;
}

export interface DashboardFilterEcho {
  academicYear: number | null;
  semester: string | null;
  schoolId: number | null;
  schoolName: string | null;
  subject: string | null;
  stage: string | null;
  moderatorUserId: string | null;
  moderatorFullName: string | null;
}

// ─── 1) Main Manager dashboard ────────────────────────────────────────────

export interface MainManagerDashboard {
  schoolsCount: number;
  activeSchoolsCount: number;
  schoolManagersCount: number;
  moderatorsCount: number;
  instructorsCount: number;
  visitsCount: number;
  approvedEvaluationsCount: number;
  visitsByStatus: VisitStatusCount[];
  averageOverallScore: number | null;
  averagePerformanceLevelAr: string | null;
  schoolComparison: SchoolComparisonRow[];
  improvementPlans: ImprovementPlanAnalytics;
  appliedFilters: DashboardFilterEcho;
}

// ─── 2) School Manager dashboard ──────────────────────────────────────────

export interface SchoolManagerDashboard {
  schoolId: number;
  schoolName: string;
  instructorsCount: number;
  moderatorsCount: number;
  visitsThisMonthCount: number;
  instructorsNeedingImprovementCount: number;
  evaluationsPendingApprovalCount: number;
  complaintsCount: number;
  openComplaintsCount: number;
  visitsByStatus: VisitStatusCount[];
  subjectPerformance: SubjectPerformanceRow[];
  moderatorPerformance: ModeratorPerformanceRow[];
  instructorsNeedingImprovement: InstructorPerformanceRow[];
  improvementPlans: ImprovementPlanAnalytics;
  appliedFilters: DashboardFilterEcho;
}

// ─── 3) Moderator dashboard ───────────────────────────────────────────────

export interface ModeratorDashboard {
  moderatorUserId: string;
  moderatorFullName: string;
  schoolId: number;
  schoolName: string;
  todaysVisitsCount: number;
  draftVisitsCount: number;
  openImprovementPlansCount: number;
  evaluationsPendingApprovalCount: number;
  averageOverallScore: number | null;
  instructorsEvaluatedCount: number;
  approvedVisitsCount: number;
  topInstructors: InstructorPerformanceRow[];
  visitsByStatus: VisitStatusCount[];
  appliedFilters: DashboardFilterEcho;
}

// ─── 4) Instructor dashboard ──────────────────────────────────────────────

export interface LatestEvaluation {
  visitId: number;
  visitDate: string;
  visitCategoryLabelAr: string;
  moderatorFullName: string;
  overallScore: number;
  performanceLevelAr: string;
  isApproved: boolean;
}

export interface PerformanceTrendPoint {
  visitId: number;
  visitDate: string;
  overallScore: number;
  performanceLevelAr: string;
}

export interface InstructorDashboard {
  instructorUserId: string;
  instructorFullName: string;
  schoolId: number;
  schoolName: string;
  latestEvaluation: LatestEvaluation | null;
  performanceTrend: PerformanceTrendPoint[];
  strengths: string[];
  improvementPoints: string[];
  openImprovementPlansCount: number;
  improvementPlansWithFollowUpsCount: number;
  totalFollowUpsCount: number;
  latestFollowUpsCount: number;
  reportViewedCount: number;
  firstReportViewedAt: string | null;
  lastReportViewedAt: string | null;
  approvedVisitsCount: number;
  appliedFilters: DashboardFilterEcho;
}

// ─── Filters ──────────────────────────────────────────────────────────────

export interface DashboardFilter {
  academicYear?: number | null;
  semester?: string | null;
  schoolId?: number | null;
  subject?: string | null;
  stage?: string | null;
  moderatorUserId?: string | null;
  fromDate?: string | null;
  toDate?: string | null;
}
