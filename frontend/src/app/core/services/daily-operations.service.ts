import {
  HttpClient,
  HttpContext,
  HttpHeaders,
  HttpParams,
  HttpResponse
} from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SUPPRESS_ERROR_TOAST } from '../http/http-context.tokens';
import { ApiResponse } from '../models/api-response.model';
import {
  AbsenceExcuseDto,
  AbsenceExcuseType,
  AttendanceRecordsPage,
  BiometricImportResultDto,
  ClassroomPage,
  StudentAttendanceHistoryDto,
  StudentAttendanceRecordsQuery,
  StudentAttendanceSheetDto,
  SubmitAbsentRosterRequestDto,
  ReviewAbsenceExcuseRequestDto,
  RejectAbsenceExcuseRequestDto
} from '../models/daily-operations.models';
import { GuardianStudentDto } from '../models/student-affairs-dashboard.models';

@Injectable({ providedIn: 'root' })
export class DailyOperationsService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/api/v1`;
  private readonly callerHandlesErrors = new HttpContext().set(SUPPRESS_ERROR_TOAST, true);

  getClassrooms(): Observable<ApiResponse<ClassroomPage>> {
    return this.http.get<ApiResponse<ClassroomPage>>(`${this.api}/classrooms`, {
      context: this.callerHandlesErrors,
      params: new HttpParams().set('pageSize', 100)
    });
  }

  getAttendanceSheet(date: string, classroomId: number): Observable<ApiResponse<StudentAttendanceSheetDto>> {
    return this.http.get<ApiResponse<StudentAttendanceSheetDto>>(`${this.api}/student-attendance/sheet`, {
      context: this.callerHandlesErrors,
      params: new HttpParams().set('date', date).set('classroomId', classroomId)
    });
  }

  saveAttendanceSheet(
    request: SubmitAbsentRosterRequestDto,
    idempotencyKey: string
  ): Observable<ApiResponse<StudentAttendanceSheetDto>> {
    return this.http.put<ApiResponse<StudentAttendanceSheetDto>>(`${this.api}/student-attendance/sheet`, request, {
      context: this.callerHandlesErrors,
      headers: this.idempotencyHeaders(idempotencyKey)
    });
  }

  importZajel(file: File): Observable<ApiResponse<BiometricImportResultDto>> {
    const body = new FormData();
    body.append('file', file, file.name);
    return this.http.post<ApiResponse<BiometricImportResultDto>>(
      `${this.api}/student-affairs/biometrics/zajel/import`,
      body,
      { context: this.callerHandlesErrors }
    );
  }

  exportNoor(weekStartsOn: string, idempotencyKey: string): Observable<HttpResponse<Blob>> {
    return this.http.post(`${this.api}/student-attendance/noor/exports`, null, {
      context: this.callerHandlesErrors,
      headers: this.idempotencyHeaders(idempotencyKey),
      observe: 'response',
      params: new HttpParams().set('weekStartsOn', weekStartsOn),
      responseType: 'blob'
    });
  }

  getGuardianStudents(): Observable<ApiResponse<readonly GuardianStudentDto[]>> {
    return this.http.get<ApiResponse<readonly GuardianStudentDto[]>>(`${this.api}/guardian/students`, {
      context: this.callerHandlesErrors
    });
  }

  getStudentAttendanceHistory(
    studentId: number,
    academicTermId?: number
  ): Observable<ApiResponse<StudentAttendanceHistoryDto>> {
    let params = new HttpParams();
    if (academicTermId !== undefined) params = params.set('academicTermId', academicTermId);
    return this.http.get<ApiResponse<StudentAttendanceHistoryDto>>(
      `${this.api}/student-attendance/students/${studentId}`,
      { context: this.callerHandlesErrors, params }
    );
  }

  submitExcuse(
    attendanceId: number,
    excuseType: AbsenceExcuseType,
    notes: string,
    attachment: File,
    idempotencyKey: string
  ): Observable<ApiResponse<AbsenceExcuseDto>> {
    const body = new FormData();
    body.append('excuseType', excuseType);
    if (notes.trim()) body.append('notes', notes.trim());
    body.append('attachment', attachment, attachment.name);
    return this.http.post<ApiResponse<AbsenceExcuseDto>>(
      `${this.api}/student-attendance/${attendanceId}/excuses`,
      body,
      {
        context: this.callerHandlesErrors,
        headers: this.idempotencyHeaders(idempotencyKey)
      }
    );
  }

  getAttendanceRecords(query: StudentAttendanceRecordsQuery): Observable<ApiResponse<AttendanceRecordsPage>> {
    let params = new HttpParams()
      .set('pageNumber', query.pageNumber)
      .set('pageSize', query.pageSize);
    if (query.fromDate) params = params.set('fromDate', query.fromDate);
    if (query.toDate) params = params.set('toDate', query.toDate);
    if (query.classroomId !== undefined) params = params.set('classroomId', query.classroomId);
    if (query.studentId !== undefined) params = params.set('studentId', query.studentId);
    if (query.excuseStatus) params = params.set('excuseStatus', query.excuseStatus);
    return this.http.get<ApiResponse<AttendanceRecordsPage>>(`${this.api}/student-attendance/records`, {
      context: this.callerHandlesErrors,
      params
    });
  }

  getExcuses(attendanceId: number): Observable<ApiResponse<readonly AbsenceExcuseDto[]>> {
    return this.http.get<ApiResponse<readonly AbsenceExcuseDto[]>>(
      `${this.api}/student-attendance/${attendanceId}/excuses`,
      { context: this.callerHandlesErrors }
    );
  }

  acceptExcuse(
    excuseId: number,
    request: ReviewAbsenceExcuseRequestDto
  ): Observable<ApiResponse<AbsenceExcuseDto>> {
    return this.http.post<ApiResponse<AbsenceExcuseDto>>(
      `${this.api}/student-attendance/excuses/${excuseId}/accept`,
      request,
      { context: this.callerHandlesErrors }
    );
  }

  rejectExcuse(
    excuseId: number,
    request: RejectAbsenceExcuseRequestDto
  ): Observable<ApiResponse<AbsenceExcuseDto>> {
    return this.http.post<ApiResponse<AbsenceExcuseDto>>(
      `${this.api}/student-attendance/excuses/${excuseId}/reject`,
      request,
      { context: this.callerHandlesErrors }
    );
  }

  downloadExcuseAttachment(excuseId: number, attachmentId: number): Observable<HttpResponse<Blob>> {
    return this.http.get(
      `${this.api}/student-attendance/excuses/${excuseId}/attachments/${attachmentId}`,
      {
        context: this.callerHandlesErrors,
        observe: 'response',
        responseType: 'blob'
      }
    );
  }

  createIdempotencyKey(): string {
    return typeof crypto !== 'undefined' && 'randomUUID' in crypto
      ? crypto.randomUUID()
      : `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  }

  private idempotencyHeaders(key: string): HttpHeaders {
    return new HttpHeaders({ 'Idempotency-Key': key });
  }
}
