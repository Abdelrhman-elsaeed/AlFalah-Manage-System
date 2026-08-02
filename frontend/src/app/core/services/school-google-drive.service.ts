import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { ConfigureSchoolGoogleDriveRequest, GoogleAuthUrl, SchoolGoogleDriveSettings } from '../models/school-google-drive.models';

@Injectable({ providedIn: 'root' })
export class SchoolGoogleDriveService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/api/v1/school-google-drive`;

  get(): Observable<ApiResponse<SchoolGoogleDriveSettings>> {
    return this.http.get<ApiResponse<SchoolGoogleDriveSettings>>(this.url);
  }

  configure(body: ConfigureSchoolGoogleDriveRequest): Observable<ApiResponse<SchoolGoogleDriveSettings>> {
    return this.http.put<ApiResponse<SchoolGoogleDriveSettings>>(this.url, body);
  }

  /**
   * Asks the server for this school's Google consent URL. The caller must then perform a
   * top-level navigation to it — Google's consent screen cannot be fetched cross-origin or
   * shown in an iframe, so opening it via HttpClient would only ever fail on CORS.
   */
  authUrl(): Observable<ApiResponse<GoogleAuthUrl>> {
    return this.http.get<ApiResponse<GoogleAuthUrl>>(`${this.url}/auth-url`);
  }
}
