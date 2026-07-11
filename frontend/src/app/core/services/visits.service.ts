import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse } from '../models/api-response.model';
import { environment } from '../../../environments/environment';
import {
  CreateVisitRequest,
  InstructorReport,
  RejectVisitRequest,
  ReopenVisitRequest,
  ReportViewStatus,
  UpdateVisitRequest,
  VisitAnalysis,
  VisitDetail,
  VisitListItem,
  VisitListQuery
} from '../models/visit.models';
import { PagedResult } from '../models/phase2.models';

@Injectable({ providedIn: 'root' })
export class VisitsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/v1/visits`;

  /** GET /api/v1/visits — paged list */
  list(query: VisitListQuery = {}): Observable<ApiResponse<PagedResult<VisitListItem>>> {
    let params = new HttpParams()
      .set('page', String(query.page ?? 1))
      .set('pageSize', String(query.pageSize ?? 20));
    if (query.status !== undefined) params = params.set('status', String(query.status));
    if (query.instructorId) params = params.set('instructorId', query.instructorId);
    if (query.visitCategory !== undefined) params = params.set('visitCategory', String(query.visitCategory));
    if (query.fromDate) params = params.set('fromDate', query.fromDate);
    if (query.toDate) params = params.set('toDate', query.toDate);
    if (query.sortBy) params = params.set('sortBy', query.sortBy);
    if (query.sortDesc) params = params.set('sortDesc', String(query.sortDesc));

    return this.http.get<ApiResponse<PagedResult<VisitListItem>>>(this.base, { params });
  }

  /** GET /api/v1/visits/:id — full detail */
  getById(id: number): Observable<ApiResponse<VisitDetail>> {
    return this.http.get<ApiResponse<VisitDetail>>(`${this.base}/${id}`);
  }

  /** POST /api/v1/visits — create draft */
  create(body: CreateVisitRequest): Observable<ApiResponse<VisitDetail>> {
    return this.http.post<ApiResponse<VisitDetail>>(this.base, body);
  }

  /** PUT /api/v1/visits/:id — update draft (Phase 4) + edit-after-reject / edit-after-reopen (Phase 5) */
  update(id: number, body: UpdateVisitRequest): Observable<ApiResponse<VisitDetail>> {
    return this.http.put<ApiResponse<VisitDetail>>(`${this.base}/${id}`, body);
  }

  /** POST /api/v1/visits/:id/submit — Draft → PendingApproval + persist analysis (Phase 4);
   *  also handles Reopened → PendingApproval (recompute, Phase 5). */
  submit(id: number): Observable<ApiResponse<VisitDetail>> {
    return this.http.post<ApiResponse<VisitDetail>>(`${this.base}/${id}/submit`, {});
  }

  /** DELETE /api/v1/visits/:id — soft delete (Draft only) */
  softDelete(id: number): Observable<ApiResponse<void>> {
    return this.http.delete<ApiResponse<void>>(`${this.base}/${id}`);
  }

  /** GET /api/v1/visits/:id/analysis — analysis snapshot (404 if not submitted) */
  getAnalysis(id: number): Observable<ApiResponse<VisitAnalysis>> {
    return this.http.get<ApiResponse<VisitAnalysis>>(`${this.base}/${id}/analysis`);
  }

  // ─── Phase 5: approval workflow + instructor visibility ──────────────────

  /** POST /api/v1/visits/:id/approve — PendingApproval → Approved (School Manager). */
  approve(id: number): Observable<ApiResponse<VisitDetail>> {
    return this.http.post<ApiResponse<VisitDetail>>(`${this.base}/${id}/approve`, {});
  }

  /** POST /api/v1/visits/:id/reject — PendingApproval → RejectedForChanges (reason required). */
  reject(id: number, body: RejectVisitRequest): Observable<ApiResponse<VisitDetail>> {
    return this.http.post<ApiResponse<VisitDetail>>(`${this.base}/${id}/reject`, body);
  }

  /** POST /api/v1/visits/:id/reopen — Approved → Reopened (reason required). */
  reopen(id: number, body: ReopenVisitRequest): Observable<ApiResponse<VisitDetail>> {
    return this.http.post<ApiResponse<VisitDetail>>(`${this.base}/${id}/reopen`, body);
  }

  /** GET /api/v1/visits/:id/report — Instructor-only full result (approved + own visit). Records a view log. */
  getInstructorReport(id: number): Observable<ApiResponse<InstructorReport>> {
    return this.http.get<ApiResponse<InstructorReport>>(`${this.base}/${id}/report`);
  }

  /** GET /api/v1/visits/:id/view-status — aggregated view status for manager / moderator. */
  getReportViewStatus(id: number): Observable<ApiResponse<ReportViewStatus>> {
    return this.http.get<ApiResponse<ReportViewStatus>>(`${this.base}/${id}/view-status`);
  }

  /**
   * Phase 6 / Stage 1: GET /api/v1/visits/:id/report/pdf — server-side Arabic PDF download.
   * Returns the full HttpResponse so the caller can recover the Content-Disposition
   * filename (D-41: filename pattern is `{teacher} - {year} - {visitType}.pdf`).
   * The backend stamps a "مسودة — غير معتمدة" watermark on non-Approved visits
   * (D-41: relaxed approved-only PDF rule, visibility gates intact).
   */
  downloadReportPdf(id: number): Observable<HttpResponse<Blob>> {
    return this.http.get(`${this.base}/${id}/report/pdf`, {
      responseType: 'blob',
      observe: 'response'
    });
  }

  /**
   * D-41 / Task 6: GET /api/v1/visits/export/zip — bulk export. Backend applies the
   * same scoped query the list endpoint uses (school-scope + moderator own-only)
   * and packages one PDF per visit into a single ZIP returned as
   * `application/zip`.
   */
  exportAllZip(query: VisitListQuery = {}): Observable<HttpResponse<Blob>> {
    let params = new HttpParams();
    if (query.status !== undefined) params = params.set('status', String(query.status));
    if (query.instructorId) params = params.set('instructorId', query.instructorId);
    if (query.visitCategory !== undefined) params = params.set('visitCategory', String(query.visitCategory));
    if (query.fromDate) params = params.set('fromDate', query.fromDate);
    if (query.toDate) params = params.set('toDate', query.toDate);

    return this.http.get(`${this.base}/export/zip`, {
      params,
      responseType: 'blob',
      observe: 'response'
    });
  }

  /**
   * D-41 / Task 7 — best-effort client-side filename helper, used as a fallback
   * when the backend's Content-Disposition header is not available (e.g. the
   * browser already consumed it). Mirrors the backend pattern so the same file
   * is produced whether the filename comes from the header or this helper.
   */
  suggestedPdfFilename(item: VisitListItem): string {
    const teacher = sanitizeForFilename(item.instructorFullName);
    const year = new Date(item.visitDate).getFullYear();
    const category = sanitizeForFilename(item.visitCategoryLabelAr);
    return `${teacher} - ${year} - ${category}.pdf`;
  }
}

/**
 * D-41 / Task 7 — sanitizes an Arabic string for use as a filesystem filename.
 * Removes filesystem-illegal characters and control bytes while preserving
 * Arabic letters / digits / spaces / hyphens. Returns "ملف" as a safe fallback
 * when the input is empty / all illegal.
 */
function sanitizeForFilename(input: string | null | undefined): string {
  if (!input) return 'ملف';
  let s = input
    // Remove filesystem-illegal characters on Windows + Unix
    .replace(/[\\/:*?"<>|\u0000-\u001F]/g, '')
    // Collapse runs of whitespace into a single space, then trim
    .replace(/\s+/g, ' ')
    .trim();
  if (!s) return 'ملف';
  // Cap length to keep filenames portable (NTFS limit is 255 chars; we leave headroom)
  if (s.length > 80) s = s.substring(0, 80).trim();
  return s;
}

/**
 * D-41 — extracts the filename from a Content-Disposition response header.
 * Returns null if absent / unparseable so the caller can fall back to its
 * own naming convention.
 */
export function filenameFromContentDisposition(header: string | null | undefined): string | null {
  if (!header) return null;
  const m = /filename\*?=(?:UTF-8'')?["']?([^"';]+)["']?/i.exec(header);
  if (!m) return null;
  let name = m[1].trim();
  try {
    name = decodeURIComponent(name);
  } catch {
    // not url-encoded
  }
  return name;
}