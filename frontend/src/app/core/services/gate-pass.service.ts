import { HttpClient, HttpContext, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SUPPRESS_ERROR_TOAST, SUPPRESS_FORBIDDEN_REDIRECT } from '../http/http-context.tokens';
import { ApiResponse } from '../models/api-response.model';
import {
  AcknowledgeGatePassRequestDto,
  ApproveGatePassRequestDto,
  CancelGatePassRequestDto,
  CreateGatePassRequestDto,
  ExecuteGatePassRequestDto,
  GatePassDto,
  GatePassHistoryDto,
  GatePassListQuery,
  GatePassPage,
  RejectGatePassRequestDto,
  SecurityGatePassPage
} from '../models/gate-pass.models';
import { GuardianStudentDto } from '../models/student-affairs-dashboard.models';

@Injectable({ providedIn: 'root' })
export class GatePassService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/api/v1`;
  private readonly localErrors = new HttpContext()
    .set(SUPPRESS_ERROR_TOAST, true)
    .set(SUPPRESS_FORBIDDEN_REDIRECT, true);

  getGuardianStudents(): Observable<ApiResponse<readonly GuardianStudentDto[]>> {
    return this.http.get<ApiResponse<readonly GuardianStudentDto[]>>(`${this.api}/guardian/students`, {
      context: this.localErrors
    });
  }

  getMine(query: GatePassListQuery): Observable<ApiResponse<GatePassPage>> {
    return this.http.get<ApiResponse<GatePassPage>>(`${this.api}/gate-passes/mine`, {
      context: this.localErrors,
      params: this.queryParams(query)
    });
  }

  create(request: CreateGatePassRequestDto, idempotencyKey: string): Observable<ApiResponse<GatePassDto>> {
    return this.http.post<ApiResponse<GatePassDto>>(`${this.api}/gate-passes`, request, {
      context: this.localErrors,
      headers: new HttpHeaders({ 'Idempotency-Key': idempotencyKey })
    });
  }

  list(query: GatePassListQuery): Observable<ApiResponse<GatePassPage>> {
    return this.http.get<ApiResponse<GatePassPage>>(`${this.api}/gate-passes`, {
      context: this.localErrors,
      params: this.queryParams(query)
    });
  }

  getById(id: number): Observable<ApiResponse<GatePassDto>> {
    return this.http.get<ApiResponse<GatePassDto>>(`${this.api}/gate-passes/${id}`, {
      context: this.localErrors
    });
  }

  approve(id: number, request: ApproveGatePassRequestDto): Observable<ApiResponse<GatePassDto>> {
    return this.http.post<ApiResponse<GatePassDto>>(`${this.api}/gate-passes/${id}/approve`, request, {
      context: this.localErrors
    });
  }

  reject(id: number, request: RejectGatePassRequestDto): Observable<ApiResponse<GatePassDto>> {
    return this.http.post<ApiResponse<GatePassDto>>(`${this.api}/gate-passes/${id}/reject`, request, {
      context: this.localErrors
    });
  }

  cancel(id: number, request: CancelGatePassRequestDto): Observable<ApiResponse<GatePassDto>> {
    return this.http.post<ApiResponse<GatePassDto>>(`${this.api}/gate-passes/${id}/cancel`, request, {
      context: this.localErrors
    });
  }

  acknowledgeSecurity(id: number, request: AcknowledgeGatePassRequestDto): Observable<ApiResponse<GatePassDto>> {
    return this.http.post<ApiResponse<GatePassDto>>(
      `${this.api}/gate-passes/${id}/security-acknowledgement`,
      request,
      { context: this.localErrors }
    );
  }

  execute(id: number, request: ExecuteGatePassRequestDto): Observable<ApiResponse<GatePassDto>> {
    return this.http.post<ApiResponse<GatePassDto>>(`${this.api}/gate-passes/${id}/exit`, request, {
      context: this.localErrors
    });
  }

  securityQueue(query: GatePassListQuery): Observable<ApiResponse<SecurityGatePassPage>> {
    return this.http.get<ApiResponse<SecurityGatePassPage>>(`${this.api}/gate-passes/security-queue`, {
      context: this.localErrors,
      params: this.queryParams(query)
    });
  }

  history(id: number): Observable<ApiResponse<GatePassHistoryDto>> {
    return this.http.get<ApiResponse<GatePassHistoryDto>>(`${this.api}/gate-passes/${id}/history`, {
      context: this.localErrors
    });
  }

  createIdempotencyKey(): string {
    return typeof crypto !== 'undefined' && 'randomUUID' in crypto
      ? crypto.randomUUID()
      : `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  }

  private queryParams(query: GatePassListQuery): HttpParams {
    let params = new HttpParams()
      .set('pageNumber', query.pageNumber)
      .set('pageSize', query.pageSize);
    if (query.status) params = params.set('status', query.status);
    if (query.date) params = params.set('date', query.date);
    if (query.classroomId !== undefined) params = params.set('classroomId', query.classroomId);
    if (query.sortBy) params = params.set('sortBy', query.sortBy);
    if (query.sortDirection) params = params.set('sortDirection', query.sortDirection);
    return params;
  }
}
