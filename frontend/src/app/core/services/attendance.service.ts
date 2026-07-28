import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { AttendanceRecordItem, AttendanceSheet, MyAttendanceItem, SaveAttendanceSheetRequest } from '../models/attendance.models';
import { SUPPRESS_ERROR_TOAST } from '../http/http-context.tokens';

@Injectable({ providedIn: 'root' })
export class AttendanceService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/v1/attendance`;

  getSheet(date: string): Observable<ApiResponse<AttendanceSheet>> {
    return this.http.get<ApiResponse<AttendanceSheet>>(`${this.base}/sheet`, {
      params: new HttpParams().set('date', date)
    });
  }

  saveSheet(request: SaveAttendanceSheetRequest): Observable<ApiResponse<AttendanceSheet>> {
    return this.http.put<ApiResponse<AttendanceSheet>>(`${this.base}/sheet`, request);
  }

  getMine(fromDate?: string, toDate?: string): Observable<ApiResponse<MyAttendanceItem[]>> {
    let params = new HttpParams();
    if (fromDate) params = params.set('fromDate', fromDate);
    if (toDate) params = params.set('toDate', toDate);
    return this.http.get<ApiResponse<MyAttendanceItem[]>>(`${this.base}/me`, { params });
  }

  getRecords(fromDate?: string, toDate?: string, name?: string): Observable<ApiResponse<AttendanceRecordItem[]>> {
    let params = new HttpParams();
    if (fromDate) params = params.set('fromDate', fromDate);
    if (toDate) params = params.set('toDate', toDate);
    if (name?.trim()) params = params.set('name', name.trim());
    return this.http.get<ApiResponse<AttendanceRecordItem[]>>(`${this.base}/records`, { params });
  }

  downloadRecordsPdf(fromDate?: string, toDate?: string, name?: string): Observable<Blob> {
    let params = new HttpParams();
    if (fromDate) params = params.set('fromDate', fromDate);
    if (toDate) params = params.set('toDate', toDate);
    if (name?.trim()) params = params.set('name', name.trim());
    return this.http.get(`${this.base}/records/pdf`, {
      params,
      responseType: 'blob',
      context: new HttpContext().set(SUPPRESS_ERROR_TOAST, true)
    });
  }
}
