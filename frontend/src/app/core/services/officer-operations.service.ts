import { HttpClient, HttpContext, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SUPPRESS_ERROR_TOAST } from '../http/http-context.tokens';
import { ApiResponse } from '../models/api-response.model';
import {
  AssignableSocialWorkerDto,
  AutomationReviewPage,
  ClassroomEntryPermitDto,
  EntryPermitPage,
  OperationalRecordPage,
  ReferralPage,
  StudentLookupPage
} from '../models/officer-operations.models';
import { ReferralDto, SummonDto } from '../models/phase5.models';

@Injectable({ providedIn: 'root' })
export class OfficerOperationsService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/api/v1`;
  private readonly context = new HttpContext().set(SUPPRESS_ERROR_TOAST, true);

  searchStudents(search: string): Observable<ApiResponse<StudentLookupPage>> {
    return this.http.get<ApiResponse<StudentLookupPage>>(`${this.api}/students/stats`, {
      context: this.context,
      params: new HttpParams().set('pageNumber', 1).set('pageSize', 25).set('search', search.trim())
    });
  }

  listEntryPermits(page: number, pageSize: number): Observable<ApiResponse<EntryPermitPage>> {
    return this.http.get<ApiResponse<EntryPermitPage>>(`${this.api}/classroom-entry-permits`, {
      context: this.context,
      params: this.pageParams(page, pageSize)
    });
  }

  issueEntryPermit(request: { studentId: number; reason: string; validFrom: string; validUntil: string }): Observable<ApiResponse<ClassroomEntryPermitDto>> {
    return this.http.post<ApiResponse<ClassroomEntryPermitDto>>(`${this.api}/classroom-entry-permits`, request, { context: this.context });
  }

  revokeEntryPermit(id: number, reason: string, rowVersion: string): Observable<ApiResponse<ClassroomEntryPermitDto>> {
    return this.http.post<ApiResponse<ClassroomEntryPermitDto>>(`${this.api}/classroom-entry-permits/${id}/revoke`, { reason, rowVersion }, { context: this.context });
  }

  listReferrals(page: number, pageSize: number): Observable<ApiResponse<ReferralPage>> {
    return this.http.get<ApiResponse<ReferralPage>>(`${this.api}/referrals`, {
      context: this.context,
      params: this.pageParams(page, pageSize).set('isAssigned', false)
    });
  }

  createReferral(request: { studentId: number; reason: string; source: string; priority: string }, idempotencyKey: string): Observable<ApiResponse<ReferralDto>> {
    return this.http.post<ApiResponse<ReferralDto>>(`${this.api}/referrals`, request, {
      context: this.context,
      headers: new HttpHeaders({ 'Idempotency-Key': idempotencyKey })
    });
  }

  assignableWorkers(search = ''): Observable<ApiResponse<readonly AssignableSocialWorkerDto[]>> {
    return this.http.get<ApiResponse<readonly AssignableSocialWorkerDto[]>>(`${this.api}/referrals/assignable-social-workers`, {
      context: this.context,
      params: search.trim() ? new HttpParams().set('search', search.trim()) : undefined
    });
  }

  assignReferral(id: number, socialWorkerUserId: string, reason: string, rowVersion: string): Observable<ApiResponse<ReferralDto>> {
    return this.http.post<ApiResponse<ReferralDto>>(`${this.api}/referrals/${id}/assign`, { socialWorkerUserId, reason: reason.trim() || null, rowVersion }, { context: this.context });
  }

  listAutomationReviews(page: number, pageSize: number): Observable<ApiResponse<AutomationReviewPage>> {
    return this.http.get<ApiResponse<AutomationReviewPage>>(`${this.api}/summons/automation-impact-reviews`, {
      context: this.context,
      params: this.pageParams(page, pageSize)
    });
  }

  reviewAutomationImpact(id: number, decision: string, rationale: string, rowVersion: string): Observable<ApiResponse<SummonDto>> {
    return this.http.post<ApiResponse<SummonDto>>(`${this.api}/summons/${id}/automation-impact-review`, { decision, rationale, rowVersion }, { context: this.context });
  }

  listOperationalRecords(type: string, page: number, pageSize: number, search = ''): Observable<ApiResponse<OperationalRecordPage>> {
    return this.http.get<ApiResponse<OperationalRecordPage>>(`${this.api}/${type}`, {
      context: this.context,
      params: search.trim()
        ? this.pageParams(page, pageSize).set('search', search.trim())
        : this.pageParams(page, pageSize)
    });
  }

  createIdempotencyKey(): string {
    return typeof crypto !== 'undefined' && 'randomUUID' in crypto
      ? crypto.randomUUID()
      : `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  }

  private pageParams(page: number, pageSize: number): HttpParams {
    return new HttpParams().set('pageNumber', page).set('pageSize', pageSize);
  }
}
