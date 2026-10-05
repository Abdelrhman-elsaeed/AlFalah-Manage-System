import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../../core/models/api-response.model';
import { SUPPRESS_ERROR_TOAST, SUPPRESS_FORBIDDEN_REDIRECT } from '../../core/http/http-context.tokens';

export interface ImportBatch {
  id: number; academicYearId: number; templateVersion: number; sourceName: string; sourceVersion: string; sourceSHA256: string;
  status: string; rows: number; matched: number; missing: number; conflicts: number; referenceOnly: number; importedFiles: number;
  digest: string; reviewedDigest?: string; rowVersion: string; committedAtUtc?: string;
}
export interface ImportRow {
  id: number; source: { key: string; name: string; referencePath?: string; responsibleName?: string; sourceStatus?: string };
  classification: string; status: string; reason?: string; requirementId?: number; responsibleUserId?: string;
  suggestions: {userId: string; name: string}[]; storedFileId?: number; uploadOperationId?: number; rowVersion: string;
}
@Injectable({providedIn: 'root'})
export class ImportApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/v1/storage/imports`;
  private options() { return {context: new HttpContext().set(SUPPRESS_ERROR_TOAST, true).set(SUPPRESS_FORBIDDEN_REDIRECT, true)}; }
  private data<T>(r: ApiResponse<T>) { if (!r.isSuccess || r.data == null) throw new Error(r.message); return r.data; }
  list() { return this.http.get<ApiResponse<ImportBatch[]>>(this.base, this.options()).pipe(map(r => this.data(r))); }
  get(id: number) { return this.http.get<ApiResponse<ImportBatch>>(`${this.base}/${id}`, this.options()).pipe(map(r => this.data(r))); }
  preview(file: File, year: number, version: number, sourceVersion: string) {
    const form = new FormData(); form.append('file', file); form.append('academicYearId', String(year)); form.append('templateVersion', String(version)); form.append('sourceVersion', sourceVersion);
    return this.http.post<ApiResponse<ImportBatch>>(`${this.base}/preview`, form, this.options()).pipe(map(r => this.data(r)));
  }
  rows(id: number, page: number, classification: string) { return this.http.get<ApiResponse<{items: ImportRow[]; total: number}>>(`${this.base}/${id}/rows`, {...this.options(), params: {page, classification}}).pipe(map(r => this.data(r))); }
  resolve(batch: ImportBatch, row: ImportRow, reason: string) { return this.http.patch<ApiResponse<ImportBatch>>(`${this.base}/${batch.id}/rows/${row.id}`, {rowVersion: row.rowVersion, requirementId: row.requirementId ?? null, responsibleUserId: row.responsibleUserId ?? null, reason}, this.options()).pipe(map(r => this.data(r))); }
  decide(batch: ImportBatch, commit: boolean, reason: string) { return this.http.post<ApiResponse<ImportBatch>>(`${this.base}/${batch.id}/${commit ? 'commit' : 'review'}`, {rowVersion: batch.rowVersion, digest: batch.digest, reason}, this.options()).pipe(map(r => this.data(r))); }
  export(id: number) { return this.http.get(`${this.base}/${id}/exceptions.csv`, {...this.options(), responseType: 'blob'}); }
  bytes(batch: ImportBatch, row: ImportRow, file: File, reason: string) {
    const form = new FormData(); form.append('file', file); form.append('rowVersion', row.rowVersion); form.append('reason', reason);
    return this.http.post<ApiResponse<ImportRow>>(`${this.base}/${batch.id}/rows/${row.id}/bytes`, form, this.options()).pipe(map(r => this.data(r)));
  }
  reconcile(batch: ImportBatch, row: ImportRow) { return this.http.post<ApiResponse<ImportRow>>(`${this.base}/${batch.id}/rows/${row.id}/reconcile`, {}, this.options()).pipe(map(r => this.data(r))); }
}
