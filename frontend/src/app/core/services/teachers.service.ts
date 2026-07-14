import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse } from '../models/api-response.model';
import { environment } from '../../../environments/environment';
import {
  TeacherListItem,
  TeacherListPage,
  TeacherListQuery,
  TeacherProfile,
  TeacherProgress,
  TeacherTeaching,
  TeacherTeachingUpsertRequest,
  TeacherVisitSummary
} from '../models/teacher.models';
import { SUPPRESS_FORBIDDEN_REDIRECT } from '../http/http-context.tokens';

/**
 * D-71 — Teachers Management + Teacher Profile service (frontend mirror of
 * the backend ITeacherService).
 *
 * Reuses the existing user-create / user-update / user-deactivate endpoints
 * (POST /users, PUT /users/{id}, POST /users/{id}/deactivate) with
 * role=Instructor + schoolId=ActiveSchoolId — no parallel Teacher write
 * endpoints exist on purpose (per docs/03 permissions + school-scope).
 *
 * D-74 — adds the teaching-info endpoints (Subject + Classes) used by the
 * account-settings "مادتي وفصولي" section + the visit-form auto-fill.
 */
@Injectable({ providedIn: 'root' })
export class TeachersService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/v1/teachers`;

  /** GET /api/v1/teachers — paged list (Users.View scoped). */
  list(query: TeacherListQuery = {}): Observable<ApiResponse<TeacherListPage>> {
    let params = new HttpParams()
      .set('page', String(query.page ?? 1))
      .set('pageSize', String(query.pageSize ?? 20));
    if (query.search) params = params.set('search', query.search);
    return this.http.get<ApiResponse<TeacherListPage>>(this.base, { params });
  }

  /** GET /api/v1/teachers/{userId} — profile header (Users.View scoped). */
  getProfile(userId: string): Observable<ApiResponse<TeacherProfile>> {
    return this.http.get<ApiResponse<TeacherProfile>>(`${this.base}/${userId}`);
  }

  /** GET /api/v1/teachers/{userId}/visits — visits (Visits.View scoped, D-37 enforced server-side). */
  getVisits(userId: string): Observable<ApiResponse<TeacherVisitSummary[]>> {
    return this.http.get<ApiResponse<TeacherVisitSummary[]>>(`${this.base}/${userId}/visits`);
  }

  /** GET /api/v1/teachers/{userId}/progress — radar chart payload (Visits.View scoped). */
  getProgress(userId: string): Observable<ApiResponse<TeacherProgress>> {
    return this.http.get<ApiResponse<TeacherProgress>>(`${this.base}/${userId}/progress`);
  }

  /** GET /api/v1/teachers/{userId}/teaching — Subject + Classes for auto-fill + manager edit (Users.View scoped). */
  getTeaching(userId: string, suppressForbiddenRedirect = false): Observable<ApiResponse<TeacherTeaching>> {
    const context = suppressForbiddenRedirect
      ? new HttpContext().set(SUPPRESS_FORBIDDEN_REDIRECT, true)
      : undefined;
    return this.http.get<ApiResponse<TeacherTeaching>>(`${this.base}/${userId}/teaching`, { context });
  }

  /** PUT /api/v1/teachers/{userId}/teaching — manager sets a teacher's Subject + Classes (Users.Edit scoped). */
  updateTeaching(userId: string, body: TeacherTeachingUpsertRequest): Observable<ApiResponse<TeacherTeaching>> {
    return this.http.put<ApiResponse<TeacherTeaching>>(`${this.base}/${userId}/teaching`, body);
  }
}
