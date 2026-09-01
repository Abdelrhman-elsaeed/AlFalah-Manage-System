export type BehaviorSeverity = 'Low' | 'Medium' | 'High' | 'Critical';
export type NotificationDeliveryStatus = 'Pending' | 'Processing' | 'Delivered' | 'Failed' | 'Suppressed';
export type StudentTermMetricCode =
  | 'MorningArrivalDelay'
  | 'PenaltyAbsenceDay'
  | 'SessionDelay'
  | 'AcademicConcern'
  | 'CountableBehaviorIncident'
  | 'ClassroomEntryPermit';

export interface ActorSummaryDto {
  readonly userId: string;
  readonly displayName: string;
  readonly roleSnapshot: string;
}

export interface StudentSummaryDto {
  readonly id: number;
  readonly studentNumber: string;
  readonly identityNumber?: string;
  readonly displayName: string;
  readonly classroomId: number | null;
  readonly classLabel: string | null;
  readonly isActive: boolean;
  readonly photoUrl: string | null;
}

export interface AcademicTermSummaryDto {
  readonly id: number;
  readonly label: string;
  readonly startsOn: string;
  readonly endsOn: string;
  readonly isActive: boolean;
}

export interface ClassroomSummaryDto {
  readonly id: number;
  readonly label: string;
  readonly stage: string;
  readonly gradeLevel: number;
  readonly section: string;
}

export interface GuardianSummaryDto {
  readonly id: number;
  readonly displayName: string;
  readonly relationship: string;
  readonly isPrimary: boolean;
  readonly receivesNotifications: boolean;
}

export interface MetricBadgeDto {
  readonly metricCode: StudentTermMetricCode;
  readonly eligibleTermCount: number;
  readonly effectiveSettingsVersion: number;
  readonly nextThreshold: number | null;
  readonly severity: string;
  readonly lastOccurrenceAt: string | null;
  readonly recalculatedAt: string;
}

export interface StudentContextDto {
  readonly student: StudentSummaryDto;
  readonly activeTerm: AcademicTermSummaryDto | null;
  readonly classroom: ClassroomSummaryDto | null;
  readonly primaryGuardian: GuardianSummaryDto | null;
  readonly metrics: readonly MetricBadgeDto[];
}

export interface TeacherPeriodContextDto {
  readonly timetableEntryId: number;
  readonly period: number;
  readonly startsAt: string;
  readonly endsAt: string;
  readonly subject: string;
  readonly classroom: ClassroomSummaryDto;
}

export interface TeacherCurrentContextDto {
  readonly teacher: ActorSummaryDto;
  readonly schoolLocalTime: string;
  readonly schoolTimeZone: string;
  readonly timetableRevision: number;
  readonly currentPeriod: TeacherPeriodContextDto | null;
  readonly roster: readonly StudentSummaryDto[];
  readonly permittedQuickActions: readonly string[];
}

export interface TeacherTopPriorityDto {
  readonly context: TeacherCurrentContextDto;
  readonly pendingGatePassAcknowledgements: number;
  readonly pendingEntryPermitAcknowledgements: number;
  readonly alerts: readonly string[];
}

export interface DashboardCountDto {
  readonly code: string;
  readonly label: string;
  readonly count: number;
  readonly severity: string;
}

export interface TeacherStudentAffairsDashboardDto {
  readonly topPriority: TeacherTopPriorityDto;
  readonly counts: readonly DashboardCountDto[];
}

export interface NotificationDeliveryDto {
  readonly recipientLabel: string;
  readonly recipientRole: string;
  readonly status: NotificationDeliveryStatus;
  readonly deliveredAt: string | null;
  readonly readAt: string | null;
}

export interface CreateBehaviorIncidentRequestDto {
  readonly studentId: number;
  readonly schoolTimetableEntryId: number;
  readonly category: string;
  readonly severity: BehaviorSeverity;
  readonly description: string;
  readonly occurredAt: string | null;
  readonly location: string | null;
  readonly immediateAction: string | null;
}

export interface CreateAcademicConcernRequestDto {
  readonly studentId: number;
  readonly schoolTimetableEntryId: number;
  readonly category: string;
  readonly description: string;
  readonly occurredAt: string | null;
}

export interface CreateSessionDelayRequestDto {
  readonly studentId: number;
  readonly schoolTimetableEntryId: number;
  readonly occurredAt: string | null;
  readonly delayMinutes: number | null;
  readonly reason: string | null;
}

export interface CreateRecognitionRequestDto {
  readonly studentId: number;
  readonly recognitionType: string;
  readonly title: string;
  readonly description: string;
  readonly recognizedAt: string | null;
}

export interface AcademicConcernDto {
  readonly id: number;
  readonly student: StudentSummaryDto;
  readonly category: string;
  readonly description: string;
  readonly occurredAt: string;
  readonly reporter: ActorSummaryDto;
  readonly dispatchDecision: string;
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

export interface SessionDelayDto {
  readonly id: number;
  readonly student: StudentSummaryDto;
  readonly timetableEntryId: number;
  readonly period: number;
  readonly occurredAt: string;
  readonly delayMinutes: number | null;
  readonly reason: string | null;
  readonly reporter: ActorSummaryDto;
  readonly metric: MetricBadgeDto;
  readonly guardianNotification: NotificationDeliveryDto | null;
  readonly rowVersion: string;
}

export interface RecognitionDto {
  readonly id: number;
  readonly student: StudentSummaryDto;
  readonly recognitionType: string;
  readonly title: string;
  readonly description: string;
  readonly recognizedAt: string;
  readonly reporter: ActorSummaryDto;
  readonly guardianNotification: NotificationDeliveryDto | null;
  readonly rowVersion: string;
}

export interface PickupPersonDto {
  readonly name: string;
  readonly relationship: string | null;
  readonly identityHint: string | null;
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
  readonly status: string;
  readonly rowVersion: string;
}

export interface SecurityStudentAffairsDashboardDto {
  readonly approvedGatePasses: readonly SecurityGatePassQueueItemDto[];
  readonly counts: readonly DashboardCountDto[];
}

export interface GuardianStudentAffairsDashboardDto {
  readonly students: readonly StudentContextDto[];
  readonly actions: readonly DashboardCountDto[];
}

export interface GuardianStudentDto {
  readonly student: StudentSummaryDto;
  readonly canSubmitExcuses: boolean;
  readonly canRequestGatePass: boolean;
  readonly receivesNotifications: boolean;
}

export interface GuardianStudentSummaryDto {
  readonly context: StudentContextDto;
  readonly pendingSummons: number;
  readonly activeGatePasses: number;
  readonly recentRecognitions: number;
}

export interface GuardianStudentCard {
  readonly context: StudentContextDto;
  readonly canSubmitExcuses: boolean;
  readonly canRequestGatePass: boolean;
  readonly receivesNotifications: boolean;
}

export interface OfficerStudentAffairsDashboardDto {
  readonly queues: readonly DashboardCountDto[];
  readonly thresholdAlerts: readonly DashboardCountDto[];
}

export interface ClassroomAttendanceAggregateDto {
  readonly classroomId: number;
  readonly classLabel: string;
  readonly present: number;
  readonly absent: number;
  readonly absentExcused: number;
}

export interface SchoolOversightDashboardDto {
  readonly present: number;
  readonly absent: number;
  readonly absentExcused: number;
  readonly byClassroom: readonly ClassroomAttendanceAggregateDto[];
  readonly thresholdCounts: readonly DashboardCountDto[];
  readonly caseCounts: readonly DashboardCountDto[];
  readonly generatedAt: string;
}
