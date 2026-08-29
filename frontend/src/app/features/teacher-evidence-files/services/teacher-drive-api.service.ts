import { HttpClient, HttpContext, HttpEventType, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ApiResponse } from '../../../core/models/api-response.model';
import { SUPPRESS_FORBIDDEN_REDIRECT } from '../../../core/http/http-context.tokens';
import { DriveBreadcrumb, DriveItem, DriveItemsPage, EvidenceUploadCatalog, RecentFile, TeacherDriveStatus } from '../models/teacher-drive.models';

/**
 * Talks to the teacher-drive API using the ordinary application session — AuthInterceptor
 * attaches the bearer token, exactly as for every other feature. The previous Microsoft Entra
 * token dance is gone: Google Drive is reached server-side with the school's credential.
 */
@Injectable({ providedIn: 'root' })
export class TeacherDriveApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/api/v1/teacher-drive`;

  status(): Observable<TeacherDriveStatus> { return this.get<TeacherDriveStatus>('status'); }
  evidenceTasks(): Observable<EvidenceUploadCatalog> { return this.get<EvidenceUploadCatalog>('evidence-tasks'); }

  items(parentItemId?: string, search?: string): Observable<DriveItemsPage> {
    let params = new HttpParams();
    if (parentItemId) params = params.set('parentItemId', parentItemId);
    if (search) params = params.set('search', search);
    return this.get<DriveItemsPage>('items', params);
  }

  breadcrumb(itemId?: string): Observable<DriveBreadcrumb[]> { return this.get<DriveBreadcrumb[]>(`breadcrumb/${itemId ?? ''}`); }
  recent(): Observable<RecentFile[]> { return this.get<RecentFile[]>('recent-files'); }

  /**
   * Fetches the file as a blob rather than linking to Drive. The files belong to the school's
   * Google account and the teacher has no Google session, so a Drive link would only ever
   * show "Request access" — and a plain <a href> could not carry our bearer token anyway.
   */
  content(itemId: string): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/items/${encodeURIComponent(itemId)}/content`, {
      responseType: 'blob',
      context: this.context()
    });
  }

  upload(file: File, taskId: number, parentItemId?: string): Observable<number | DriveItem> {
    const body = new FormData();
    body.append('file', file);
    body.append('taskId', String(taskId));
    if (parentItemId) body.append('parentItemId', parentItemId);
    // A stable per-attempt key: if the request is retried the server recognises it and
    // returns the original submission instead of uploading the file twice.
    const requestId = globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(36).slice(2)}`;
    return this.http.post<ApiResponse<{ submissionId: number; item: DriveItem }>>(`${this.baseUrl}/uploads`, body, {
      headers: new HttpHeaders({ 'Idempotency-Key': requestId }),
      reportProgress: true,
      observe: 'events',
      context: this.context()
    }).pipe(map(event => event.type === HttpEventType.UploadProgress
      ? Math.round(100 * event.loaded / (event.total || file.size))
      : event.type === HttpEventType.Response && event.body?.data
        ? { ...event.body.data.item, submissionId: event.body.data.submissionId }
        : 0));
  }

  rename(submissionId: number, name: string): Observable<DriveItem> {
    return this.http.patch<ApiResponse<DriveItem>>(
      `${this.baseUrl}/submissions/${submissionId}/name`, { name }, { context: this.context() }
    ).pipe(map(response => this.data(response)));
  }

  deleteSubmission(submissionId: number): Observable<void> {
    return this.http.delete<ApiResponse<unknown>>(
      `${this.baseUrl}/submissions/${submissionId}`, { context: this.context() }
    ).pipe(map(response => {
      if (!response.isSuccess) throw new Error(response.errors?.join(' ') || response.message);
    }));
  }

  private get<T>(path: string, params?: HttpParams): Observable<T> {
    return this.http.get<ApiResponse<T>>(`${this.baseUrl}/${path}`, { params, context: this.context() })
      .pipe(map(response => this.data(response)));
  }

  /**
   * A teacher without a granted folder legitimately gets 403 here, and that must render as an
   * inline explanation on this page rather than bouncing them to the Unauthorized screen.
   */
  private context(): HttpContext { return new HttpContext().set(SUPPRESS_FORBIDDEN_REDIRECT, true); }

  private data<T>(response: ApiResponse<T>): T {
    if (!response.isSuccess || response.data === undefined || response.data === null)
      throw new Error(response.errors?.join(' ') || response.message);
    return response.data;
  }
}
