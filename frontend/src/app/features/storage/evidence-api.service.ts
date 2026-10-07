import { HttpClient, HttpContext, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../../core/models/api-response.model';
import { SUPPRESS_FORBIDDEN_REDIRECT, SUPPRESS_ERROR_TOAST } from '../../core/http/http-context.tokens';
import { EvidenceCounts, EvidenceLink, FileChange, Requirement } from './evidence.models';
import { StoragePage, StorageUpload, StorageDetails } from './storage.models';

@Injectable({ providedIn: 'root' })
export class StorageEvidenceApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/v1/storage`;
  private context() { return new HttpContext().set(SUPPRESS_FORBIDDEN_REDIRECT, true).set(SUPPRESS_ERROR_TOAST, true); }
  private data<T>(r: ApiResponse<T>) { if (!r.isSuccess || r.data == null) throw new Error(r.message); return r.data; }
  private get<T>(path: string, params: Record<string, string | number | boolean> = {}) { return this.http.get<ApiResponse<T>>(`${this.base}/${path}`, { params, context: this.context() }).pipe(map(r => this.data(r))); }
  private post<T>(path: string, body: unknown) { return this.http.post<ApiResponse<T>>(`${this.base}/${path}`, body, { context: this.context() }).pipe(map(r => this.data(r))); }
  years() { return this.get<{id: number; nameAr: string}[]>('academic-years'); }
  teachers() { return this.get<{id:number; displayName:string}[]>('evidence-teachers'); }
  history(file: number) { return this.get<StorageDetails>(`files/${file}/history`); }
  changeQueue(year: number, page: number, status = 'Pending') { return this.get<StoragePage<{id:number; storedFileId:number; fileName:string; kind:string; status:string; reason:string}>>('change-queue',{academicYearId:year,page,status}); }
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
    let params = new HttpParams().set('academicYearId', year).set('page', page).set('pageSize', 25);
    for (const [key, value] of Object.entries({requirementId: requirement, teacherId: teacher, standardCode: standard, status})) if (value != null && value !== '') params = params.set(key, value);
    if (decided) params = params.set('decided', true);
    return this.http.get<ApiResponse<StoragePage<EvidenceLink>>>(`${this.base}/review-queue`, { params, context: this.context() }).pipe(map(r => this.data(r)));
  }
  counts(year: number, own: boolean) { return this.get<EvidenceCounts>('evidence-counts', { academicYearId: year, own }); }
  changes(file: number) { return this.get<FileChange[]>(`files/${file}/change-requests`); }
  requestChange(file: number, kind: string, reason: string, rowVersion: string, replaceBeforeReview = false) { return this.post<FileChange>(`files/${file}/change-requests`, { kind, reason, rowVersion, replaceBeforeReview }); }
  decideChange(change: FileChange, approve: boolean, note: string) { return this.post<FileChange>(`change-requests/${change.id}/review`, { approve, note, rowVersion: change.rowVersion }); }
  uploadVersion(change: FileChange, file: File, key: string) {
    const body = new FormData(); body.append('length', String(file.size)); body.append('file', file);
    return this.http.post<ApiResponse<StorageUpload>>(`${this.base}/change-requests/${change.id}/version`, body, { headers: new HttpHeaders({'Idempotency-Key': key}), reportProgress: true, observe: 'events', context: this.context() });
  }
  versionContent(file: number, version: number) { return this.http.get(`${this.base}/files/${file}/versions/${version}/content`, { responseType: 'blob', context: this.context() }); }
}
