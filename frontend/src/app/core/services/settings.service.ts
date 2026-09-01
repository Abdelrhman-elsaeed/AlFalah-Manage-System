import { HttpClient, HttpContext, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ApiOutcome,
  ApiResponse,
  PagedResult,
  normalizeApiResponse
} from '../models/api-response.model';
import {
  CreateStudentAffairsSettingsRequestDto,
  ResetStudentAffairsSettingsRequestDto,
  SchoolStudentAffairsSettingsDto,
  StudentAffairsPageQuery,
  StudentAffairsSettingsHistoryDto,
  UpdateStudentAffairsSettingsRequestDto
} from '../models/student-affairs-settings.models';
import { SUPPRESS_ERROR_TOAST } from '../http/http-context.tokens';

@Injectable({ providedIn: 'root' })
export class SettingsService {
  private readonly url = `${environment.apiUrl}/api/v1/student-affairs/settings`;
  private readonly callerHandlesErrors = new HttpContext().set(SUPPRESS_ERROR_TOAST, true);

  constructor(private readonly http: HttpClient) {}

  getSettings(): Observable<ApiOutcome<SchoolStudentAffairsSettingsDto>> {
    return this.http.get<ApiResponse<SchoolStudentAffairsSettingsDto>>(this.url, {
      context: this.callerHandlesErrors
    }).pipe(map(normalizeApiResponse));
  }

  createSettings(
    request: CreateStudentAffairsSettingsRequestDto,
    idempotencyKey: string
  ): Observable<ApiOutcome<SchoolStudentAffairsSettingsDto>> {
    return this.http.post<ApiResponse<SchoolStudentAffairsSettingsDto>>(this.url, request, {
      context: this.callerHandlesErrors,
      headers: this.idempotencyHeaders(idempotencyKey)
    }).pipe(map(normalizeApiResponse));
  }

  updateSettings(
    request: UpdateStudentAffairsSettingsRequestDto,
    idempotencyKey: string
  ): Observable<ApiOutcome<SchoolStudentAffairsSettingsDto>> {
    return this.http.put<ApiResponse<SchoolStudentAffairsSettingsDto>>(this.url, request, {
      context: this.callerHandlesErrors,
      headers: this.idempotencyHeaders(idempotencyKey)
    }).pipe(map(normalizeApiResponse));
  }

  resetSettings(
    request: ResetStudentAffairsSettingsRequestDto,
    idempotencyKey: string
  ): Observable<ApiOutcome<SchoolStudentAffairsSettingsDto>> {
    return this.http.request<ApiResponse<SchoolStudentAffairsSettingsDto>>('DELETE', this.url, {
      body: request,
      context: this.callerHandlesErrors,
      headers: this.idempotencyHeaders(idempotencyKey)
    }).pipe(map(normalizeApiResponse));
  }

  getHistory(
    query: StudentAffairsPageQuery
  ): Observable<ApiOutcome<PagedResult<StudentAffairsSettingsHistoryDto>>> {
    const params = new HttpParams()
      .set('pageNumber', query.pageNumber)
      .set('pageSize', query.pageSize);

    return this.http.get<ApiResponse<PagedResult<StudentAffairsSettingsHistoryDto>>>(`${this.url}/history`, {
      context: this.callerHandlesErrors,
      params
    }).pipe(map(normalizeApiResponse));
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
