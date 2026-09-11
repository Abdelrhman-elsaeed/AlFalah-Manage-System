import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { TeachingCell, TeachingOverview, TeachingTeacherPage } from '../models/teaching-assignment.models';
import { SUPPRESS_ERROR_TOAST } from '../http/http-context.tokens';

@Injectable({ providedIn: 'root' })
export class TeachingAssignmentService {
  private readonly http = inject(HttpClient);
  private readonly options = { context: new HttpContext().set(SUPPRESS_ERROR_TOAST, true) };
  private url(setup: number) { return `${environment.apiUrl}/api/v1/intelligent-timetable/settings/profiles/${setup}/assignments`; }
  get(setup: number) { return this.http.get<ApiResponse<TeachingOverview>>(this.url(setup), this.options); }
  save(setup: number, revision: number, changes: TeachingCell[]) {
    return this.http.put<ApiResponse<TeachingOverview>>(this.url(setup), { revision, changes }, this.options);
  }
  teachers(setup: number, params: Record<string, string | number | boolean>) {
    return this.http.get<ApiResponse<TeachingTeacherPage>>(`${this.url(setup)}/teachers`, { ...this.options, params });
  }
}
