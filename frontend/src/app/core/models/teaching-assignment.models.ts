import { Subject, SubjectClassroom, SubjectRequirement } from './subject.models';

export type TeachingMode = 'SingleTeacher' | 'CoTeaching' | 'SplitQuota';
export interface TeachingMember { teacherTimetableProfileId: number; allocatedPeriodCount: number; allocatedPairedBlockCount: number; }
export interface TeachingCell { classSubjectRequirementId: number; mode: TeachingMode; members: TeachingMember[]; }
export interface TeachingAssignment extends TeachingCell { id: number; members: (TeachingMember & { instructorProfileId: number; name: string })[]; }
export interface TeachingTeacher { id: number; instructorProfileId: number; name: string; specialization: string | null; isActive: boolean;
  allocatedPeriods: number; maximumWeeklyPeriods: number; remainingPeriods: number; subjectCount: number; classroomCount: number; isVisiting: boolean; warnings: string[]; }
export interface TeachingOverview { revision: number; subjects: Subject[]; classrooms: SubjectClassroom[]; requirements: SubjectRequirement[];
  assignments: TeachingAssignment[]; teachers: TeachingTeacher[]; warnings: string[]; }
export interface TeachingTeacherPage { items: TeachingTeacher[]; totalCount: number; }
