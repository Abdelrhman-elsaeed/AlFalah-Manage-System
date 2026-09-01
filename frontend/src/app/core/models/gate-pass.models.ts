import { PagedResult } from './api-response.model';
import {
  ActorSummaryDto,
  ClassroomSummaryDto,
  NotificationDeliveryDto,
  PickupPersonDto,
  StudentSummaryDto
} from './student-affairs-dashboard.models';

export type GatePassStatus =
  | 'Requested'
  | 'Approved'
  | 'Rejected'
  | 'SecurityAcknowledged'
  | 'Exited'
  | 'Cancelled'
  | 'Expired';

export type PickupVerificationMethod = 'Visual' | 'Manual' | 'GuardianScreenshot';

export interface GatePassDto {
  readonly id: number;
  readonly student: StudentSummaryDto;
  readonly requestedAt: string;
  readonly requestedExitAt: string;
  readonly reason: string;
  readonly pickupPerson: PickupPersonDto;
  readonly status: GatePassStatus;
  readonly approvedWindowStartsAt: string | null;
  readonly approvedWindowEndsAt: string | null;
  readonly reviewedAt: string | null;
  readonly exitedAt: string | null;
  readonly currentClassroom: ClassroomSummaryDto | null;
  readonly currentTeacher: ActorSummaryDto | null;
  readonly notifications: readonly NotificationDeliveryDto[];
  /** Opaque Base64 concurrency token. Never decode or manufacture it. */
  readonly rowVersion: string;
}

export interface SecurityGatePassQueueItemDto {
  readonly id: number;
  readonly student: StudentSummaryDto;
  readonly classLabel: string;
  readonly approvedWindowStartsAt: string;
  readonly approvedWindowEndsAt: string;
  readonly pickupPerson: PickupPersonDto;
  readonly officerName: string;
  readonly approvedAt: string;
  readonly status: Extract<GatePassStatus, 'Approved' | 'SecurityAcknowledged'>;
  readonly rowVersion: string;
}

export interface GatePassListQuery {
  readonly status?: GatePassStatus;
  readonly date?: string;
  readonly classroomId?: number;
  readonly pageNumber: number;
  readonly pageSize: number;
  readonly sortBy?: string;
  readonly sortDirection?: 'asc' | 'desc';
}

export interface CreateGatePassRequestDto {
  readonly studentId: number;
  readonly desiredExitTime: string;
  readonly reason: string;
  readonly pickupPersonName: string;
  readonly pickupRelationship: string | null;
  readonly pickupIdentityHint: string | null;
}

export interface ApproveGatePassRequestDto {
  readonly windowStartsAt: string;
  readonly windowEndsAt: string;
  readonly approvalNote: string | null;
  readonly rowVersion: string;
}

export interface RejectGatePassRequestDto {
  readonly reason: string;
  readonly rowVersion: string;
}

export interface CancelGatePassRequestDto extends RejectGatePassRequestDto {}

export interface AcknowledgeGatePassRequestDto {
  readonly rowVersion: string;
}

export interface ExecuteGatePassRequestDto {
  readonly exitedAt: null;
  readonly verificationMethod: PickupVerificationMethod;
  readonly verificationNote: string;
  readonly gateNote: string | null;
  readonly rowVersion: string;
}

export interface TransitionDto {
  readonly fromState: string | null;
  readonly toState: string;
  readonly actor: ActorSummaryDto;
  readonly occurredAt: string;
  readonly reason: string | null;
}

export interface GatePassHistoryDto {
  readonly transitions: readonly TransitionDto[];
  readonly deliveries: readonly NotificationDeliveryDto[];
}

export type GatePassPage = PagedResult<GatePassDto>;
export type SecurityGatePassPage = PagedResult<SecurityGatePassQueueItemDto>;

export const ACTIVE_GATE_PASS_STATUSES: readonly GatePassStatus[] = [
  'Requested',
  'Approved',
  'SecurityAcknowledged'
];

export function gatePassStatusLabel(status: GatePassStatus): string {
  const labels: Record<GatePassStatus, string> = {
    Requested: 'بانتظار المراجعة',
    Approved: 'معتمد',
    Rejected: 'مرفوض',
    SecurityAcknowledged: 'تمت المطابقة — بانتظار الخروج',
    Exited: 'تم الخروج',
    Cancelled: 'ملغي',
    Expired: 'منتهي'
  };
  return labels[status];
}
