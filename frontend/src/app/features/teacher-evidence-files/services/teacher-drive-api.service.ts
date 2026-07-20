import { HttpClient, HttpContext, HttpEventType, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { from, Observable, switchMap, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ApiResponse } from '../../../core/models/api-response.model';
import { SKIP_LOCAL_AUTH, SUPPRESS_FORBIDDEN_REDIRECT } from '../../../core/http/http-context.tokens';
import { MicrosoftAuthService } from '../../../core/services/microsoft-auth.service';
import { DriveBreadcrumb, DriveItem, DriveItemsPage, EvidenceUploadCatalog, RecentFile, TeacherDriveStatus } from '../models/teacher-drive.models';

@Injectable({ providedIn: 'root' })
export class TeacherDriveApiService {
  private readonly http = inject(HttpClient);
  private readonly microsoftAuth = inject(MicrosoftAuthService);
  private readonly baseUrl = `${environment.apiUrl}/api/v1/teacher-drive`;

  status(): Observable<TeacherDriveStatus> { return this.get<TeacherDriveStatus>('status'); }
  linkAccount(): Observable<unknown> { return this.post<unknown>('link-account', null, true); }
  evidenceTasks(): Observable<EvidenceUploadCatalog> { return this.get<EvidenceUploadCatalog>('evidence-tasks'); }
  items(parentItemId?: string, search?: string): Observable<DriveItemsPage> {
    let params = new HttpParams();
    if (parentItemId) params = params.set('parentItemId', parentItemId);
    if (search) params = params.set('search', search);
    return this.get<DriveItemsPage>('items', params);
  }
  breadcrumb(itemId?: string): Observable<DriveBreadcrumb[]> { return this.get<DriveBreadcrumb[]>(`breadcrumb/${itemId ?? ''}`); }
  recent(): Observable<RecentFile[]> { return this.get<RecentFile[]>('recent-files'); }
  preview(itemId: string): Observable<{ previewUrl: string; webUrl: string; name: string }> { return this.get(`items/${itemId}/preview`); }

  upload(file: File, taskId: number, parentItemId?: string): Observable<number | DriveItem> {
    const body = new FormData();
    body.append('file', file);
    body.append('taskId', String(taskId));
    if (parentItemId) body.append('parentItemId', parentItemId);
    const requestId = globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(36).slice(2)}`;
    return this.withToken(false, token => this.http.post<ApiResponse<{ item: DriveItem }>>(`${this.baseUrl}/uploads`, body, {
      headers: new HttpHeaders({ Authorization: `Bearer ${token}`, 'Idempotency-Key': requestId }),
      reportProgress: true,
      observe: 'events',
      context: this.context()
    })).pipe(map(event => event.type === HttpEventType.UploadProgress
      ? Math.round(100 * event.loaded / (event.total || file.size))
      : event.type === HttpEventType.Response ? event.body?.data?.item ?? 0 : 0));
  }

  private get<T>(path: string, params?: HttpParams): Observable<T> {
    return this.withToken(false, token => this.http.get<ApiResponse<T>>(`${this.baseUrl}/${path}`, {
      params, headers: { Authorization: `Bearer ${token}` }, context: this.context()
    }).pipe(map(x => this.data(x))));
  }
  private post<T>(path: string, body: unknown, interactive = false): Observable<T> {
    return this.withToken(interactive, token => this.http.post<ApiResponse<T>>(`${this.baseUrl}/${path}`, body, {
      headers: { Authorization: `Bearer ${token}` }, context: this.context()
    }).pipe(map(x => this.data(x))));
  }
  private withToken<T>(interactive: boolean, call: (token: string) => Observable<T>): Observable<T> {
    return from(this.microsoftAuth.getApiToken(interactive)).pipe(switchMap(call));
  }
  private context(): HttpContext { return new HttpContext().set(SKIP_LOCAL_AUTH, true).set(SUPPRESS_FORBIDDEN_REDIRECT, true); }
  private data<T>(response: ApiResponse<T>): T {
    if (!response.isSuccess || response.data === undefined || response.data === null)
      throw new Error(response.errors?.join(' ') || response.message);
    return response.data;
  }
}
