import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { BellSchedule, SaveBellSchedule } from '../models/bell-schedule.models';

@Injectable({ providedIn: 'root' })
export class BellScheduleService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/api/v1/intelligent-timetable/timings`;
  list(academicYearId: number, semester: number) {
    return this.http.get<ApiResponse<BellSchedule[]>>(this.url, { params: { academicYearId, semester } });
  }
  save(id: number | null, request: SaveBellSchedule) {
    return id ? this.http.put<ApiResponse<BellSchedule>>(`${this.url}/${id}`, request)
      : this.http.post<ApiResponse<BellSchedule>>(this.url, request);
  }
  select(profileId: number, templateId: number, profileRevision: number) {
    return this.http.put<ApiResponse<number>>(`${this.url}/profiles/${profileId}/selection`, { templateId, profileRevision });
  }
}
