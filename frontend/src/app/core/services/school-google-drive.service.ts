import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { ConfigureSchoolGoogleDriveRequest, SchoolGoogleDriveSettings } from '../models/school-google-drive.models';

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
}
