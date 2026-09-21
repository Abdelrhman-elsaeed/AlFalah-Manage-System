import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import {
  CreateVisitV2Request, UpdateVisitV2Request, VisitV2ArchiveQuery, VisitV2ArchiveResult,
  VisitV2Availability, VisitV2Dashboard, VisitV2Detail, VisitV2ObservationCard, VisitV2Treatment
} from '../models/visit-v2.models';

@Injectable({ providedIn: 'root' })
export class VisitsV2Service {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/v2/visits`;

  availability(): Observable<ApiResponse<VisitV2Availability>> { return this.http.get<ApiResponse<VisitV2Availability>>(`${this.base}/availability`); }
  observationCard(): Observable<ApiResponse<VisitV2ObservationCard>> { return this.http.get<ApiResponse<VisitV2ObservationCard>>(`${this.base}/observation-card`); }
  create(body: CreateVisitV2Request): Observable<ApiResponse<VisitV2Detail>> { return this.http.post<ApiResponse<VisitV2Detail>>(this.base, body); }
  update(id: number, body: UpdateVisitV2Request): Observable<ApiResponse<VisitV2Detail>> { return this.http.put<ApiResponse<VisitV2Detail>>(`${this.base}/${id}`, body); }
  get(id: number): Observable<ApiResponse<VisitV2Detail>> { return this.http.get<ApiResponse<VisitV2Detail>>(`${this.base}/${id}`); }
  finalize(id: number): Observable<ApiResponse<VisitV2Detail>> { return this.http.post<ApiResponse<VisitV2Detail>>(`${this.base}/${id}/finalize`, {}); }
  approve(id: number): Observable<ApiResponse<VisitV2Detail>> { return this.http.post<ApiResponse<VisitV2Detail>>(`${this.base}/${id}/approve`, {}); }
  reject(id: number, reason: string): Observable<ApiResponse<VisitV2Detail>> { return this.http.post<ApiResponse<VisitV2Detail>>(`${this.base}/${id}/reject`, { reason }); }
  reopen(id: number, reason: string): Observable<ApiResponse<VisitV2Detail>> { return this.http.post<ApiResponse<VisitV2Detail>>(`${this.base}/${id}/reopen`, { reason }); }
  softDelete(id: number): Observable<ApiResponse<void>> { return this.http.delete<ApiResponse<void>>(`${this.base}/${id}`); }
  dashboard(): Observable<ApiResponse<VisitV2Dashboard>> { return this.http.get<ApiResponse<VisitV2Dashboard>>(`${this.base}/dashboard`); }

  list(query: VisitV2ArchiveQuery = {}): Observable<ApiResponse<VisitV2ArchiveResult>> {
    return this.http.get<ApiResponse<VisitV2ArchiveResult>>(this.base, { params: this.params(query) });
  }
  updateTreatments(id: number, items: VisitV2Treatment[]): Observable<ApiResponse<VisitV2Treatment[]>> {
    return this.http.put<ApiResponse<VisitV2Treatment[]>>(`${this.base}/${id}/treatment-recommendations`, {
      items: items.map(({ id: itemId, rubricDomainId, domainNameAr, goal, actions, successIndicators, sortOrder }) =>
        ({ id: itemId || null, rubricDomainId, domainNameAr, goal, actions, successIndicators, sortOrder }))
    });
  }
  exportCsv(query: VisitV2ArchiveQuery = {}): Observable<HttpResponse<Blob>> {
    return this.http.get(`${this.base}/export/csv`, { params: this.params(query), responseType: 'blob', observe: 'response' });
  }
  exportPdf(id: number): Observable<HttpResponse<Blob>> {
    return this.http.get(`${this.base}/${id}/report/pdf`, { responseType: 'blob', observe: 'response' });
  }

  private params(query: VisitV2ArchiveQuery): HttpParams {
    let params = new HttpParams().set('page', String(query.page ?? 1)).set('pageSize', String(query.pageSize ?? 20));
    Object.entries(query).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== '') params = params.set(key, String(value));
    });
    return params;
  }
}

