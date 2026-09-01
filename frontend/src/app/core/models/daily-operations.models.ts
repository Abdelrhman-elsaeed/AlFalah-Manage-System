import { PagedResult } from './api-response.model';
import {
  AcademicTermSummaryDto,
  ActorSummaryDto,
  ClassroomSummaryDto,
  GuardianStudentDto,
  GuardianSummaryDto,
  MetricBadgeDto,
  StudentSummaryDto
} from './student-affairs-dashboard.models';

export type StudentAttendanceStatus = 'Present' | 'Absent' | 'AbsentExcused';
export type AbsenceExcuseStatus = 'Pending' | 'Accepted' | 'Rejected';
export type AbsenceExcuseType = 'Medical' | 'Family' | 'Official' | 'Other';

export interface ClassroomDto {
  readonly id: number;
  readonly label: string;
  readonly stage: string;
  readonly gradeLevel: number;
  readonly section: string;
  readonly academicYearId: number;
  readonly academicYearLabel: string;
  readonly isActive: boolean;
  readonly activeEnrollmentCount: number;
  readonly rowVersion: string;
}

export interface StudentAttendanceSheetRowDto {
  readonly attendanceId: number | null;
  readonly student: StudentSummaryDto;
  readonly status: StudentAttendanceStatus;
  readonly excuseStatus: AbsenceExcuseStatus | null;
  readonly recordedBy: ActorSummaryDto | null;
  readonly recordedAt: string | null;
  readonly penaltyEligibleAbsenceBadge: MetricBadgeDto;
  readonly rowVersion: string | null;
}

export interface StudentAttendanceSheetDto {
  readonly date: string;
  readonly classroom: ClassroomSummaryDto;
  readonly rosterRevision: string;
  readonly isSaved: boolean;
  readonly rows: readonly StudentAttendanceSheetRowDto[];
}

export interface SubmitAbsentRosterRequestDto {
  readonly date: string;
  readonly classroomId: number;
  readonly absentStudentIds: readonly number[];
  readonly rosterRevision: string;
}

export interface BiometricImportIssueDto {
  readonly rowNumber: number;
  readonly code: string;
  readonly message: string;
}

export interface BiometricImportResultDto {
  readonly totalRows: number;
  readonly importedDelays: number;
  readonly skippedOnTimeRows: number;
  readonly duplicateRows: number;
  readonly unmatchedRows: number;
  readonly issues: readonly BiometricImportIssueDto[];
}

export interface StudentAttendanceRecordDto {
  readonly id: number;
  readonly student: StudentSummaryDto;
  readonly date: string;
  readonly status: StudentAttendanceStatus;
  readonly excuseStatus: AbsenceExcuseStatus | null;
  readonly recordedBy: ActorSummaryDto;
  readonly recordedAt: string;
  readonly rowVersion: string;
}

export interface StudentAttendanceHistoryDto {
  readonly student: StudentSummaryDto;
  readonly term: AcademicTermSummaryDto;
  readonly records: readonly StudentAttendanceRecordDto[];
  readonly absenceMetric: MetricBadgeDto;
}

export interface AttachmentDto {
  readonly id: number;
  readonly originalName: string;
  readonly contentType: string;
  readonly sizeBytes: number;
  readonly uploadedAt: string;
  readonly uploadedBy: ActorSummaryDto;
  readonly downloadUrl: string;
}

export interface AbsenceExcuseDto {
  readonly id: number;
  readonly excuseType: AbsenceExcuseType;
  readonly status: AbsenceExcuseStatus;
  readonly guardian: GuardianSummaryDto;
  readonly submittedAt: string;
  readonly reviewedBy: ActorSummaryDto | null;
  readonly reviewedAt: string | null;
  readonly reviewReason: string | null;
  readonly attachments: readonly AttachmentDto[];
  readonly rowVersion: string;
}

export interface StudentAttendanceRecordsQuery {
  readonly pageNumber: number;
  readonly pageSize: number;
  readonly fromDate?: string;
  readonly toDate?: string;
  readonly classroomId?: number;
  readonly studentId?: number;
  readonly excuseStatus?: AbsenceExcuseStatus;
}

export interface ReviewAbsenceExcuseRequestDto {
  readonly reviewNote: string | null;
  readonly rowVersion: string;
}

export interface RejectAbsenceExcuseRequestDto {
  readonly rejectionReason: string;
  readonly rowVersion: string;
}

export interface OfficerExcuseQueueItem {
  readonly attendance: StudentAttendanceRecordDto;
  readonly excuse: AbsenceExcuseDto;
}

export type ClassroomPage = PagedResult<ClassroomDto>;
export type AttendanceRecordsPage = PagedResult<StudentAttendanceRecordDto>;
export type LinkedGuardianStudent = GuardianStudentDto;
