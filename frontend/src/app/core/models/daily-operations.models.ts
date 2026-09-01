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

export type SchoolStage = 'Primary' | 'Intermediate' | 'Secondary';

export interface AcademicYearLookupDto {
  readonly id: number;
  readonly code: string;
  readonly nameAr: string;
  readonly isActive: boolean;
}

export interface CreateClassroomRequestDto {
  readonly academicYearId: number;
  readonly stage: SchoolStage;
  readonly gradeLevel: number;
  readonly section: string;
  readonly classLabel: string;
}

export interface UpdateClassroomRequestDto {
  readonly classLabel: string;
  readonly section: string;
  readonly isActive: boolean;
  readonly rowVersion: string;
}

export interface DeleteClassroomRequestDto {
  readonly reason: string;
  readonly rowVersion: string;
  readonly forceDelete: boolean;
}

export interface StudentListItemDto {
  readonly student: StudentSummaryDto;
  readonly riskBadges: readonly MetricBadgeDto[];
}

export interface StudentEnrollmentDto {
  readonly id: number;
  readonly classroom: ClassroomSummaryDto;
  readonly rollNumber: number | null;
  readonly rowVersion: string;
}

export interface StudentDetailsDto {
  readonly student: StudentSummaryDto;
  readonly identityNumber: string;
  readonly firstName: string;
  readonly middleName: string | null;
  readonly lastName: string;
  readonly nationalId: string | null;
  readonly dateOfBirth: string | null;
  readonly gender: string | null;
  readonly currentEnrollment: StudentEnrollmentDto | null;
  readonly rowVersion: string;
}

export interface CreateStudentRequestDto {
  readonly studentNumber: string;
  readonly identityNumber: string;
  readonly firstName: string;
  readonly middleName: string | null;
  readonly lastName: string;
  readonly nationalId: string | null;
  readonly dateOfBirth: string | null;
  readonly gender: string | null;
  readonly classroomId: number | null;
  readonly rollNumber: number | null;
}

export interface UpdateStudentRequestDto extends CreateStudentRequestDto {
  readonly isActive: boolean;
  readonly rowVersion: string;
}

export interface DeleteStudentRequestDto {
  readonly reason: string;
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
export type StudentPage = PagedResult<StudentListItemDto>;
export type AttendanceRecordsPage = PagedResult<StudentAttendanceRecordDto>;
export type LinkedGuardianStudent = GuardianStudentDto;

export interface StudentStatsQuery {
  readonly pageNumber?: number;
  readonly pageSize?: number;
  readonly search?: string;
  readonly classroomId?: number;
  readonly isActive?: boolean;
}

export interface StudentStatsDto {
  readonly studentId: number;
  readonly studentNumber: string;
  readonly name: string;
  readonly identityNumber: string;
  readonly nationalId: string | null;
  readonly classroomName: string;
  readonly classroomId: number | null;
  readonly isActive: boolean;
  readonly totalAbsences: number;
  readonly totalDelays: number;
  readonly totalExcuses: number;
  readonly totalReferrals: number;
}

export interface MonthlyAttendanceTrendDto {
  readonly monthKey: string;
  readonly monthLabel: string;
  readonly absences: number;
  readonly delays: number;
  readonly excuses: number;
}

export interface StudentAnalyticsEventDto {
  readonly id: string;
  readonly eventType: string;
  readonly title: string;
  readonly description: string | null;
  readonly occurredAt: string;
  readonly severity: 'danger' | 'warning' | 'info' | 'success' | string;
  readonly icon: string;
  readonly status: string | null;
  readonly actorName: string | null;
}

export interface StudentAnalyticsProfileDto {
  readonly studentId: number;
  readonly studentNumber: string;
  readonly fullName: string;
  readonly identityNumber: string;
  readonly nationalId: string | null;
  readonly dateOfBirth: string | null;
  readonly gender: string | null;
  readonly isActive: boolean;
  readonly profilePhotoStorageKey: string | null;
  readonly classroomId: number | null;
  readonly classroomName: string;
  readonly stage: string;
  readonly gradeLevel: number | null;
  readonly section: string;
  readonly rollNumber: number | null;
  readonly enrollmentStatus: string | null;
  readonly totalAbsences: number;
  readonly totalDelays: number;
  readonly totalExcuses: number;
  readonly totalReferrals: number;
  readonly totalBehaviors: number;
  readonly totalRecognitions: number;
  readonly totalGatePasses: number;
  readonly monthlyTrends: readonly MonthlyAttendanceTrendDto[];
  readonly recentEvents: readonly StudentAnalyticsEventDto[];
  readonly guardians: readonly any[];
}

export interface StudentStatsPage extends PagedResult<StudentStatsDto> {
  readonly totalClassrooms: number;
}

