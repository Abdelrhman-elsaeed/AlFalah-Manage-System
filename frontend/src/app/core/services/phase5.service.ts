import { HttpClient, HttpContext, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SUPPRESS_ERROR_TOAST } from '../http/http-context.tokens';
import { ApiResponse, PagedResult } from '../models/api-response.model';
import {
  AcademicConcernDto,
  AcceptReferralRequestDto,
  AddReferralActionRequestDto,
  ApproveNotificationRequestDto,
  AttendSummonRequestDto,
  BehaviorIncidentDto,
  CloseConversationRequestDto,
  ConversationDto,
  ConversationMessageDto,
  CreateConversationRequestDto,
  CreateSummonRequestDto,
  GuardianStaffOptionDto,
  GuardianTeacherOptionDto,
  MarkConversationReadRequestDto,
  MarkSummonNoShowRequestDto,
  MarkSummonImprovedRequestDto,
  OfficeHoursAggregateDto,
  PendingDispatchDto,
  ReferralDto,
  ReferralHistoryDto,
  ReferralListQuery,
  ReopenReferralRequestDto,
  ResolveReferralRequestDto,
  ScheduleSummonRequestDto,
  SendMessageRequestDto,
  SendMessageResultDto,
  StartSummonObservationRequestDto,
  StudentGuardianLinkDto,
  SummonDto,
  SummonHistoryDto,
  SummonListQuery,
  SuppressNotificationRequestDto,
  UpdateMyOfficeHoursRequestDto
} from '../models/phase5.models';
import { SocialWorkerStudentAffairsDashboardDto } from '../models/student-affairs-dashboard.models';

