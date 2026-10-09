import { HttpClient, HttpContext, HttpEvent, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { catchError, map, Observable, shareReplay, tap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../../core/models/api-response.model';
import { SUPPRESS_FORBIDDEN_REDIRECT, SUPPRESS_ERROR_TOAST } from '../../core/http/http-context.tokens';
import { StorageAccess, StorageContext, StorageDetails, StorageFile, StorageFolder, StoragePage, StorageUpload, StorageDiscovery } from './storage.models';
import { AuthService } from '../../core/services/auth.service';

@Injectable({ providedIn: 'root' })
export class StorageApiService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly base = `${environment.apiUrl}/api/v1/storage`;
  private readonly contextCache = new Map<string, { key: string; until: number; request: Observable<StorageContext> }>();
  private readonly accessCache = new Map<boolean, { key: string; until: number; request: Observable<StorageAccess> }>();
  private readonly libraryCache = new Map<string, { files: StoragePage<StorageFile>; folders: StoragePage<StorageFolder> }>();
  private readonly detailsCache = new Map<string, { until: number; details: StorageDetails }>();
  private libraryKey(own: boolean, folderId: number, search: string, global: boolean, sort: string, descending: boolean, page: number, filter: string) {
    const actor = this.auth.currentUser();
    return JSON.stringify([actor?.userId, actor?.activeSchoolId, this.auth.getAccessToken(), own, folderId, search, global, sort, descending, page, filter]);
  }
  private detailsKey(id: number, academicYearId?: number, folderId?: number, own?: boolean) {
    const actor = this.auth.currentUser();
    return JSON.stringify([actor?.userId, actor?.activeSchoolId, this.auth.getAccessToken(), id, academicYearId, folderId, own]);
  }
  private context() { return new HttpContext().set(SUPPRESS_FORBIDDEN_REDIRECT, true).set(SUPPRESS_ERROR_TOAST, true); }
  private data<T>(response: ApiResponse<T>): T {
    if (!response.isSuccess || response.data == null) throw new Error(response.errors?.join(' ') || response.message);
    return response.data;
  }
  accessInfo(own: boolean, refresh = false): Observable<StorageAccess> {
    const actor = this.auth.currentUser();
    const key = `${actor?.userId ?? ''}|${actor?.activeSchoolId ?? ''}|${this.auth.getAccessToken() ?? ''}`;
    const cached = this.accessCache.get(own);
    if (!refresh && cached?.key === key && cached.until > Date.now()) return cached.request;
    const request = this.http.get<ApiResponse<StorageAccess>>(`${this.base}/access`, { params: { own }, context: this.context() }).pipe(
      map(r => this.data(r)),
      catchError(error => { if (this.accessCache.get(own)?.request === request) this.accessCache.delete(own); return throwError(() => error); }),
      shareReplay({ bufferSize: 1, refCount: false })
    );
    this.accessCache.set(own, { key, until: Date.now() + 60_000, request });
    return request;
  }
  contextInfo(own: boolean, refresh = false, detailed = false): Observable<StorageContext> {
    const actor = this.auth.currentUser();
    const key = `${actor?.userId ?? ''}|${actor?.activeSchoolId ?? ''}|${this.auth.getAccessToken() ?? ''}`;
    const slot = `${own}:${detailed}`;
    const cached = this.contextCache.get(slot);
    if (!refresh && cached?.key === key && cached.until > Date.now()) return cached.request;
    const request = this.http.get<ApiResponse<StorageContext>>(`${this.base}/context`, { params: { own, fast: !detailed }, context: this.context() }).pipe(
      map(r => this.data(r)),
      catchError(error => { if (this.contextCache.get(slot)?.request === request) this.contextCache.delete(slot); return throwError(() => error); }),
      shareReplay({ bufferSize: 1, refCount: false })
    );
    this.contextCache.set(slot, { key, until: Date.now() + 60_000, request });
    return request;
  }
  invalidateContext() { this.contextCache.clear(); this.accessCache.clear(); this.libraryCache.clear(); this.detailsCache.clear(); }
  peekDetails(id: number, academicYearId?: number, folderId?: number, own?: boolean) {
    const cached = this.detailsCache.get(this.detailsKey(id, academicYearId, folderId, own));
    return cached && cached.until > Date.now() ? cached.details : undefined;
  }
  invalidateDetails() { this.detailsCache.clear(); }
  peekLibrary(own: boolean, folderId: number, search: string, global: boolean, sort: string, descending: boolean, page: number, filter: string) {
    return this.libraryCache.get(this.libraryKey(own, folderId, search, global, sort, descending, page, filter));
  }
  rememberLibrary(own: boolean, folderId: number, search: string, global: boolean, sort: string, descending: boolean, page: number, filter: string,
    files: StoragePage<StorageFile>, folders: StoragePage<StorageFolder>) {
    const key = this.libraryKey(own, folderId, search, global, sort, descending, page, filter);
    this.libraryCache.delete(key);
    this.libraryCache.set(key, { files, folders });
    if (this.libraryCache.size > 30) this.libraryCache.delete(this.libraryCache.keys().next().value!);
  }
  folders(own: boolean, parentFolderId?: number, page = 1) {
    let params = new HttpParams().set('own', own).set('page', page).set('pageSize', 25);
    if (parentFolderId != null) params = params.set('parentFolderId', parentFolderId);
    return this.http.get<ApiResponse<StoragePage<StorageFolder>>>(`${this.base}/folders`, { params, context: this.context() }).pipe(map(r => this.data(r)));
  }
  folderPath(own: boolean, id: number) {
    return this.http.get<ApiResponse<StorageFolder[]>>(`${this.base}/folders/${id}/path`, {
      params: { own }, context: this.context()
    }).pipe(map(r => this.data(r)));
  }
  files(own: boolean, folderId: number, search: string, global: boolean, sort: string, descending: boolean, page: number, filter = 'all') {
    return this.http.get<ApiResponse<StoragePage<StorageFile>>>(`${this.base}/${own ? 'me/' : ''}files`, {
      params: { folderId, search, global, sort, descending, page, pageSize: 25, filter }, context: this.context()
    }).pipe(map(r => this.data(r)));
  }
  details(id: number, academicYearId?: number, folderId?: number, own?: boolean) {
    let params = new HttpParams();
    if (academicYearId != null) params = params.set('academicYearId', academicYearId);
    if (folderId != null) params = params.set('folderId', folderId);
    if (own != null) params = params.set('own', own);
    return this.http.get<ApiResponse<StorageDetails>>(`${this.base}/files/${id}`, { params, context: this.context() }).pipe(
      map(r => this.data(r)), tap(details => this.detailsCache.set(this.detailsKey(id, academicYearId, folderId, own), { until: Date.now() + 120_000, details })));
  }
  discover(own: boolean, id: number, pageToken = '') {
    return this.http.get<ApiResponse<StorageDiscovery>>(`${this.base}/folders/${id}/drive-items`, { params: { own, pageToken }, context: this.context() }).pipe(map(r => this.data(r)));
  }
  content(id: number, preview = false) { return this.http.get(`${this.base}/files/${id}/content`, { params: { preview }, responseType: 'blob', context: this.context() }); }
  createFolder(parentFolderId: number | undefined, displayName: string) {
    return this.http.post<ApiResponse<StorageFolder>>(`${this.base}/folders`, { parentFolderId, displayName, requestKey: crypto.randomUUID() }, { context: this.context() }).pipe(
      map(r => this.data(r)), tap(() => this.invalidateContext()));
  }
  activateLibrary() { return this.createFolder(undefined, ''); }
  moveFolder(folder: StorageFolder, parentFolderId: number) {
    return this.http.patch<ApiResponse<StorageFolder>>(`${this.base}/folders/${folder.id}/parent`, { parentFolderId, rowVersion: folder.rowVersion }, { context: this.context() }).pipe(map(r => this.data(r)));
  }
  rename(file: StorageFile, displayName: string) {
    return this.http.patch<ApiResponse<unknown>>(`${this.base}/files/${file.storedFileId}/name`, { displayName, rowVersion: file.rowVersion }, { context: this.context() });
  }
  delete(file: StorageFile) {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/files/${file.storedFileId}`, { body: { rowVersion: file.rowVersion }, context: this.context() });
  }
  upload(own: boolean, folderId: number, file: File, key: string) {
    const params = new HttpParams().set('parentFolderId', folderId).set('length', file.size).set('fileName', file.name);
    return this.http.post<ApiResponse<StorageUpload>>(`${this.base}/${own ? 'me/' : ''}files`, file, {
      params, headers: new HttpHeaders({ 'Idempotency-Key': key, 'Content-Type': 'application/octet-stream' }),
      reportProgress: true, observe: 'events', context: this.context()
    });
  }
  reconcile(id: number) {
    return this.http.post<ApiResponse<StorageUpload>>(`${this.base}/operations/${id}/reconcile`, {}, { context: this.context() }).pipe(map(r => this.data(r)));
  }
}
