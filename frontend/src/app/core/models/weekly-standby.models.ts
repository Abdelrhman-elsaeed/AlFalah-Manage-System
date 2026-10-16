export interface StandbyDemand { day: number; period: number; required: number; }
export interface WeeklyStandbySettings { requiredPerPeriod: number; overrides: StandbyDemand[]; excludedTeacherIds: number[]; }
export interface StandbyPlacement { instructorProfileId: number; day: number; period: number; isLocked: boolean; source?: string | null; }
export interface StandbyPeriod { day: number; period: number; start: string; end: string; required: number; available: number; allocated: number; }
export interface StandbyCell { day: number; period: number; eligible: boolean; canStandby: boolean; kind: string; reason: string | null; classroom: string | null; subject: string | null; isProtected: boolean; }
export interface StandbyTeacher { id: number; name: string; teachingCount: number; availablePeriods: number; standbyCount: number; capacity: number; cells: StandbyCell[]; }
export interface WeeklyStandbyWorkbench {
  timetableId: number; title: string; revision: number; setupRevision: number | null; bellScheduleRevisionId: number | null;
  isPublished: boolean; ready: boolean; issues: string[]; settings: WeeklyStandbySettings;
  periods: StandbyPeriod[]; teachers: StandbyTeacher[]; placements: StandbyPlacement[]; notices?: string[] | null;
}
export interface WeeklyStandbyRequest { revision: number; mode: 'Manual' | 'Fair'; settings: WeeklyStandbySettings; placements: StandbyPlacement[]; proposalId?: string; }
export interface WeeklyStandbyPreview { proposalId: string; mode: string; workbench: WeeklyStandbyWorkbench; }
