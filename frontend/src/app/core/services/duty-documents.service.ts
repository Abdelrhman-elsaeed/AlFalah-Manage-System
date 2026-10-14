import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpContext } from '@angular/common/http';
import { SUPPRESS_ERROR_TOAST, SUPPRESS_FORBIDDEN_REDIRECT } from '../http/http-context.tokens';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';

export interface DutyDocumentState {
  documentId: number; kind: 'Letter' | 'Report'; assignmentRevision: number; signedAt: string;
  previousDocumentId: number | null; correctionReason: string | null; artifactState: string; archiveState: string;
  canRetry: boolean; attempts: number; nextAttemptAt: string | null; verifiedAt: string | null; errorCode: string | null;
  authorName?: string; dutyDate?: string; isCurrent?: boolean;
}
export interface DutyDocumentCollection {
  readiness: { enabled: boolean; processingEnabled: boolean; connectionState: string };
  documents: DutyDocumentState[];
}
export interface DutyArchiveRow { assignmentId:number;teacherId:string;teacher:string;date:string;revision:number;isCurrent:boolean;revisionCount:number;letter:DutyDocumentState|null;report:DutyDocumentState|null; }
export interface DutyArchivePage { page:number;pageSize:number;total:number;readiness:DutyDocumentCollection['readiness'];rows:DutyArchiveRow[]; }
@Injectable({ providedIn: 'root' })
export class DutyDocumentsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/v1/school-duty`;
  private readonly options = { context: new HttpContext().set(SUPPRESS_ERROR_TOAST, true).set(SUPPRESS_FORBIDDEN_REDIRECT, true) };
  list(id: number) { return this.http.get<ApiResponse<DutyDocumentCollection>>(`${this.base}/assignments/${id}/documents`, this.options); }
  pdf(id: number, archived = false) { return this.http.get(`${this.base}/documents/${id}/pdf`, { params: { archived }, observe: 'response', responseType: 'blob', ...this.options }); }
  roster(term: number, revision: number) { return this.http.get(`${this.base}/plans/roster.pdf`, { params: { termId: term, revision }, observe: 'response', responseType: 'blob', ...this.options }); }
  retry(id: number) { return this.http.post<ApiResponse<boolean>>(`${this.base}/documents/${id}/archive/retry`, {}, this.options); }
  enable(enabled: boolean) { return this.http.put<ApiResponse<DutyDocumentCollection['readiness']>>(`${this.base}/archive/enablement`, { enabled }, this.options); }
  readiness() { return this.http.get<ApiResponse<DutyDocumentCollection['readiness']>>(`${this.base}/archive/enablement`, this.options); }
  archive(query:Record<string,string|number>) { return this.http.get<ApiResponse<DutyArchivePage>>(`${this.base}/archive`,{...this.options,params:query}); }
}
