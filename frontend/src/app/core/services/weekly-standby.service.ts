import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { SUPPRESS_ERROR_TOAST } from '../http/http-context.tokens';
import { WeeklyStandbyWorkbench, WeeklyStandbyRequest, WeeklyStandbyPreview } from '../models/weekly-standby.models';

@Injectable({ providedIn: 'root' })
export class WeeklyStandbyService {
  private readonly http = inject(HttpClient);
  private url(id: number) { return `${environment.apiUrl}/api/v1/timetables/${id}/standby`; }
  private options() { return { context: new HttpContext().set(SUPPRESS_ERROR_TOAST, true) }; }
  get(id: number) { return this.http.get<ApiResponse<WeeklyStandbyWorkbench>>(this.url(id), this.options()); }
  preview(id: number, body: WeeklyStandbyRequest) { return this.http.post<ApiResponse<WeeklyStandbyPreview>>(`${this.url(id)}/preview`, body, this.options()); }
  apply(id: number, body: WeeklyStandbyRequest) { return this.http.put<ApiResponse<WeeklyStandbyWorkbench>>(this.url(id), body, this.options()); }
}
