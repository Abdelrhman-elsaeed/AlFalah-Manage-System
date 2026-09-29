import { PagedResult } from './api-response.model';
import { StudentSummaryDto } from './student-affairs-dashboard.models';

export interface GuardianSummonDto {
  readonly id: number;
  readonly student: StudentSummaryDto;
  readonly reason: string;
  readonly priority: string;
  readonly status: string;
  readonly scheduledAt: string | null;
  readonly location: string | null;
  readonly instructions: string | null;
  readonly guardianNotifiedAt: string | null;
}

export interface GuardianEntryPermitDto {
  readonly id: number;
  readonly student: StudentSummaryDto;
  readonly reason: string;
  readonly issuedAt: string;
  readonly validFrom: string;
  readonly validUntil: string;
  readonly status: string;
  readonly classroom: { readonly id: number; readonly label: string };
  readonly acknowledgedAt: string | null;
}

export interface GuardianNotificationDto {
  readonly id: number;
  readonly studentId: number | null;
  readonly type: string;
  readonly title: string;
  readonly body: string;
  readonly priority: string;
  readonly createdAt: string;
  readonly readAt: string | null;
  readonly rowVersion: string;
}

export type GuardianSummonPage = PagedResult<GuardianSummonDto>;
export type GuardianEntryPermitPage = PagedResult<GuardianEntryPermitDto>;
export type GuardianNotificationPage = PagedResult<GuardianNotificationDto>;
