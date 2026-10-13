import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams, HttpContext } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { SUPPRESS_ERROR_TOAST, SUPPRESS_FORBIDDEN_REDIRECT } from '../http/http-context.tokens';
import { ApiResponse } from '../models/api-response.model';
import { DutyDismissalStatistics, DutyExecution, DutyGuardianNotice, DutyLate, DutyReportContent, DutyRoster } from '../models/duty-execution.models';
@Injectable({ providedIn: 'root' })
export class DutyExecutionService {
  private readonly http = inject(HttpClient);
  private readonly options = { context: new HttpContext().set(SUPPRESS_ERROR_TOAST, true).set(SUPPRESS_FORBIDDEN_REDIRECT, true) };
  private readonly base = `${environment.apiUrl}/api/v1/school-duty`;
  get(id: number) { return this.http.get<ApiResponse<DutyExecution>>(`${this.base}/assignments/${id}/execution`, this.options); }
  roster(id: number, search: string, grade: number | null, classroomId: number | null, page: number) {
    let params = new HttpParams().set('assignmentId', id).set('search', search).set('page', page);
    if (grade !== null) params = params.set('grade', grade);
    if (classroomId !== null) params = params.set('classroomId', classroomId);
    return this.http.get<ApiResponse<DutyRoster>>(`${this.base}/students`, { params, ...this.options });
  }
  save(id: number, assignmentRevision: number, expectedVersion: number, content: DutyReportContent) { return this.http.put<ApiResponse<DutyExecution>>(`${this.base}/assignments/${id}/report/draft`, { assignmentRevision, expectedVersion, content }, this.options); }
  sign(id: number, action: 'acknowledge' | 'report/submit', assignmentRevision: number, requestId: string, draftVersion?: number) { return this.http.post<ApiResponse<DutyExecution>>(`${this.base}/assignments/${id}/${action}`, { assignmentRevision, requestId, draftVersion }, this.options); }
  correct(id: number, assignmentRevision: number, expectedVersion: number, reason: string, requestId: string) { return this.http.post<ApiResponse<DutyExecution>>(`${this.base}/assignments/${id}/report/correction`, { assignmentRevision, expectedVersion, reason, requestId }, this.options); }
  late(id: number, request: { assignmentRevision: number; studentId: number; enrollmentId: number; observedAt: string; departureAt: string | null; action: string; notes: string; requestId: string }) { return this.http.post<ApiResponse<DutyLate>>(`${this.base}/assignments/${id}/dismissal-lateness`, request, this.options); }
  correctLate(id: number, request: { expectedRevision: number; observedAt: string; departureAt: string | null; action: string; notes: string; reason: string; retract: boolean; requestId: string }) { return this.http.post<ApiResponse<DutyLate>>(`${this.base}/dismissal-lateness/${id}/correction`, request, this.options); }
  timing(id: number, assignmentRevision: number, dismissalAt: string | null, reason: string) { return this.http.post<ApiResponse<DutyExecution>>(`${this.base}/assignments/${id}/timing-exception`, { assignmentRevision, dismissalAt, reason, requestId: crypto.randomUUID() }, this.options); }
  statistics(filters: { from?: string; to?: string; termId?: number | null; classroomId?: number | null; page: number }) {
    let params = new HttpParams(); for (const [key,value] of Object.entries(filters)) if (value !== null && value !== undefined && value !== '') params = params.set(key, value);
    return this.http.get<ApiResponse<DutyDismissalStatistics>>(`${this.base}/dismissal-lateness`, { params, ...this.options });
  }
  retry(id: number) { return this.http.post<ApiResponse<boolean>>(`${this.base}/dismissal-lateness/${id}/retry`, {}, this.options); }
  guardian() { return this.http.get<ApiResponse<DutyGuardianNotice[]>>(`${this.base}/guardian/notices`, this.options); }
  read(id: number) { return this.http.post<ApiResponse<boolean>>(`${this.base}/guardian/notices/${id}/read`, {}, this.options); }
  snapshot(id: number) { return this.http.get<ApiResponse<{ Snapshot: unknown; SignatureData: string; SignatureHash: string; SnapshotHash: string }>>(`${this.base}/documents/${id}/snapshot`, this.options); }
}
