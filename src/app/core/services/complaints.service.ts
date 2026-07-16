import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse } from '../models/api-response.model';
import { environment } from '../../../environments/environment';
import {
  Complaint,
  CreateComplaintRequest,
  UpdateComplaintStatusRequest,
  ReopenVisitFromComplaintRequest
} from '../models/complaint.models';

@Injectable({ providedIn: 'root' })
export class ComplaintsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/v1`;

  /** Instructor submits a complaint on his OWN approved + viewed visit. */
  create(visitId: number, body: CreateComplaintRequest): Observable<ApiResponse<Complaint>> {
    return this.http.post<ApiResponse<Complaint>>(`${this.base}/visits/${visitId}/complaints`, body);
  }

  /** Scoped list (SM = school, Instructor = own, SuperAdmin = support; MainManager/Moderator → 403). */
  list(status?: number | null): Observable<ApiResponse<Complaint[]>> {
    let params = new HttpParams();
    if (status !== null && status !== undefined) params = params.set('status', String(status));
    return this.http.get<ApiResponse<Complaint[]>>(`${this.base}/complaints`, { params });
  }

  getById(id: number): Observable<ApiResponse<Complaint>> {
    return this.http.get<ApiResponse<Complaint>>(`${this.base}/complaints/${id}`);
  }

  updateStatus(id: number, body: UpdateComplaintStatusRequest): Observable<ApiResponse<Complaint>> {
    return this.http.put<ApiResponse<Complaint>>(`${this.base}/complaints/${id}/status`, body);
  }

  reopenVisit(id: number, body: ReopenVisitFromComplaintRequest): Observable<ApiResponse<Complaint>> {
    return this.http.post<ApiResponse<Complaint>>(`${this.base}/complaints/${id}/reopen-visit`, body);
  }

  delete(id: number): Observable<ApiResponse<void>> {
    return this.http.delete<ApiResponse<void>>(`${this.base}/complaints/${id}`);
  }
}
