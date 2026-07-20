import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ApiResponse } from '../../../core/models/api-response.model';
import { AcademicYear, EvidenceCellFiles, EvidenceCellStatus, EvidenceMatrix, EvidenceMatrixFilter } from '../models/evidence-matrix.models';

@Injectable({ providedIn: 'root' })
export class EvidenceMatrixApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/api/v1/evidence-matrix`;

  academicYears(): Observable<AcademicYear[]> { return this.get<AcademicYear[]>('academic-years'); }
  matrix(filter: EvidenceMatrixFilter): Observable<EvidenceMatrix> { return this.get<EvidenceMatrix>('', this.params(filter)); }
  files(teacherId: number, taskId: number, academicYearId: number): Observable<EvidenceCellFiles> {
    return this.get<EvidenceCellFiles>(`cells/${teacherId}/${taskId}`, new HttpParams().set('academicYearId', academicYearId));
  }
  review(submissionId: number, reviewStatus: 3 | 4, note?: string): Observable<void> {
    return this.http.post<ApiResponse>(`${this.baseUrl}/submissions/${submissionId}/review`, { reviewStatus, note: note || null })
      .pipe(map(response => { if (!response.isSuccess) throw new Error(response.errors?.join(' ') || response.message); }));
  }
  export(format: 'excel' | 'pdf', filter: EvidenceMatrixFilter): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/export/${format}`, { params: this.params(filter), responseType: 'blob' });
  }

  private get<T>(path: string, params?: HttpParams): Observable<T> {
    const url = path ? `${this.baseUrl}/${path}` : this.baseUrl;
    return this.http.get<ApiResponse<T>>(url, { params }).pipe(map(response => {
      if (!response.isSuccess || response.data === null || response.data === undefined) throw new Error(response.errors?.join(' ') || response.message);
      return response.data;
    }));
  }
  private params(filter: EvidenceMatrixFilter): HttpParams {
    let params = new HttpParams();
    if (filter.schoolId) params = params.set('schoolId', filter.schoolId);
    if (filter.academicYearId) params = params.set('academicYearId', filter.academicYearId);
    if (filter.teacherId) params = params.set('teacherId', filter.teacherId);
    if (filter.category) params = params.set('category', filter.category);
    if (filter.completionStatus) params = params.set('completionStatus', filter.completionStatus);
    return params;
  }
}
