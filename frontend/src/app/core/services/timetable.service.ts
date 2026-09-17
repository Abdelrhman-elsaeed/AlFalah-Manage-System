import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SUPPRESS_ERROR_TOAST } from '../http/http-context.tokens';
import { ApiResponse } from '../models/api-response.model';
import {
  CreateTimetableRequest,
  SaveTimetableRequest,
  SchoolTimetable,
  TimetableCatalog,
  TimetableDay,
  TimetableEntryType,
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
    return this.http.get<ApiResponse<TimetableCatalog>>(`${this.base}/catalog`).pipe(
      map(response => response.data?.bellSchedule
        ? { ...response, data: { ...response.data, bellSchedule: normalizeBellSchedule(response.data.bellSchedule) } }
        : response)
    );
  }

  getCurrent(academicYearId: number, semester: TimetableSemester): Observable<ApiResponse<SchoolTimetable | null>> {
    const params = new HttpParams().set('academicYearId', academicYearId).set('semester', semester);
    return this.http.get<ApiResponse<SchoolTimetable | null>>(`${this.base}/current`, { params }).pipe(
      map(normalizeTimetableResponse)
    );
  }

  create(request: CreateTimetableRequest): Observable<ApiResponse<SchoolTimetable>> {
    return this.http.post<ApiResponse<SchoolTimetable>>(this.base, request).pipe(map(normalizeRequiredTimetableResponse));
  }

  save(id: number, request: SaveTimetableRequest): Observable<ApiResponse<SchoolTimetable>> {
    return this.http.put<ApiResponse<SchoolTimetable>>(`${this.base}/${id}`, request).pipe(map(normalizeRequiredTimetableResponse));
  }

  publish(id: number, revision: number): Observable<ApiResponse<SchoolTimetable>> {
    return this.http.post<ApiResponse<SchoolTimetable>>(`${this.base}/${id}/publish`, { revision }).pipe(map(normalizeRequiredTimetableResponse));
  }

  getVersions(id: number): Observable<ApiResponse<TimetableVersion[]>> {
    return this.http.get<ApiResponse<TimetableVersion[]>>(`${this.base}/${id}/versions`);
  }

  restore(id: number, versionNumber: number, revision: number): Observable<ApiResponse<SchoolTimetable>> {
    return this.http.post<ApiResponse<SchoolTimetable>>(`${this.base}/${id}/versions/${versionNumber}/restore`, { revision }).pipe(map(normalizeRequiredTimetableResponse));
  }

  updateGrants(moderatorUserIds: string[]): Observable<ApiResponse<TimetableModerator[]>> {
    return this.http.put<ApiResponse<TimetableModerator[]>>(`${this.base}/editor-grants`, { moderatorUserIds });
  }

  import(id: number, revision: number, file: File): Observable<ApiResponse<TimetableImportResult>> {
    const form = new FormData();
    form.append('file', file);
    form.append('revision', String(revision));
    return this.http.post<ApiResponse<TimetableImportResult>>(`${this.base}/${id}/import`, form).pipe(
      map(response => response.data
        ? { ...response, data: { ...response.data, timetable: normalizeTimetable(response.data.timetable) } }
        : response)
    );
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

const semesterByName: Readonly<Record<string, TimetableSemester>> = { First: 1, Second: 2 };
const dayByName: Readonly<Record<string, TimetableDay>> = {
  Saturday: 1,
  Sunday: 2,
  Monday: 3,
  Tuesday: 4,
  Wednesday: 5,
  Thursday: 6,
  Friday: 7
};
const entryTypeByName: Readonly<Record<string, TimetableEntryType>> = { Lesson: 1, Standby: 2 };

function normalizeEnum<T extends number>(value: unknown, names: Readonly<Record<string, T>>): T {
  if (typeof value === 'number') return value as T;
  if (typeof value === 'string' && names[value] !== undefined) return names[value];
  const parsed = Number(value);
  return parsed as T;
}

function normalizeBellSchedule(schedule: SchoolTimetable['bellSchedule']): NonNullable<SchoolTimetable['bellSchedule']> {
  return {
    ...schedule!,
    semester: normalizeEnum(schedule!.semester, semesterByName)
  };
}

function normalizeTimetable(timetable: SchoolTimetable): SchoolTimetable {
  return {
    ...timetable,
    semester: normalizeEnum(timetable.semester, semesterByName),
    bellSchedule: timetable.bellSchedule ? normalizeBellSchedule(timetable.bellSchedule) : undefined,
    entries: timetable.entries.map(entry => ({
      ...entry,
      day: normalizeEnum(entry.day, dayByName),
      entryType: normalizeEnum(entry.entryType, entryTypeByName)
    }))
  };
}

function normalizeTimetableResponse(response: ApiResponse<SchoolTimetable | null>): ApiResponse<SchoolTimetable | null> {
  return response.data ? { ...response, data: normalizeTimetable(response.data) } : response;
}

function normalizeRequiredTimetableResponse(response: ApiResponse<SchoolTimetable>): ApiResponse<SchoolTimetable> {
  return response.data ? { ...response, data: normalizeTimetable(response.data) } : response;
}
