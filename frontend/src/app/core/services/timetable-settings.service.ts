import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SUPPRESS_ERROR_TOAST } from '../http/http-context.tokens';
import { ApiResponse } from '../models/api-response.model';
import {
  CreateTimetableAcademicYearRequest,
  CreateTimetableSetupProfileRequest,
  TimetableSettingsOverview,
  TimetableSetupAcademicYear,
  TimetableSetupProfile,
  UpdateTimetableSetupProfileRequest
} from '../models/timetable-settings.models';

@Injectable({ providedIn: 'root' })
export class TimetableSettingsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/api/v1/intelligent-timetable/settings`;
  private readonly context = new HttpContext().set(SUPPRESS_ERROR_TOAST, true);
  private rememberedContext: { academicYearId: number; semester: number } | null = null;

  getOverview(academicYearId?: number, semester?: number, profileId?: number): Observable<ApiResponse<TimetableSettingsOverview>> {
    let params = new HttpParams();
    if (academicYearId) params = params.set('academicYearId', academicYearId);
    if (semester) params = params.set('semester', semester);
    if (profileId) params = params.set('profileId', profileId);
    return this.http.get<ApiResponse<TimetableSettingsOverview>>(this.baseUrl, { params, context: this.context }).pipe(
      tap(response => {
        if (!response.data?.selectedAcademicYearId) return;
        this.rememberedContext = {
          academicYearId: response.data.selectedAcademicYearId,
          semester: response.data.selectedSemester
        };
      })
    );
  }

  getRememberedContext(): Readonly<{ academicYearId: number; semester: number }> | null {
    return this.rememberedContext;
  }

  createAcademicYear(request: CreateTimetableAcademicYearRequest): Observable<ApiResponse<TimetableSetupAcademicYear>> {
    return this.http.post<ApiResponse<TimetableSetupAcademicYear>>(
      `${this.baseUrl}/academic-years`,
      request,
      { context: this.context });
  }

  createProfile(request: CreateTimetableSetupProfileRequest): Observable<ApiResponse<TimetableSetupProfile>> {
    return this.http.post<ApiResponse<TimetableSetupProfile>>(`${this.baseUrl}/profiles`, request, { context: this.context });
  }

  updateProfile(id: number, request: UpdateTimetableSetupProfileRequest): Observable<ApiResponse<TimetableSetupProfile>> {
    return this.http.put<ApiResponse<TimetableSetupProfile>>(`${this.baseUrl}/profiles/${id}`, request, { context: this.context });
  }
}
