import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import {
  ParentSurvey,
  ParentSurveySubmission,
  ParentSurveySubmissionListItem,
  PublicParentSurvey,
  PublishParentSurvey,
  SaveParentSurveyRequest,
  SubmitParentSurveyRequest
} from '../models/parent-survey.models';

@Injectable({ providedIn: 'root' })
export class ParentSurveysService {
  private readonly http = inject(HttpClient);
  private readonly adminBase = `${environment.apiUrl}/api/v1/parent-surveys`;
  private readonly publicBase = `${environment.apiUrl}/api/v1/public/parent-surveys`;

  list(templates: boolean): Observable<ApiResponse<ParentSurvey[]>> {
    const params = new HttpParams().set('templates', String(templates));
    return this.http.get<ApiResponse<ParentSurvey[]>>(this.adminBase, { params });
  }

  get(id: number): Observable<ApiResponse<ParentSurvey>> {
    return this.http.get<ApiResponse<ParentSurvey>>(`${this.adminBase}/${id}`);
  }

  create(request: SaveParentSurveyRequest): Observable<ApiResponse<ParentSurvey>> {
    return this.http.post<ApiResponse<ParentSurvey>>(this.adminBase, request);
  }

  update(id: number, request: SaveParentSurveyRequest): Observable<ApiResponse<ParentSurvey>> {
    return this.http.put<ApiResponse<ParentSurvey>>(`${this.adminBase}/${id}`, request);
  }

  publish(id: number): Observable<ApiResponse<PublishParentSurvey>> {
    return this.http.post<ApiResponse<PublishParentSurvey>>(`${this.adminBase}/${id}/publish`, {});
  }

  close(id: number): Observable<ApiResponse<void>> {
    return this.http.post<ApiResponse<void>>(`${this.adminBase}/${id}/close`, {});
  }

  delete(id: number): Observable<ApiResponse<void>> {
    return this.http.delete<ApiResponse<void>>(`${this.adminBase}/${id}`);
  }

  listSubmissions(id: number): Observable<ApiResponse<ParentSurveySubmissionListItem[]>> {
    return this.http.get<ApiResponse<ParentSurveySubmissionListItem[]>>(`${this.adminBase}/${id}/submissions`);
  }

  getSubmission(id: number, submissionId: number): Observable<ApiResponse<ParentSurveySubmission>> {
    return this.http.get<ApiResponse<ParentSurveySubmission>>(`${this.adminBase}/${id}/submissions/${submissionId}`);
  }

  getPublic(token: string): Observable<ApiResponse<PublicParentSurvey>> {
    return this.http.get<ApiResponse<PublicParentSurvey>>(`${this.publicBase}/${encodeURIComponent(token)}`);
  }

  submitPublic(token: string, request: SubmitParentSurveyRequest): Observable<ApiResponse<void>> {
    return this.http.post<ApiResponse<void>>(
      `${this.publicBase}/${encodeURIComponent(token)}/submissions`,
      request
    );
  }
}
