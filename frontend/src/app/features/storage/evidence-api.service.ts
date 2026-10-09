import { HttpClient, HttpContext, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { catchError, map, tap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../../core/models/api-response.model';
import { SUPPRESS_FORBIDDEN_REDIRECT, SUPPRESS_ERROR_TOAST } from '../../core/http/http-context.tokens';
import { AuthService } from '../../core/services/auth.service';
import { EvidenceCounts, EvidenceLink, FileChange, Requirement } from './evidence.models';
import { StoragePage, StorageUpload, StorageDetails } from './storage.models';

@Injectable({ providedIn: 'root' })
export class StorageEvidenceApiService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly base = `${environment.apiUrl}/api/v1/storage`;
  private readonly countsCache = new Map<string, EvidenceCounts>();
  private readonly queueSnapshots = new Map<string, StoragePage<EvidenceLink>>();
  private readonly changeQueueSnapshots = new Map<string, StoragePage<{id:number; storedFileId:number; fileName:string; kind:string; status:string; reason:string}>>();
  private snapshotKey(...parts: unknown[]) {
    const user = this.auth.currentUser();
    return JSON.stringify([user?.userId, user?.activeSchoolId, this.auth.getAccessToken(), ...parts]);
  }
  private rememberSnapshot<T>(cache: Map<string,T>, key: string, value: T) {
    cache.delete(key);
    cache.set(key, value);
    if (cache.size > 30) cache.delete(cache.keys().next().value!);
  }
  peekQueue(year: number, page: number, requirement?: number, teacher?: number, standard?: string, status?: number, decided = false) {
    return this.queueSnapshots.get(this.snapshotKey(year,page,requirement,teacher,standard,status,decided));
  }
  peekChangeQueue(year: number, page: number, status = 'Pending') {
    return this.changeQueueSnapshots.get(this.snapshotKey(year,page,status));
  }
  clearReviewSnapshots() { this.queueSnapshots.clear(); this.changeQueueSnapshots.clear(); this.countsCache.clear(); }
  private countsKey(year: number, own: boolean) {
    const user = this.auth.currentUser();
    return `${user?.userId || ''}:${user?.activeSchoolId || ''}:${year}:${own}`;
  }
  peekCounts(year: number, own: boolean) { return this.countsCache.get(this.countsKey(year, own)); }
  private context() { return new HttpContext().set(SUPPRESS_FORBIDDEN_REDIRECT, true).set(SUPPRESS_ERROR_TOAST, true); }
  private data<T>(r: ApiResponse<T>) { if (!r.isSuccess || r.data == null) throw new Error(r.message); return r.data; }
  private get<T>(path: string, params: Record<string, string | number | boolean> = {}) { return this.http.get<ApiResponse<T>>(`${this.base}/${path}`, { params, context: this.context() }).pipe(map(r => this.data(r))); }
  private post<T>(path: string, body: unknown) { return this.http.post<ApiResponse<T>>(`${this.base}/${path}`, body, { context: this.context() }).pipe(map(r => this.data(r))); }
  years() { return this.get<{id: number; nameAr: string}[]>('academic-years'); }
  teachers() { return this.get<{id:number; displayName:string}[]>('evidence-teachers'); }
  history(file: number) { return this.get<StorageDetails>(`files/${file}/history`); }
  changeQueue(year: number, page: number, status = 'Pending') {
    const key = this.snapshotKey(year,page,status);
    return this.get<StoragePage<{id:number; storedFileId:number; fileName:string; kind:string; status:string; reason:string}>>('change-queue',{academicYearId:year,page,status})
      .pipe(tap(result => this.rememberSnapshot(this.changeQueueSnapshots,key,result)));
  }
  catalog(year: number, search = '') { return this.get<Requirement[]>('requirement-catalog', { academicYearId: year, search }); }
  initialize(year: number) { return this.post<Requirement[]>(`requirements/initialize?academicYearId=${year}`, {}); }
  configure(requirement: Requirement, body: unknown) {
    return this.http.patch<ApiResponse<Requirement>>(`${this.base}/requirements/${requirement.id}`, body, {context: this.context()}).pipe(map(r => this.data(r)));
  }
  links(file: number, own: boolean) { return this.get<EvidenceLink[]>(`${own ? 'me/' : ''}files/${file}/links`); }
  link(file: number, requirementId: number, academicYearId: number) { return this.post<EvidenceLink>(`files/${file}/links`, { requirementId, academicYearId }); }
  submit(link: EvidenceLink) { return this.post<EvidenceLink>(`links/${link.id}/submit`, { rowVersion: link.rowVersion }); }
  review(link: EvidenceLink, approve: boolean, note: string) { return this.post<EvidenceLink>(`links/${link.id}/review`, { decision: approve ? 3 : 4, note: note || null, rowVersion: link.rowVersion }); }
  queue(year: number, page: number, requirement?: number, teacher?: number, standard?: string, status?: number, decided = false) {
    const key = this.snapshotKey(year,page,requirement,teacher,standard,status,decided);
    let params = new HttpParams().set('academicYearId', year).set('page', page).set('pageSize', 25);
    for (const [key, value] of Object.entries({requirementId: requirement, teacherId: teacher, standardCode: standard, status})) if (value != null && value !== '') params = params.set(key, value);
    if (decided) params = params.set('decided', true);
    return this.http.get<ApiResponse<StoragePage<EvidenceLink>>>(`${this.base}/review-queue`, { params, context: this.context() }).pipe(map(r => this.data(r)),tap(result => this.rememberSnapshot(this.queueSnapshots,key,result)));
  }
  counts(year: number, own: boolean) { const key = this.countsKey(year, own); return this.get<EvidenceCounts>('evidence-counts', { academicYearId: year, own }).pipe(
    tap(counts => this.countsCache.set(key, counts)),
    catchError(error => { if (error?.status === 401 || error?.status === 403) this.countsCache.delete(key); return throwError(() => error); })
  ); }
  changes(file: number) { return this.get<FileChange[]>(`files/${file}/change-requests`); }
  requestChange(file: number, kind: string, reason: string, rowVersion: string, replaceBeforeReview = false) { return this.post<FileChange>(`files/${file}/change-requests`, { kind, reason, rowVersion, replaceBeforeReview }); }
  decideChange(change: FileChange, approve: boolean, note: string) { return this.post<FileChange>(`change-requests/${change.id}/review`, { approve, note, rowVersion: change.rowVersion }); }
  uploadVersion(change: FileChange, file: File, key: string) {
    const params = new HttpParams().set('length', file.size).set('fileName', file.name);
    return this.http.post<ApiResponse<StorageUpload>>(`${this.base}/change-requests/${change.id}/version`, file, {
      params, headers: new HttpHeaders({ 'Idempotency-Key': key, 'Content-Type': 'application/octet-stream' }),
      reportProgress: true, observe: 'events', context: this.context()
    });
  }
  versionContent(file: number, version: number) { return this.http.get(`${this.base}/files/${file}/versions/${version}/content`, { responseType: 'blob', context: this.context() }); }
}
