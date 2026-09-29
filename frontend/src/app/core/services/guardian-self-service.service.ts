import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SUPPRESS_ERROR_TOAST } from '../http/http-context.tokens';
import { ApiResponse } from '../models/api-response.model';
import {
  GuardianEntryPermitDto,
  GuardianEntryPermitPage,
  GuardianNotificationPage,
  GuardianSummonDto,
  GuardianSummonPage
} from '../models/guardian-self-service.models';

@Injectable({ providedIn: 'root' })
export class GuardianSelfServiceService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/api/v1`;
  private readonly context = new HttpContext().set(SUPPRESS_ERROR_TOAST, true);
  readonly unreadNotifications = signal(0);

  listSummons(pageNumber = 1, pageSize = 10): Observable<ApiResponse<GuardianSummonPage>> {
    return this.http.get<ApiResponse<GuardianSummonPage>>(`${this.api}/summons/mine`, {
      context: this.context,
      params: this.pageParams(pageNumber, pageSize)
    });
  }

  getSummon(id: number): Observable<ApiResponse<GuardianSummonDto>> {
    return this.http.get<ApiResponse<GuardianSummonDto>>(`${this.api}/summons/mine/${id}`, { context: this.context });
  }

  listEntryPermits(pageNumber = 1, pageSize = 10): Observable<ApiResponse<GuardianEntryPermitPage>> {
    return this.http.get<ApiResponse<GuardianEntryPermitPage>>(`${this.api}/classroom-entry-permits`, {
      context: this.context,
      params: this.pageParams(pageNumber, pageSize)
    });
  }

  getEntryPermit(id: number): Observable<ApiResponse<GuardianEntryPermitDto>> {
    return this.http.get<ApiResponse<GuardianEntryPermitDto>>(`${this.api}/classroom-entry-permits/${id}`, { context: this.context });
  }

  listNotifications(pageNumber = 1, pageSize = 10, isRead?: boolean): Observable<ApiResponse<GuardianNotificationPage>> {
    let params = this.pageParams(pageNumber, pageSize);
    if (isRead !== undefined) params = params.set('isRead', isRead);
    return this.http.get<ApiResponse<GuardianNotificationPage>>(`${this.api}/notifications`, { context: this.context, params });
  }

  unreadCount(): Observable<ApiResponse<number>> {
    return this.http.get<ApiResponse<number>>(`${this.api}/notifications/unread-count`, { context: this.context })
      .pipe(tap(response => {
        if (response.isSuccess) this.unreadNotifications.set(response.data ?? 0);
      }));
  }

  markRead(id: number): Observable<ApiResponse<boolean>> {
    return this.http.post<ApiResponse<boolean>>(`${this.api}/notifications/${id}/read`, {}, { context: this.context });
  }

  markAllRead(): Observable<ApiResponse<boolean>> {
    return this.http.post<ApiResponse<boolean>>(`${this.api}/notifications/read-all`, {}, { context: this.context });
  }

  private pageParams(pageNumber: number, pageSize: number): HttpParams {
    return new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize).set('sortDirection', 'desc');
  }
}
