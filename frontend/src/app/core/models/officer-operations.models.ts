import { PagedResult } from './api-response.model';
import { StudentStatsDto } from './daily-operations.models';
import { ActorSummaryDto, ClassroomSummaryDto, MetricBadgeDto, StudentSummaryDto } from './student-affairs-dashboard.models';
import { ReferralDto, SummonDto } from './phase5.models';

export type ClassroomEntryPermitStatus = 'Issued' | 'AcknowledgedByTeacher' | 'Expired' | 'Revoked';

export interface ClassroomEntryPermitDto {
  readonly id: number;
  readonly student: StudentSummaryDto;
  readonly reason: string;
  readonly issuedAt: string;
  readonly validFrom: string;
  readonly validUntil: string;
  readonly timetableEntryId: number | null;
  readonly classroom: ClassroomSummaryDto;
  readonly targetTeacher: ActorSummaryDto | null;
  readonly status: ClassroomEntryPermitStatus;
  readonly acknowledgedBy: ActorSummaryDto | null;
  readonly acknowledgedAt: string | null;
  readonly repetitionMetric: MetricBadgeDto;
  readonly rowVersion: string;
}

export interface AssignableSocialWorkerDto { readonly userId: string; readonly displayName: string; }

export interface OperationalRecordDto {
  readonly id: number;
  readonly student: StudentSummaryDto;
  readonly occurredAt?: string;
  readonly arrivalAt?: string;
  readonly recognizedAt?: string;
  readonly category?: string;
  readonly recognitionType?: string;
  readonly title?: string;
  readonly description?: string;
  readonly delayMinutes?: number | null;
  readonly severity?: string;
  readonly status?: string;
}

export type StudentLookupPage = PagedResult<StudentStatsDto>;
export type EntryPermitPage = PagedResult<ClassroomEntryPermitDto>;
export type ReferralPage = PagedResult<ReferralDto>;
export type AutomationReviewPage = PagedResult<SummonDto>;
export type OperationalRecordPage = PagedResult<OperationalRecordDto>;
