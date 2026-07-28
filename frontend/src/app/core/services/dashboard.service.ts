import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpContext, HttpParams, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse } from '../models/api-response.model';
import { environment } from '../../../environments/environment';
import {
  DashboardFilter, DashboardRoleCode,
  MainManagerDashboard, SchoolManagerDashboard,
  ModeratorDashboard, InstructorDashboard
} from '../models/dashboard.models';
import { filenameFromContentDisposition } from './visits.service';
import { SUPPRESS_ERROR_TOAST } from '../http/http-context.tokens';

/**
 * Phase 9 — role-based dashboard aggregator. All methods are thin pass-throughs
 * to the corresponding backend endpoints; the server enforces the visibility
 * scope (ActiveSchoolId / D-37 / D-36 / Phase 8 Main-Manager-no-complaints).
 *
 * The export methods return the FULL HttpResponse so the caller can recover
 * the Content-Disposition filename and trigger a blob download.
 */
@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/v1/dashboard`;

  // ─── Read endpoints ──────────────────────────────────────────────────────

  getMainManager(filter: DashboardFilter = {}): Observable<ApiResponse<MainManagerDashboard>> {
    return this.http.get<ApiResponse<MainManagerDashboard>>(`${this.base}/main-manager`, {
      params: this.toParams(filter)
    });
  }

  getSchoolManager(filter: DashboardFilter = {}): Observable<ApiResponse<SchoolManagerDashboard>> {
    return this.http.get<ApiResponse<SchoolManagerDashboard>>(`${this.base}/school-manager`, {
      params: this.toParams(filter)
    });
  }

  getModerator(filter: DashboardFilter = {}): Observable<ApiResponse<ModeratorDashboard>> {
    return this.http.get<ApiResponse<ModeratorDashboard>>(`${this.base}/moderator`, {
      params: this.toParams(filter)
    });
  }

  getInstructor(filter: DashboardFilter = {}): Observable<ApiResponse<InstructorDashboard>> {
    return this.http.get<ApiResponse<InstructorDashboard>>(`${this.base}/instructor`, {
      params: this.toParams(filter)
    });
  }

  // ─── Export endpoints (server-side, scope-aware) ─────────────────────────

  exportExcel(role: DashboardRoleCode, filter: DashboardFilter = {}): Observable<HttpResponse<Blob>> {
    return this.http.get(`${this.base}/export/excel`, {
      params: this.toParams({ ...filter, role: String(role) }),
      responseType: 'blob',
      observe: 'response',
      // Failure detail lives inside the blob; the caller reads and reports it.
      context: new HttpContext().set(SUPPRESS_ERROR_TOAST, true)
    });
  }

  exportPdf(role: DashboardRoleCode, filter: DashboardFilter = {}): Observable<HttpResponse<Blob>> {
    return this.http.get(`${this.base}/export/pdf`, {
      params: this.toParams({ ...filter, role: String(role) }),
      responseType: 'blob',
      observe: 'response',
      // Failure detail lives inside the blob; the caller reads and reports it.
      context: new HttpContext().set(SUPPRESS_ERROR_TOAST, true)
    });
  }

  // ─── Helpers ─────────────────────────────────────────────────────────────

  private toParams(obj: Record<string, unknown> | DashboardFilter): HttpParams {
    let params = new HttpParams();
    for (const [k, v] of Object.entries(obj as Record<string, unknown>)) {
      if (v === null || v === undefined || v === '') continue;
      params = params.set(k, String(v));
    }
    return params;
  }
}

/**
 * Shared helper — same download-blob pattern used everywhere else in the app.
 * Returns the resolved filename (caller can set the link's `download` attr).
 */
export function downloadDashboardBlob(
  resp: HttpResponse<Blob>,
  fallbackFilename: string
): { ok: true; filename: string } | { ok: false; message: string } {
  const blob = resp.body;
  if (!blob) {
    return { ok: false, message: 'DASHBOARD.EXPORT_FAILED' };
  }
  const filename = filenameFromContentDisposition(resp.headers.get('Content-Disposition')) ?? fallbackFilename;
  const url = window.URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  setTimeout(() => window.URL.revokeObjectURL(url), 1000);
  return { ok: true, filename };
}
