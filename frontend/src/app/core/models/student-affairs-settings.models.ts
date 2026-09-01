export interface ActorSummaryDto {
  readonly userId: string;
  readonly displayName: string;
  readonly roleSnapshot: string;
}

export interface StudentAffairsSettingsValues {
  readonly morningDelayThresholdPerTerm: number;
  readonly behaviorIncidentMultiplePerTerm: number;
  readonly academicConcernThresholdPerTerm: number;
  readonly classroomEntryPermitThresholdPerTerm: number;
  readonly absenceVisualAlertThresholdPerTerm: number;
  readonly absenceReferralThresholdPerTerm: number;
  readonly absenceChildRightsThresholdPerTerm: number;
  readonly behaviorCountabilityPolicy: string;
  /** Backend TimeOnly wire format: HH:mm:ss. */
  readonly arrivalCutoffLocalTime: string;
  readonly arrivalGraceMinutes: number;
}

export interface CreateStudentAffairsSettingsRequestDto extends StudentAffairsSettingsValues {}

export interface UpdateStudentAffairsSettingsRequestDto extends StudentAffairsSettingsValues {
  readonly auditReason: string;
  readonly rowVersion: string;
}

export interface ResetStudentAffairsSettingsRequestDto {
  readonly reason: string;
  readonly rowVersion: string;
}

export interface SchoolStudentAffairsSettingsDto extends StudentAffairsSettingsValues {
  readonly id: number | null;
  readonly effectiveVersion: number;
  /** Backend DateTimeOffset wire format: ISO-8601 with offset. */
  readonly effectiveFrom: string;
  readonly usesLockedDefaults: boolean;
  readonly rowVersion: string;
}

export interface StudentAffairsSettingsHistoryDto {
  readonly version: number;
  readonly settings: SchoolStudentAffairsSettingsDto;
  readonly actor: ActorSummaryDto;
  readonly reason: string;
  readonly effectiveFrom: string;
}

export interface StudentAffairsPageQuery {
  readonly pageNumber: number;
  readonly pageSize: number;
}

export const STUDENT_AFFAIRS_ROLES = {
  officer: 'StudentAffairsOfficer',
  schoolManager: 'SchoolManager'
} as const;

export const STUDENT_AFFAIRS_PERMISSIONS = {
  viewSettings: 'StudentAffairsSettings.View',
  manageSettings: 'StudentAffairsSettings.Manage'
} as const;
