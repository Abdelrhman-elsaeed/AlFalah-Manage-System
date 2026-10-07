import { HttpClient, HttpContext, HttpEvent, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../../core/models/api-response.model';
import { SUPPRESS_FORBIDDEN_REDIRECT, SUPPRESS_ERROR_TOAST } from '../../core/http/http-context.tokens';
import { StorageContext, StorageDetails, StorageFile, StorageFolder, StoragePage, StorageUpload, StorageDiscovery } from './storage.models';

@Injectable({ providedIn: 'root' })
export class StorageApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/v1/storage`;
  private context() { return new HttpContext().set(SUPPRESS_FORBIDDEN_REDIRECT, true).set(SUPPRESS_ERROR_TOAST, true); }
  private data<T>(response: ApiResponse<T>): T {
    if (!response.isSuccess || response.data == null) throw new Error(response.errors?.join(' ') || response.message);
    return response.data;
  }
  contextInfo(own: boolean) {
    return this.http.get<ApiResponse<StorageContext>>(`${this.base}/context`, { params: { own }, context: this.context() }).pipe(map(r => this.data(r)));
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
    return this.http.get<ApiResponse<StorageDetails>>(`${this.base}/files/${id}`, { params, context: this.context() }).pipe(map(r => this.data(r)));
  }
  discover(own: boolean, id: number, pageToken = '') {
    return this.http.get<ApiResponse<StorageDiscovery>>(`${this.base}/folders/${id}/drive-items`, { params: { own, pageToken }, context: this.context() }).pipe(map(r => this.data(r)));
  }
  content(id: number, preview = false) { return this.http.get(`${this.base}/files/${id}/content`, { params: { preview }, responseType: 'blob', context: this.context() }); }
  createFolder(parentFolderId: number | undefined, displayName: string) {
    return this.http.post<ApiResponse<StorageFolder>>(`${this.base}/folders`, { parentFolderId, displayName, requestKey: crypto.randomUUID() }, { context: this.context() }).pipe(map(r => this.data(r)));
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
    const body = new FormData();
    body.append('parentFolderId', String(folderId));
    body.append('length', String(file.size));
    body.append('file', file);
    return this.http.post<ApiResponse<StorageUpload>>(`${this.base}/${own ? 'me/' : ''}files`, body, {
      headers: new HttpHeaders({ 'Idempotency-Key': key }), reportProgress: true, observe: 'events', context: this.context()
    });
  }
  reconcile(id: number) {
    return this.http.post<ApiResponse<StorageUpload>>(`${this.base}/operations/${id}/reconcile`, {}, { context: this.context() }).pipe(map(r => this.data(r)));
  }
}
