import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { DutyChange, DutyPreview, DutySettings, DutyWorkspace } from '../models/school-duty.models';
@Injectable({ providedIn: 'root' })
export class SchoolDutyService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/v1/school-duty`;
  context(termId?: number | null) { return this.http.get<ApiResponse<DutyWorkspace>>(`${this.base}/context`, { params: termId ? new HttpParams().set('termId', termId) : new HttpParams() }); }
  preview(settings: DutySettings, expectedRevision: number) { return this.http.post<ApiResponse<DutyPreview>>(`${this.base}/plans/preview`, { settings, expectedRevision }); }
  publish(request: { settings: DutySettings; expectedRevision: number; previewDigest: string; requestId: string }) { return this.http.post<ApiResponse<{ planId: number; revision: number }>>(`${this.base}/plans/publish`, request); }
  changePreview(request: DutyChange) { return this.http.post<ApiResponse<DutyPreview>>(`${this.base}/assignments/change-preview`, request); }
  change(request: DutyChange) { return this.http.post<ApiResponse<unknown>>(`${this.base}/assignments/change`, request); }
  retry() { return this.http.post<ApiResponse<boolean>>(`${this.base}/reconciliation/retry`, {}); }
}
