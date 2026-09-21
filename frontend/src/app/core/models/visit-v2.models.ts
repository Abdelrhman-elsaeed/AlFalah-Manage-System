import { PagedResult } from './api-response.model';

export interface VisitV2Availability { isEnabled: boolean; }
export interface VisitV2ScoreLabel { score: number; labelAr: string; }
export interface VisitV2Indicator { id: number; code: string; textAr: string; sortOrder: number; isObserved: boolean; }
export interface VisitV2Standard {
  id: number; code: string; textAr: string; sortOrder: number; score: number;
  evidenceNote?: string | null; indicators: VisitV2Indicator[];
}
export interface VisitV2Domain {
  id: number; code: string; nameAr: string; sortOrder: number; standards: VisitV2Standard[];
}
export interface VisitV2ObservationCard {
  rubricVersionId: number; rubricVersionNumber: number; domains: VisitV2Domain[]; scoreLabels: VisitV2ScoreLabel[];
}
export interface CreateVisitV2Request {
  instructorId: string; visitCategory: number; visitSequence: number; visitDate: string;
  classroomPeriod: number; subject: string; gradeClass: string; lessonTitle: string;
  presentCount: number; absentCount: number; notes?: string | null;
}
export interface VisitV2ScoreInput {
  rubricStandardId: number; score: number; evidenceNote?: string | null; observedIndicatorIds: number[];
}
export interface UpdateVisitV2Request extends Omit<CreateVisitV2Request, 'instructorId'> { scores: VisitV2ScoreInput[]; }
export interface VisitV2DomainAnalysis {
  rubricDomainId: number; domainCode: string; domainNameAr: string; sum: number; maximumScore: number;
  percentage: number; levelAr: string; isStrength: boolean; isImprovementArea: boolean;
}
export interface VisitV2Analysis {
  totalScore: number; maximumScore: number; overallPercentage: number; performanceLevelAr: string;
  domains: VisitV2DomainAnalysis[]; strengths: string[]; improvementAreas: string[]; computedAt: string;
}
export interface VisitV2Treatment {
  id: number; rubricDomainId?: number | null; domainNameAr: string; goal: string; actions: string;
  successIndicators: string; source: number; sortOrder: number;
}
export interface VisitV2Detail {
  id: number; schoolId: number; schoolName: string; instructorId: string; instructorName: string;
  evaluatorName: string; evaluatorRole: string; visitCategory: number; visitCategoryLabelAr: string;
  visitSequence: number; visitSequenceLabelAr: string; status: number; statusLabelAr: string;
  visitDate: string; classroomPeriod: number; subject: string; gradeClass: string; lessonTitle: string;
  presentCount: number; absentCount: number; notes?: string | null; rubricVersionId: number;
  domains: VisitV2Domain[]; analysis?: VisitV2Analysis | null; treatments: VisitV2Treatment[];
  createdAt: string; updatedAt: string; submittedAt?: string | null; approvedAt?: string | null;
  rejectionReason?: string | null; reopenReason?: string | null; isReadOnly: boolean;
}
export interface VisitV2ArchiveQuery {
  page?: number; pageSize?: number; search?: string; evaluatorUserId?: string; status?: number;
  visitCategory?: number; fromDate?: string; toDate?: string;
}
export interface VisitV2ArchiveItem {
  id: number; visitDate: string; classroomPeriod: number; instructorName: string; employeeNumber?: string | null;
  subject: string; gradeClass: string; lessonTitle: string; visitCategoryLabelAr: string;
  visitSequenceLabelAr: string; evaluatorName: string; evaluatorRole: string; status: number;
  statusLabelAr: string; overallPercentage?: number | null; performanceLevelAr?: string | null;
}
export interface VisitV2EvaluatorFilter { userId: string; displayName: string; }
export interface VisitV2ArchiveResult { page: PagedResult<VisitV2ArchiveItem>; evaluators: VisitV2EvaluatorFilter[]; }
export interface VisitV2Aggregate { code: string; nameAr?: string; textAr?: string; averagePercentage: number; visitCount?: number; }
export interface VisitV2EvaluatorWorkload { userId: string; displayName: string; visitCount: number; averagePercentage: number; }
export interface VisitV2Dashboard {
  totalVisits: number; averageOverallPercentage: number; highAchievementRate: number;
  uniqueVisitedTeachers: number; totalActiveTeachers: number; evaluators: VisitV2EvaluatorWorkload[]; domains: VisitV2Aggregate[];
  topStandards: VisitV2Aggregate[]; bottomStandards: VisitV2Aggregate[];
}
