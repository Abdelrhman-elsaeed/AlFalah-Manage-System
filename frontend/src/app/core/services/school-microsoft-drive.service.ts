import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { ConfigureSchoolMicrosoftDriveRequest, SchoolMicrosoftDriveSettings } from '../models/school-microsoft-drive.models';

@Injectable({ providedIn: 'root' })
export class SchoolMicrosoftDriveService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/api/v1/school-microsoft-drive`;
  get(): Observable<ApiResponse<SchoolMicrosoftDriveSettings>> { return this.http.get<ApiResponse<SchoolMicrosoftDriveSettings>>(this.url); }
  configure(body: ConfigureSchoolMicrosoftDriveRequest): Observable<ApiResponse<SchoolMicrosoftDriveSettings>> { return this.http.put<ApiResponse<SchoolMicrosoftDriveSettings>>(this.url, body); }
}
