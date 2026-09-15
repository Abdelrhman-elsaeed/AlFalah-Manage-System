import { BellSchedule } from './bell-schedule.models';

export type TimetableSetupStatus = 'Draft' | 'ReadyForGeneration' | 'Generated' | 'Archived';
export type TimetableStepStatus = 'not-started' | 'incomplete' | 'complete' | 'blocked';

export interface TimetableSetupAcademicYear {
  readonly id: number;
  readonly code: string;
  readonly nameAr: string;
  readonly isActive: boolean;
}

export interface CreateTimetableAcademicYearRequest {
  readonly code: string;
  readonly nameAr: string;
  readonly startsOn: string;
  readonly endsOn: string;
  readonly firstSemesterStartsOn: string;
  readonly firstSemesterEndsOn: string;
  readonly secondSemesterStartsOn: string;
  readonly secondSemesterEndsOn: string;
  readonly activeSemester: number;
}

export interface TimetableSetupProfile {
  readonly id: number;
  readonly schoolId: number;
  readonly academicYearId: number;
  readonly academicYearName: string;
  readonly semester: number;
  readonly semesterLabelAr: string;
  readonly name: string;
  readonly bellScheduleTemplateId: number | null;
  readonly status: TimetableSetupStatus;
  readonly statusLabelAr: string;
  readonly revision: number;
  readonly updatedAt: string;
}

export interface TimetableReadinessCounts {
  readonly activeClassrooms: number;
  readonly activeStudents: number;
  readonly classroomsMissingLocation: number;
  readonly activeTeachers: number;
  readonly availableSubjects: number;
}

export interface TimetableSetupStep {
  readonly key: string;
  readonly titleAr: string;
  readonly status: TimetableStepStatus;
  readonly descriptionAr: string;
  readonly route: string;
}

export interface TimetableSettingsOverview {
  readonly schoolId: number;
  readonly schoolName: string;
  readonly academicYears: readonly TimetableSetupAcademicYear[];
  readonly profiles: readonly TimetableSetupProfile[];
  readonly selectedProfile: TimetableSetupProfile | null;
  readonly selectedAcademicYearId: number;
  readonly selectedSemester: number;
  readonly selectedSemesterLabelAr: string;
  readonly canManage: boolean;
  readonly completionPercent: number;
  readonly hardPrerequisitesValid: boolean;
  readonly counts: TimetableReadinessCounts;
  readonly steps: readonly TimetableSetupStep[];
  readonly warnings: readonly string[];
  readonly bellSchedule?: BellSchedule | null;
}

export interface CreateTimetableSetupProfileRequest {
  readonly academicYearId: number;
  readonly semester: number;
  readonly name: string;
}

export interface UpdateTimetableSetupProfileRequest {
  readonly name: string;
  readonly revision: number;
}
