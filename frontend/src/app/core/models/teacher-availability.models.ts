export interface AvailabilityTeacher { id: number; name: string; }
export interface AvailabilityCell {
  day: number; bellPeriodId: number; sequence: number; startLocalTime: string; endLocalTime: string; isAvailable: boolean;
}
export interface TeacherAvailability {
  instructorProfileId: number; teacherName: string; timetableSetupProfileId: number;
  revision: number; bellScheduleRevisionId: number; scheduleName: string; shortDisplayName: string;
  maximumWeeklyPeriods: number; isVisiting: boolean; hideFromPrint: boolean;
  allocatedPeriods: number; remainingPeriods: number; requiresScheduleReview: boolean;
  slots: AvailabilityCell[]; orphanedSlots: AvailabilityCell[]; violations: string[];
}
export interface UpdateTeacherProfile {
  revision: number; bellScheduleRevisionId: number; shortDisplayName: string; maximumWeeklyPeriods: number;
  isVisiting: boolean; hideFromPrint: boolean; confirmScheduleReview: boolean;
  slots: { day: number; bellPeriodId: number; isAvailable: boolean }[];
}