interface ConversationListQuery {
  readonly studentId?: number;
  readonly isUnread?: boolean;
  readonly pageNumber: number;
  readonly pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class Phase5Service {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/api/v1`;
  private readonly callerHandlesErrors = new HttpContext().set(SUPPRESS_ERROR_TOAST, true);

  listReferrals(query: ReferralListQuery): Observable<ApiResponse<PagedResult<ReferralDto>>> {
    return this.http.get<ApiResponse<PagedResult<ReferralDto>>>(`${this.api}/referrals`, {
      context: this.callerHandlesErrors,
      params: this.params(query)
    });
  }
  getReferral(id: number): Observable<ApiResponse<ReferralDto>> {
    return this.get<ReferralDto>(`referrals/${id}`);
  }
  getReferralHistory(id: number): Observable<ApiResponse<ReferralHistoryDto>> {
    return this.get<ReferralHistoryDto>(`referrals/${id}/history`);
  }
  acceptReferral(id: number, request: AcceptReferralRequestDto): Observable<ApiResponse<ReferralDto>> {
    return this.post<ReferralDto>(`referrals/${id}/accept`, request);
  }
  addReferralAction(id: number, request: AddReferralActionRequestDto): Observable<ApiResponse<ReferralDto>> {
    return this.post<ReferralDto>(`referrals/${id}/actions`, request);
  }
  resolveReferral(id: number, request: ResolveReferralRequestDto): Observable<ApiResponse<ReferralDto>> {
    return this.post<ReferralDto>(`referrals/${id}/resolve`, request);
  }
  reopenReferral(id: number, request: ReopenReferralRequestDto): Observable<ApiResponse<ReferralDto>> {
    return this.post<ReferralDto>(`referrals/${id}/reopen`, request);
  }

  listSummons(query: SummonListQuery): Observable<ApiResponse<PagedResult<SummonDto>>> {
    return this.http.get<ApiResponse<PagedResult<SummonDto>>>(`${this.api}/summons`, {
      context: this.callerHandlesErrors,
      params: this.params(query)
    });
  }
  createSummon(request: CreateSummonRequestDto, idempotencyKey: string): Observable<ApiResponse<SummonDto>> {
    return this.http.post<ApiResponse<SummonDto>>(`${this.api}/summons`, request, {
      context: this.callerHandlesErrors,
      headers: new HttpHeaders().set('Idempotency-Key', idempotencyKey)
    });
  }
  getSummon(id: number): Observable<ApiResponse<SummonDto>> { return this.get<SummonDto>(`summons/${id}`); }
  getSummonHistory(id: number): Observable<ApiResponse<SummonHistoryDto>> { return this.get<SummonHistoryDto>(`summons/${id}/history`); }
  getStudentGuardians(studentId: number): Observable<ApiResponse<readonly StudentGuardianLinkDto[]>> {
    return this.get<readonly StudentGuardianLinkDto[]>(`students/${studentId}/guardians`);
  }
  scheduleSummon(id: number, request: ScheduleSummonRequestDto): Observable<ApiResponse<SummonDto>> {
    return this.post<SummonDto>(`summons/${id}/schedule`, request);
  }
  attendSummon(id: number, request: AttendSummonRequestDto): Observable<ApiResponse<SummonDto>> {
    return this.post<SummonDto>(`summons/${id}/attend`, request);
  }
  markSummonNoShow(id: number, request: MarkSummonNoShowRequestDto): Observable<ApiResponse<SummonDto>> {
    return this.post<SummonDto>(`summons/${id}/no-show`, request);
  }
  startObservation(id: number, request: StartSummonObservationRequestDto): Observable<ApiResponse<SummonDto>> {
    return this.post<SummonDto>(`summons/${id}/start-observation`, request);
  }
  markImproved(id: number, request: MarkSummonImprovedRequestDto): Observable<ApiResponse<SummonDto>> {
    return this.post<SummonDto>(`summons/${id}/mark-improved`, request);
  }
  getSocialWorkerDashboard(): Observable<ApiResponse<SocialWorkerStudentAffairsDashboardDto>> {
    return this.get<SocialWorkerStudentAffairsDashboardDto>('student-affairs/dashboard/social-worker');
  }

  listPendingDispatch(pageNumber: number, pageSize: number, search = ''): Observable<ApiResponse<PagedResult<PendingDispatchDto>>> {
    let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize).set('sortDirection', 'asc');
    if (search.trim()) params = params.set('search', search.trim());
    return this.http.get<ApiResponse<PagedResult<PendingDispatchDto>>>(`${this.api}/notifications/pending-dispatch`, {
      context: this.callerHandlesErrors,
      params
    });
  }
  approveNotification(id: number, request: ApproveNotificationRequestDto): Observable<ApiResponse<PendingDispatchDto>> {
    return this.post<PendingDispatchDto>(`notifications/${id}/approve`, request);
  }
  suppressNotification(id: number, request: SuppressNotificationRequestDto): Observable<ApiResponse<PendingDispatchDto>> {
    return this.post<PendingDispatchDto>(`notifications/${id}/suppress`, request);
  }
  getBehavior(id: number): Observable<ApiResponse<BehaviorIncidentDto>> { return this.get<BehaviorIncidentDto>(`behaviors/${id}`); }
  getAcademicConcern(id: number): Observable<ApiResponse<AcademicConcernDto>> { return this.get<AcademicConcernDto>(`academic-concerns/${id}`); }

  getEligibleOfficeHours(): Observable<ApiResponse<OfficeHoursAggregateDto>> { return this.get<OfficeHoursAggregateDto>('office-hours/me/eligible'); }
  getMyOfficeHours(): Observable<ApiResponse<OfficeHoursAggregateDto>> { return this.get<OfficeHoursAggregateDto>('office-hours/me'); }
  updateMyOfficeHours(request: UpdateMyOfficeHoursRequestDto): Observable<ApiResponse<OfficeHoursAggregateDto>> {
    return this.http.put<ApiResponse<OfficeHoursAggregateDto>>(`${this.api}/office-hours/me`, request, { context: this.callerHandlesErrors });
  }

  listConversations(query: ConversationListQuery): Observable<ApiResponse<PagedResult<ConversationDto>>> {
    return this.http.get<ApiResponse<PagedResult<ConversationDto>>>(`${this.api}/conversations`, {
      context: this.callerHandlesErrors,
      params: this.params(query)
    });
  }
  getGuardianTeacherOptions(studentId: number): Observable<ApiResponse<readonly GuardianTeacherOptionDto[]>> {
    return this.get<readonly GuardianTeacherOptionDto[]>(`conversations/recipient-options/teachers?studentId=${studentId}`);
  }
  getGuardianStaffOptions(studentId: number): Observable<ApiResponse<readonly GuardianStaffOptionDto[]>> {
    return this.get<readonly GuardianStaffOptionDto[]>(`conversations/recipient-options/staff?studentId=${studentId}`);
  }
  createConversation(request: CreateConversationRequestDto): Observable<ApiResponse<ConversationDto>> {
    return this.post<ConversationDto>('conversations', request);
  }
  getConversation(id: number): Observable<ApiResponse<ConversationDto>> { return this.get<ConversationDto>(`conversations/${id}`); }
  getMessages(id: number, pageSize: number, beforeMessageId?: number): Observable<ApiResponse<PagedResult<ConversationMessageDto>>> {
    let params = new HttpParams().set('pageNumber', 1).set('pageSize', pageSize).set('sortDirection', 'asc');
    if (beforeMessageId !== undefined) params = params.set('beforeMessageId', beforeMessageId);
    return this.http.get<ApiResponse<PagedResult<ConversationMessageDto>>>(`${this.api}/conversations/${id}/messages`, {
      context: this.callerHandlesErrors,
      params
    });
  }
  sendMessage(id: number, request: SendMessageRequestDto): Observable<ApiResponse<SendMessageResultDto>> {
    return this.post<SendMessageResultDto>(`conversations/${id}/messages`, request);
  }
  markConversationRead(id: number, request: MarkConversationReadRequestDto): Observable<ApiResponse<boolean>> {
    return this.post<boolean>(`conversations/${id}/read`, request);
  }
  closeConversation(id: number, request: CloseConversationRequestDto): Observable<ApiResponse<ConversationDto>> {
    return this.post<ConversationDto>(`conversations/${id}/close`, request);
  }

  createIdempotencyKey(): string {
    return typeof crypto !== 'undefined' && 'randomUUID' in crypto
      ? crypto.randomUUID()
      : `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  }

  idempotencyHeaders(key: string): HttpHeaders { return new HttpHeaders({ 'Idempotency-Key': key }); }

  private get<T>(path: string): Observable<ApiResponse<T>> {
    return this.http.get<ApiResponse<T>>(`${this.api}/${path}`, { context: this.callerHandlesErrors });
  }
  private post<T>(path: string, body: object): Observable<ApiResponse<T>> {
    return this.http.post<ApiResponse<T>>(`${this.api}/${path}`, body, { context: this.callerHandlesErrors });
  }
  private params(query: object): HttpParams {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null && value !== '') params = params.set(key, String(value));
    }
    return params;
  }
}
