import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResult } from '../models/api-response.model';
import { CoverageAction, CoverageApproval, CoverageBoard, CoverageNeed, CoverageOption, CoveragePreview, CoveragePreviewRequest, CoverageTask } from '../models/daily-coverage.models';
import { SUPPRESS_ERROR_TOAST } from '../http/http-context.tokens';

@Injectable({ providedIn: 'root' })
export class DailyCoverageService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/v1/timetable-coverage`;
  private context() { return new HttpContext().set(SUPPRESS_ERROR_TOAST, true); }
  board(date: string) { return this.http.get<ApiResponse<CoverageBoard>>(`${this.base}/board`, { params: { date }, context: this.context() }); }
  reconcile(date: string) { return this.http.post<ApiResponse<CoverageBoard>>(`${this.base}/reconcile`, null, { params: { date }, context: this.context() }); }
  options(id: number) { return this.http.get<ApiResponse<CoverageOption[]>>(`${this.base}/needs/${id}/options`, { context: this.context() }); }
  preview(body: CoveragePreviewRequest) { return this.http.post<ApiResponse<CoveragePreview>>(`${this.base}/preview`, body, { context: this.context() }); }
  approve(body: CoverageApproval) { return this.http.post<ApiResponse<CoverageBoard>>(`${this.base}/approve`, body, { context: this.context() }); }
  tasks(page = 1, pageSize = 20) { return this.http.get<ApiResponse<PagedResult<CoverageNeed>>>(`${this.base}/my-tasks`, { params: { page, pageSize }, context: this.context() }); }
  task(id: number) { return this.http.get<ApiResponse<CoverageTask>>(`${this.base}/tasks/${id}`, { context: this.context() }); }
  draft(id: number, body: { revision: number; substitutionId: number; narrative: string; notes: string }) { return this.http.put<ApiResponse<CoverageTask>>(`${this.base}/tasks/${id}/draft`, body, { context: this.context() }); }
  act(id: number, action: 'acknowledge' | 'submit' | 'unable' | 'reopen' | 'keep' | 'release', body: CoverageAction) {
    const area = ['reopen', 'keep', 'release'].includes(action) ? 'needs' : 'tasks';
    return this.http.post<ApiResponse<CoverageTask>>(`${this.base}/${area}/${id}/${action}`, body, { context: this.context() });
  }
}
