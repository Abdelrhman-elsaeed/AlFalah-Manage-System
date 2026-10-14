import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { DutyChange, DutyPreview, DutySettings, DutyWorkspace } from '../models/school-duty.models';
import { SUPPRESS_ERROR_TOAST, SUPPRESS_FORBIDDEN_REDIRECT } from '../http/http-context.tokens';
@Injectable({ providedIn: 'root' })
export class SchoolDutyService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/v1/school-duty`;
  private readonly options = { context: new HttpContext().set(SUPPRESS_ERROR_TOAST, true).set(SUPPRESS_FORBIDDEN_REDIRECT, true) };
  context(termId?: number | null) { return this.http.get<ApiResponse<DutyWorkspace>>(`${this.base}/context`, { params: termId ? new HttpParams().set('termId', termId) : new HttpParams(), ...this.options }); }
  preview(settings: DutySettings, expectedRevision: number) { return this.http.post<ApiResponse<DutyPreview>>(`${this.base}/plans/preview`, { settings, expectedRevision }, this.options); }
  publish(request: { settings: DutySettings; expectedRevision: number; previewDigest: string; requestId: string }) { return this.http.post<ApiResponse<{ planId: number; revision: number }>>(`${this.base}/plans/publish`, request, this.options); }
  changePreview(request: DutyChange) { return this.http.post<ApiResponse<DutyPreview>>(`${this.base}/assignments/change-preview`, request, this.options); }
  change(request: DutyChange) { return this.http.post<ApiResponse<unknown>>(`${this.base}/assignments/change`, request, this.options); }
  retry() { return this.http.post<ApiResponse<boolean>>(`${this.base}/reconciliation/retry`, {}, this.options); }
  dashboard() { return this.http.get<ApiResponse<DutyDashboard>>(`${this.base}/dashboard`,this.options); }
  administrators(){return this.http.get<ApiResponse<DutyAdministratorAccount[]>>(`${this.base}/administrators`,this.options);}
  changeAdministrator(userId:string,enabled:boolean,expectedVersion:string|null){return this.http.put<ApiResponse<DutyAdministratorAccount[]>>(`${this.base}/administrators/${encodeURIComponent(userId)}`,{enabled,expectedVersion},this.options);}
}
export interface DutyAdministratorAccount {userId:string;name:string;username?:string|null;roles:string[];eligible:boolean;inherited:boolean;enabled:boolean;version:string|null;}
export interface DutyDashboardDay { assignmentId:number;date:string;teacher:string;needsReview:boolean; }
export interface DutyDashboard { school:string;today:string;from:string;to:string;current:DutyDashboardDay|null;recent:DutyDashboardDay[]; }
