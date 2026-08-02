import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpContext } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse } from '../models/api-response.model';
import { environment } from '../../../environments/environment';
import { DriveFolderMapping, UpsertDriveFolderMappingRequest } from '../models/teacher-drive-admin.models';
import { SUPPRESS_FORBIDDEN_REDIRECT } from '../http/http-context.tokens';

/**
 * Manager-only endpoints for granting/revoking a teacher's Google Drive evidence folder.
 * Keyed by InstructorProfile.Id (TeacherProfile.instructorProfileId), NOT by UserId.
 */
@Injectable({ providedIn: 'root' })
export class TeacherDriveAdminService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/v1/teacher-drive-admin/teachers`;

  /** GET .../folder — null data means no folder has been granted yet (not an error). */
  getFolder(teacherId: number): Observable<ApiResponse<DriveFolderMapping | null>> {
    return this.http.get<ApiResponse<DriveFolderMapping | null>>(`${this.base}/${teacherId}/folder`, {
      context: new HttpContext().set(SUPPRESS_FORBIDDEN_REDIRECT, true)
    });
  }

  /** PUT .../folder — creates or replaces the grant. */
  upsertFolder(teacherId: number, body: UpsertDriveFolderMappingRequest): Observable<ApiResponse<DriveFolderMapping>> {
    return this.http.put<ApiResponse<DriveFolderMapping>>(`${this.base}/${teacherId}/folder`, body);
  }

  /** DELETE .../folder — revokes access; already-uploaded evidence stays in the matrix. */
  revokeFolder(teacherId: number): Observable<ApiResponse> {
    return this.http.delete<ApiResponse>(`${this.base}/${teacherId}/folder`);
  }
}
