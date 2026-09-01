import {
  ActorSummaryDto,
  BehaviorSeverity,
  GuardianSummaryDto,
  MetricBadgeDto,
  NotificationDeliveryDto,
  StudentSummaryDto
} from './student-affairs-dashboard.models';

export type ReferralPriority = 'Normal' | 'High' | 'Critical';
export type StudentReferralStatus = 'Open' | 'Assigned' | 'InProgress' | 'Resolved' | 'Closed';
export type ReferralSourceType =
  | 'MorningDelay'
  | 'SessionDelay'
  | 'AcademicConcern'
  | 'Behavior'
  | 'Absence'
  | 'RepeatedEntryPermit'
  | 'Manual';
export type StudentCaseActionType =
  | 'CounselingSession'
  | 'GuardianSummon'
  | 'GradeDeductionRecommendation'
  | 'SuspensionRecommendation'
  | 'ChildRightsCommitteeReferral'
  | 'Other';
export type GuardianSummonStatus = 'Pending' | 'Attended' | 'UnderObservation' | 'Improved';
export type GuardianDispatchDecision = 'PendingOfficerDecision' | 'Approved' | 'Suppressed';
export type ConversationThreadType = 'GuardianTeacher' | 'GuardianStudentAffairs' | 'GuardianSocialWorker';
export type ConversationThreadStatus = 'Open' | 'Closed' | 'Archived';
export type MessageDeliveryState = 'Pending' | 'Delivered' | 'Failed';
export type OfficeHoursDisposition = 'SentImmediately' | 'QueuedUntilOfficeHours' | 'BypassedForUrgency';
export type TeacherOfficeHourSource = 'DerivedFromPublishedTimetable' | 'TeacherSelected' | 'ManagerOverride';
export type DayOfWeek = 'Sunday' | 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday';

export interface ReferralListQuery {
  readonly status?: StudentReferralStatus;
  readonly priority?: ReferralPriority;
  readonly studentId?: number;
  readonly assignedWorkerUserId?: string;
  readonly isAssigned?: boolean;
  readonly pageNumber: number;
  readonly pageSize: number;
  readonly search?: string;
  readonly sortDirection?: 'asc' | 'desc';
}

export interface ReferralSourceSnapshotDto {
  readonly sourceType: ReferralSourceType;
  readonly sourceEntityId: number | null;
  readonly countSnapshot: number | null;
  readonly thresholdSnapshot: number | null;
}

export interface StudentCaseActionDto {
  readonly id: number;
  readonly actionType: StudentCaseActionType;
  readonly description: string;
  readonly actor: ActorSummaryDto;
  readonly actionAt: string;
  readonly result: string | null;
}

export interface ReferralDto {
  readonly id: number;
  readonly student: StudentSummaryDto;
  readonly sourceSnapshot: ReferralSourceSnapshotDto;
  readonly currentMetric: MetricBadgeDto | null;
  readonly priority: ReferralPriority;
  readonly status: StudentReferralStatus;
  readonly assignedSocialWorker: ActorSummaryDto | null;
  readonly actions: readonly StudentCaseActionDto[];
  readonly resolutionNotes: string | null;
  readonly createdAt: string;
  readonly rowVersion: string;
}

export interface AcceptReferralRequestDto { readonly rowVersion: string; }
export interface AddReferralActionRequestDto {
  readonly actionType: StudentCaseActionType;
  readonly description: string;
  readonly actionAt: string | null;
  readonly result: string | null;
  readonly rowVersion: string;
}
export interface ResolveReferralRequestDto { readonly resolutionNote: string; readonly rowVersion: string; }
export interface ReopenReferralRequestDto { readonly reason: string; readonly rowVersion: string; }

export interface SummonListQuery {
  readonly status?: GuardianSummonStatus;
  readonly priority?: ReferralPriority;
  readonly appointmentDate?: string;
  readonly assignedWorkerUserId?: string;
  readonly studentId?: number;
  readonly pageNumber: number;
  readonly pageSize: number;
  readonly search?: string;
  readonly sortDirection?: 'asc' | 'desc';
}

export interface SummonDto {
  readonly id: number;
  readonly student: StudentSummaryDto;
  readonly referralId: number | null;
  readonly createdReason: string;
  readonly priority: ReferralPriority;
  readonly sourceCountSnapshot: number | null;
  readonly thresholdSnapshot: number | null;
  readonly status: GuardianSummonStatus;
  readonly scheduledAt: string | null;
  readonly location: string | null;
  readonly instructions: string | null;
  readonly guardian: GuardianSummaryDto;
  readonly assignedSocialWorker: ActorSummaryDto | null;
  readonly requiresOfficerReview: boolean;
  readonly officerReviewReason: string | null;
  readonly guardianNotifiedAt: string | null;
  readonly rowVersion: string;
}

