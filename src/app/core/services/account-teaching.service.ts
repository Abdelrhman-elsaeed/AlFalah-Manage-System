import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse } from '../models/api-response.model';
import { environment } from '../../../environments/environment';
import {
  TeacherTeaching,
  TeacherTeachingUpsertRequest
} from '../models/teacher.models';

/**
 * D-74 — Account-level service for the self-only endpoints.
 *
 *   GET /api/v1/account/teaching   → current user's teaching info
 *   PUT /api/v1/account/teaching   → current user updates their own Subject + Classes
 *
 * The HARD-SELF rule is enforced server-side: a teacher can only ever read
 * /edit their own teaching info via these endpoints. The manager path lives
 * on TeachersService (`/api/v1/teachers/{userId}/teaching`).
 *
 * The existing signature endpoint (`/api/v1/account/signature`) is still
 * reached via HttpClient directly inside account-settings.component.ts (it's
 * a single call; no need to migrate it here for this enhancement).
 */
@Injectable({ providedIn: 'root' })
export class AccountTeachingService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/v1/account`;

  /** GET /api/v1/account/teaching */
  getMyTeaching(): Observable<ApiResponse<TeacherTeaching>> {
    return this.http.get<ApiResponse<TeacherTeaching>>(`${this.base}/teaching`);
  }

  /** PUT /api/v1/account/teaching */
  updateMyTeaching(body: TeacherTeachingUpsertRequest): Observable<ApiResponse<TeacherTeaching>> {
    return this.http.put<ApiResponse<TeacherTeaching>>(`${this.base}/teaching`, body);
  }
}
