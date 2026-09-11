import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { AvailabilityTeacher, TeacherAvailability, UpdateTeacherProfile } from '../models/teacher-availability.models';

@Injectable({ providedIn: 'root' })
export class TeacherAvailabilityService {
  private readonly http = inject(HttpClient);
  private url(setupId: number) { return `${environment.apiUrl}/api/v1/intelligent-timetable/settings/profiles/${setupId}/teachers`; }
  list(setupId: number) { return this.http.get<ApiResponse<AvailabilityTeacher[]>>(this.url(setupId)); }
  get(setupId: number, teacherId: number) { return this.http.get<ApiResponse<TeacherAvailability>>(`${this.url(setupId)}/${teacherId}`); }
  save(setupId: number, teacherId: number, request: UpdateTeacherProfile) {
    return this.http.put<ApiResponse<TeacherAvailability>>(`${this.url(setupId)}/${teacherId}`, request);
  }
}