export interface TransitionDto {
  readonly fromState: string | null;
  readonly toState: string;
  readonly actor: ActorSummaryDto;
  readonly occurredAt: string;
  readonly reason: string | null;
}
export interface SummonHistoryDto { readonly transitions: readonly TransitionDto[]; }
export interface StudentGuardianLinkDto {
  readonly id: number;
  readonly guardian: GuardianSummaryDto;
  readonly canSubmitExcuses: boolean;
  readonly canRequestGatePass: boolean;
  readonly validFrom: string;
  readonly validTo: string | null;
  readonly isActive: boolean;
  readonly rowVersion: string;
}
export interface ScheduleSummonRequestDto {
  readonly appointmentAt: string;
  readonly location: string;
  readonly instructions: string | null;
  readonly guardianProfileId: number;
  readonly rowVersion: string;
}
export interface AttendSummonRequestDto { readonly attendanceNotes: string; readonly rowVersion: string; }
export interface StartSummonObservationRequestDto { readonly observationPlan: string; readonly rowVersion: string; }
export interface MarkSummonImprovedRequestDto { readonly outcomeEvidence: string; readonly rowVersion: string; }

export interface PendingDispatchDto {
  readonly id: number;
  readonly studentId: number;
  readonly factType: string;
  readonly factId: number;
  readonly summary: string;
  readonly queuedAt: string;
  readonly rowVersion: string;
}
export interface ApproveNotificationRequestDto { readonly rowVersion: string; }
export interface SuppressNotificationRequestDto { readonly reason: string; readonly rowVersion: string; }

export interface AcademicConcernDto {
  readonly id: number;
  readonly student: StudentSummaryDto;
  readonly category: string;
  readonly description: string;
  readonly occurredAt: string;
  readonly reporter: ActorSummaryDto;
  readonly dispatchDecision: GuardianDispatchDecision;
  readonly metric: MetricBadgeDto;
  readonly referralId: number | null;
  readonly rowVersion: string;
}
export interface BehaviorIncidentDto extends AcademicConcernDto {
  readonly severity: BehaviorSeverity;
  readonly location: string | null;
  readonly immediateAction: string | null;
  readonly queuedActions: readonly string[];
}
export type DispatchFactDto = AcademicConcernDto | BehaviorIncidentDto;

export interface OfficeHourSlotDto {
  readonly id: number;
  readonly dayOfWeek: DayOfWeek;
  readonly startsAt: string;
  readonly endsAt: string;
  readonly effectiveFrom: string;
  readonly effectiveTo: string | null;
  readonly source: TeacherOfficeHourSource;
  readonly isEligible: boolean;
  readonly rowVersion: string;
}
export interface UpdateMyOfficeHoursRequestDto {
  readonly eligibleSlotIds: readonly number[];
  readonly effectiveFrom: string;
  readonly rowVersion: string;
}

export interface ConversationParticipantDto { readonly userId: string; readonly displayName: string; readonly role: string; }
export interface ConversationDto {
  readonly id: number;
  readonly student: StudentSummaryDto;
  readonly subject: string;
  readonly threadType: ConversationThreadType;
  readonly status: ConversationThreadStatus;
  readonly participants: readonly ConversationParticipantDto[];
  readonly unreadCount: number;
  readonly updatedAt: string;
  readonly rowVersion: string;
}
export interface ConversationMessageDto {
  readonly id: number;
  readonly conversationId: number;
  readonly sender: ActorSummaryDto;
  readonly body: string;
  readonly replyToMessageId: number | null;
  readonly createdAt: string;
  readonly deliveryState: MessageDeliveryState;
  readonly receipts: readonly NotificationDeliveryDto[];
}
export interface SendMessageRequestDto {
  readonly body: string;
  readonly replyToMessageId: number | null;
  readonly idempotencyKey: string;
}
export interface SendMessageResultDto {
  readonly message: ConversationMessageDto;
  readonly disposition: OfficeHoursDisposition;
  readonly nextEligibleSendAt: string | null;
}
export interface MarkConversationReadRequestDto { readonly throughMessageId: number; }
export interface CloseConversationRequestDto { readonly reason: string; readonly rowVersion: string; }

export const REFERRAL_STATUSES: readonly StudentReferralStatus[] = ['Open', 'Assigned', 'InProgress', 'Resolved', 'Closed'];
export const SUMMON_STATUSES: readonly GuardianSummonStatus[] = ['Pending', 'Attended', 'UnderObservation', 'Improved'];

export function consistentOfficeHoursRowVersion(slots: readonly OfficeHourSlotDto[]): string | null {
  if (slots.length === 0) return null;
  const tokens = new Set(slots.map(slot => slot.rowVersion).filter(token => token.trim().length > 0));
  return tokens.size === 1 ? [...tokens][0] : null;
}

export function isBehaviorFact(fact: DispatchFactDto): fact is BehaviorIncidentDto {
  return 'severity' in fact;
}
