import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SUPPRESS_ERROR_TOAST } from '../http/http-context.tokens';
import { ApiResponse } from '../models/api-response.model';
import {
  CreateTimetableRequest,
  SaveTimetableRequest,
  SchoolTimetable,
  TimetableCatalog,
  TimetableImportResult,
  TimetableModerator,
  TimetablePdfColorMode,
  TimetableSemester,
  TimetableVersion
} from '../models/timetable.models';

@Injectable({ providedIn: 'root' })
export class TimetableService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/v1/timetables`;

  getCatalog(): Observable<ApiResponse<TimetableCatalog>> {
    return this.http.get<ApiResponse<TimetableCatalog>>(`${this.base}/catalog`);
  }

  getCurrent(academicYearId: number, semester: TimetableSemester): Observable<ApiResponse<SchoolTimetable | null>> {
    const params = new HttpParams().set('academicYearId', academicYearId).set('semester', semester);
    return this.http.get<ApiResponse<SchoolTimetable | null>>(`${this.base}/current`, { params });
  }

  create(request: CreateTimetableRequest): Observable<ApiResponse<SchoolTimetable>> {
    return this.http.post<ApiResponse<SchoolTimetable>>(this.base, request);
  }

  save(id: number, request: SaveTimetableRequest): Observable<ApiResponse<SchoolTimetable>> {
    return this.http.put<ApiResponse<SchoolTimetable>>(`${this.base}/${id}`, request);
  }

  publish(id: number, revision: number): Observable<ApiResponse<SchoolTimetable>> {
    return this.http.post<ApiResponse<SchoolTimetable>>(`${this.base}/${id}/publish`, { revision });
  }

  getVersions(id: number): Observable<ApiResponse<TimetableVersion[]>> {
    return this.http.get<ApiResponse<TimetableVersion[]>>(`${this.base}/${id}/versions`);
  }

  restore(id: number, versionNumber: number, revision: number): Observable<ApiResponse<SchoolTimetable>> {
    return this.http.post<ApiResponse<SchoolTimetable>>(`${this.base}/${id}/versions/${versionNumber}/restore`, { revision });
  }

  updateGrants(moderatorUserIds: string[]): Observable<ApiResponse<TimetableModerator[]>> {
    return this.http.put<ApiResponse<TimetableModerator[]>>(`${this.base}/editor-grants`, { moderatorUserIds });
  }

  import(id: number, revision: number, file: File): Observable<ApiResponse<TimetableImportResult>> {
    const form = new FormData();
    form.append('file', file);
    form.append('revision', String(revision));
    return this.http.post<ApiResponse<TimetableImportResult>>(`${this.base}/${id}/import`, form);
  }

  downloadPdf(id: number, colorMode: TimetablePdfColorMode): Observable<Blob> {
    const params = new HttpParams().set('colorMode', colorMode);
    return this.download(`${this.base}/${id}/pdf`, params);
  }

  downloadTemplate(id: number): Observable<Blob> {
    return this.download(`${this.base}/${id}/import-template`);
  }

  private download(url: string, params?: HttpParams): Observable<Blob> {
    return this.http.get(url, {
      params,
      responseType: 'blob',
      context: new HttpContext().set(SUPPRESS_ERROR_TOAST, true)
    });
  }
}
