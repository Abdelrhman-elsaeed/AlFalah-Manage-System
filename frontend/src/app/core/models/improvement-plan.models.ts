export interface PlanFollowUp {
  id: number;
  improvementPlanId: number;
  followDate: string;
  progressNote: string;
  evidenceNote?: string;
  progressScore?: number;
  createdAt: string;
  createdByUserId: string;
  createdByFullName: string;
}

export interface ImprovementPlan {
  id: number;
  schoolId: number;
  schoolName: string;
  instructorId: string;
  instructorFullName: string;
  visitId: number;
  domainId?: number;
  domainNameAr?: string;
  goal: string;
  actions: string;
  startDate: string;
  endDate: string;
  successIndicators: string;
  status: 'active' | 'completed' | 'cancelled';
  createdAt: string;
  createdByUserId: string;
  createdByFullName: string;
  updatedAt: string;
  isReadOnly: boolean;
  followUps: PlanFollowUp[];
}

export interface ImprovementPlanListItem {
  id: number;
  schoolId: number;
  schoolName: string;
  instructorId: string;
  instructorFullName: string;
  visitId: number;
  domainNameAr?: string;
  goal: string;
  startDate: string;
  endDate: string;
  status: 'active' | 'completed' | 'cancelled';
  followUpsCount: number;
  latestProgressScore?: number;
}

export interface CreatePlanRequest {
  visitId: number;
  domainId?: number;
  goal: string;
  actions: string;
  startDate: string;
  endDate: string;
  successIndicators: string;
}

export interface UpdatePlanRequest {
  goal: string;
  actions: string;
  startDate: string;
  endDate: string;
  successIndicators: string;
  status: 'active' | 'completed' | 'cancelled';
}

export interface CreateFollowUpRequest {
  followDate: string;
  progressNote: string;
  evidenceNote?: string | null;
  progressScore?: number | null;
}

export interface UpdateFollowUpRequest {
  followDate: string;
  progressNote: string;
  evidenceNote?: string | null;
  progressScore?: number | null;
}

export interface ChartPoint {
  followDate: string;
  progressScore: number;
}

export interface PlanProgress {
  latestProgressScore?: number;
  latestProgressColor?: 'success' | 'warning' | 'danger';
  chartData: ChartPoint[];
}

export interface WeakDomainSuggestion {
  domainId: number;
  domainCode: string;
  domainNameAr: string;
  averageScore: number;
  prefilledGoal: string;
  prefilledActions: string;
  prefilledSuccessIndicators: string;
}
