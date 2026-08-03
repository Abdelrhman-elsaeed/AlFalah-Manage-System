import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import {
  AnalyzeStudentRequest,
  StudentAnalyzerAnalysis,
  StudentAnalyzerCapabilities,
  StudentAnalyzerDelegate,
  StudentAnalyzerFile,
  StudentAnalyzerFilePage,
  StudentAnalyzerFileQuery,
  StudentAnalyzerModel,
  StudentAnalyzerProvider,
  StudentAnalyzerReportPage,
  StudentAnalyzerReportQuery,
  StudentAnalyzerSettings,
  UpdateStudentAnalyzerSettingsRequest
} from '../models/student-analyzer.models';

@Injectable({ providedIn: 'root' })
export class StudentAnalyzerService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/api/v1/student-analyzer`;
  private capabilitiesRequest?: Observable<ApiResponse<StudentAnalyzerCapabilities>>;

  capabilities(refresh = false): Observable<ApiResponse<StudentAnalyzerCapabilities>> {
    if (refresh || !this.capabilitiesRequest) {
      this.capabilitiesRequest = this.http
        .get<ApiResponse<StudentAnalyzerCapabilities>>(`${this.url}/capabilities`)
        .pipe(shareReplay({ bufferSize: 1, refCount: false }));
    }
    return this.capabilitiesRequest;
  }

  delegates(): Observable<ApiResponse<StudentAnalyzerDelegate[]>> {
    return this.http.get<ApiResponse<StudentAnalyzerDelegate[]>>(`${this.url}/delegates`);
  }

  updateDelegates(userIds: string[]): Observable<ApiResponse<StudentAnalyzerDelegate[]>> {
    return this.http.put<ApiResponse<StudentAnalyzerDelegate[]>>(`${this.url}/delegates`, { userIds });
  }

  settings(): Observable<ApiResponse<StudentAnalyzerSettings>> {
    return this.http.get<ApiResponse<StudentAnalyzerSettings>>(`${this.url}/settings`);
  }

  updateSettings(body: UpdateStudentAnalyzerSettingsRequest): Observable<ApiResponse<StudentAnalyzerSettings>> {
    return this.http.put<ApiResponse<StudentAnalyzerSettings>>(`${this.url}/settings`, body);
  }

  models(provider: StudentAnalyzerProvider, providerApiKey?: string): Observable<ApiResponse<StudentAnalyzerModel[]>> {
    return this.http.get<ApiResponse<StudentAnalyzerModel[]>>(`${this.url}/models`, {
      headers: providerApiKey
        ? new HttpHeaders({ 'X-Provider-Api-Key': providerApiKey })
        : undefined,
      params: { provider: String(provider) }
    });
  }

  upload(file: File): Observable<ApiResponse<StudentAnalyzerFile>> {
    const body = new FormData();
    body.append('file', file, file.name);
    return this.http.post<ApiResponse<StudentAnalyzerFile>>(`${this.url}/files`, body);
  }

  files(query: StudentAnalyzerFileQuery): Observable<ApiResponse<StudentAnalyzerFilePage>> {
    return this.http.get<ApiResponse<StudentAnalyzerFilePage>>(`${this.url}/files`, {
      params: this.params(query as unknown as Record<string, unknown>)
    });
  }

  fileContent(id: number): Observable<Blob> {
    return this.http.get(`${this.url}/files/${id}/content`, { responseType: 'blob' });
  }

  deleteFile(id: number): Observable<ApiResponse> {
    return this.http.delete<ApiResponse>(`${this.url}/files/${id}`);
  }

  analyze(body: AnalyzeStudentRequest): Observable<ApiResponse<StudentAnalyzerAnalysis>> {
    return this.http.post<ApiResponse<StudentAnalyzerAnalysis>>(`${this.url}/analyses`, body);
  }

  reports(query: StudentAnalyzerReportQuery): Observable<ApiResponse<StudentAnalyzerReportPage>> {
    return this.http.get<ApiResponse<StudentAnalyzerReportPage>>(`${this.url}/analyses`, {
      params: this.params(query as unknown as Record<string, unknown>)
    });
  }

  report(id: number): Observable<ApiResponse<StudentAnalyzerAnalysis>> {
    return this.http.get<ApiResponse<StudentAnalyzerAnalysis>>(`${this.url}/analyses/${id}`);
  }

  deleteReport(id: number): Observable<ApiResponse> {
    return this.http.delete<ApiResponse>(`${this.url}/analyses/${id}`);
  }

  private params(values: Record<string, unknown>): HttpParams {
    let result = new HttpParams();
    for (const [key, value] of Object.entries(values)) {
      if (value !== undefined && value !== null && value !== '') result = result.set(key, String(value));
    }
    return result;
  }
}
