import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpContext } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { SUPPRESS_ERROR_TOAST, SUPPRESS_FORBIDDEN_REDIRECT } from '../http/http-context.tokens';
import { ConfigureSchoolGoogleDriveRequest, GoogleAuthUrl, SchoolGoogleDriveSettings, SchoolDriveFolderPage } from '../models/school-google-drive.models';

@Injectable({ providedIn: 'root' })
export class SchoolGoogleDriveService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/api/v1/school-google-drive`;
  private context(): HttpContext { return new HttpContext().set(SUPPRESS_ERROR_TOAST, true).set(SUPPRESS_FORBIDDEN_REDIRECT, true); }

  get(): Observable<ApiResponse<SchoolGoogleDriveSettings>> {
    return this.http.get<ApiResponse<SchoolGoogleDriveSettings>>(this.url, { context: this.context() });
  }

  configure(body: ConfigureSchoolGoogleDriveRequest): Observable<ApiResponse<SchoolGoogleDriveSettings>> {
    return this.http.put<ApiResponse<SchoolGoogleDriveSettings>>(this.url, body, { context: this.context() });
  }

  /**
   * Asks the server for this school's Google consent URL. The caller must then perform a
   * top-level navigation to it — Google's consent screen cannot be fetched cross-origin or
   * shown in an iframe, so opening it via HttpClient would only ever fail on CORS.
   */
  authUrl(): Observable<ApiResponse<GoogleAuthUrl>> {
    return this.http.get<ApiResponse<GoogleAuthUrl>>(`${this.url}/auth-url`, { context: this.context() });
  }

  folders(parentItemId?: string, pageToken?: string, search?: string): Observable<ApiResponse<SchoolDriveFolderPage>> {
    const params: Record<string, string> = {};
    if (parentItemId) params['parentItemId'] = parentItemId;
    if (pageToken) params['pageToken'] = pageToken;
    if (search?.trim()) params['search'] = search.trim();
    return this.http.get<ApiResponse<SchoolDriveFolderPage>>(`${this.url}/folders`, { params, context: this.context() });
  }
}
