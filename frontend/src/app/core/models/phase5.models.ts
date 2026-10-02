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
  readonly referralReason?: string | null;
  readonly lastActivityAt?: string | null;
  readonly requiresOfficerReview?: boolean;
}

export interface ReferralTransitionDto {
  readonly fromStatus: StudentReferralStatus;
  readonly toStatus: StudentReferralStatus;
  readonly actor: ActorSummaryDto;
  readonly occurredAt: string;
  readonly reason: string | null;
}
export interface ReferralHistoryDto { readonly transitions: readonly ReferralTransitionDto[]; }

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
export interface CreateSummonRequestDto {
  readonly studentId: number;
  readonly referralId: number | null;
  readonly reason: string;
  readonly priority: ReferralPriority;
  readonly guardianProfileId: number;
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
  readonly currentMetricCount?: number | null;
  readonly observationGoals?: string | null;
  readonly observationStartDate?: string | null;
  readonly observationReviewDate?: string | null;
  readonly observationEndDate?: string | null;
  readonly observationResponsibleStaffUserId?: string | null;
  readonly observationIndicators?: readonly string[] | null;
  readonly observationNotes?: string | null;
  readonly outcomeEvidence?: string | null;
  readonly outcomeVerificationDetails?: string | null;
  readonly guardianDelivery?: NotificationDeliveryDto | null;
}

export interface TransitionDto {
  readonly fromState: string | null;
  readonly toState: string;
  readonly actor: ActorSummaryDto;
  readonly occurredAt: string;
  readonly reason: string | null;
}
export interface SummonAppointmentDto {
  readonly appointmentAt: string;
  readonly location: string;
  readonly instructions: string | null;
  readonly action: 'Scheduled' | 'Rescheduled' | 'NoShow';
  readonly actor: ActorSummaryDto;
  readonly occurredAt: string;
  readonly notes: string | null;
}
export interface SummonHistoryDto {
  readonly transitions: readonly TransitionDto[];
  readonly appointments?: readonly SummonAppointmentDto[] | null;
}
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
export interface MarkSummonNoShowRequestDto { readonly notes: string; readonly rowVersion: string; }
export interface StartSummonObservationRequestDto {
  readonly goals: string;
  readonly startDate: string;
  readonly reviewDate: string;
  readonly endDate: string | null;
  readonly responsibleStaffUserId: string;
  readonly measurableIndicators: readonly string[];
  readonly notes: string;
  readonly rowVersion: string;
}
export interface MarkSummonImprovedRequestDto {
  readonly outcomeEvidence: string;
  readonly verificationDetails: string;
  readonly rowVersion: string;
}

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
  readonly stableKey: string;
  readonly dayOfWeek: DayOfWeek;
  readonly periodSequence: number;
  readonly startsAt: string;
  readonly endsAt: string;
  readonly isEligible: boolean;
  readonly isSelected: boolean;
  readonly isConflicted: boolean;
  readonly conflictReason: string | null;
  readonly source: TeacherOfficeHourSource;
  readonly schoolTimetableId: number;
  readonly timetableRevision: number;
  readonly bellScheduleRevisionId: number;
}
export interface OfficeHoursAggregateDto {
  readonly configurationId: number | null;
  readonly instructorId: number;
  readonly academicTermId: number;
  readonly schoolTimetableId: number;
  readonly timetableRevision: number;
  readonly bellScheduleRevisionId: number;
  readonly effectiveFrom: string;
  readonly effectiveTo: string | null;
  readonly rowVersion: string;
  readonly source: TeacherOfficeHourSource;
  readonly updatedByUserId: string;
  readonly updatedAt: string;
  readonly statusReason: string | null;
  readonly slots: readonly OfficeHourSlotDto[];
}
export interface UpdateMyOfficeHoursRequestDto {
  readonly selectedSlotKeys: readonly string[];
  readonly effectiveFrom: string;
  readonly rowVersion: string;
}
export interface OverrideTeacherOfficeHoursRequestDto extends UpdateMyOfficeHoursRequestDto { readonly reason: string; }
export interface SchoolInstructorOptionDto { readonly instructorProfileId: number; readonly displayName: string; readonly subject: string; }

export interface MessagingAuditThreadDto {
  readonly threadId: number;
  readonly threadType: ConversationThreadType;
  readonly status: ConversationThreadStatus;
  readonly participantRoles: readonly string[];
  readonly createdAt: string;
  readonly lastActivityAt: string;
  readonly messageCount: number;
  readonly pendingDeliveryCount: number;
  readonly deliveredCount: number;
  readonly failedCount: number;
}
export interface MessagingAuditQuery {
  readonly threadType?: ConversationThreadType;
  readonly status?: ConversationThreadStatus;
  readonly pageNumber: number;
  readonly pageSize: number;
  readonly sortDirection?: 'asc' | 'desc';
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
  readonly referralId?: number | null;
}
export interface ConversationMessageDto {
  readonly id: number;
  readonly conversationId: number;
  readonly sender: ActorSummaryDto;
  readonly body: string;
  readonly replyToMessageId: number | null;
  readonly createdAt: string;
  readonly deliveryState: MessageDeliveryState;
  readonly disposition: OfficeHoursDisposition;
  readonly nextEligibleSendAt: string | null;
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
  readonly conversationRowVersion: string;
}
export interface GuardianTeacherOptionDto {
  readonly instructorProfileId: number;
  readonly displayName: string;
  readonly subject: string;
}
export interface GuardianStaffOptionDto {
  readonly userId: string;
  readonly displayName: string;
  readonly role: string;
  readonly threadType: ConversationThreadType;
}
export interface StudentGuardianOptionDto {
  readonly guardianProfileId: number;
  readonly displayName: string;
  readonly relationship: string;
  readonly isPrimary: boolean;
}
export interface CreateConversationRequestDto {
  readonly studentId: number;
  readonly threadType: ConversationThreadType;
  readonly targetInstructorProfileId: number | null;
  readonly targetStaffRole: string | null;
  readonly targetStaffUserId: string | null;
  readonly subject: string;
  readonly initialBody: string;
  readonly idempotencyKey: string;
  readonly referralId?: number | null;
  readonly targetGuardianProfileId?: number | null;
}
export interface MarkConversationReadRequestDto { readonly throughMessageId: number; }
export interface CloseConversationRequestDto { readonly reason: string; readonly rowVersion: string; }

export const REFERRAL_STATUSES: readonly StudentReferralStatus[] = ['Open', 'Assigned', 'InProgress', 'Resolved', 'Closed'];
export const SUMMON_STATUSES: readonly GuardianSummonStatus[] = ['Pending', 'Attended', 'UnderObservation', 'Improved'];

export function isBehaviorFact(fact: DispatchFactDto): fact is BehaviorIncidentDto {
  return 'severity' in fact;
}
