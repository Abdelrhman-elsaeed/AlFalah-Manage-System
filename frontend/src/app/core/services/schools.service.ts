import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse } from '../models/api-response.model';
import { environment } from '../../../environments/environment';
import {
  AssignSchoolManagerRequest,
  PagedResult,
  SchoolCreateRequest,
  SchoolDetail,
  SchoolListItem,
  SchoolListQuery,
  SchoolLocation,
  SchoolLocationCreateRequest,
  SchoolUpdateRequest
} from '../models/phase2.models';

@Injectable({ providedIn: 'root' })
export class SchoolsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/v1/schools`;
  private readonly locationsBase = `${environment.apiUrl}/api/v1/school-locations`;

  list(query: SchoolListQuery): Observable<ApiResponse<PagedResult<SchoolListItem>>> {
    let params = new HttpParams()
      .set('page', String(query.page ?? 1))
      .set('pageSize', String(query.pageSize ?? 20));
    if (query.search)   params = params.set('search', query.search);
    if (query.city)     params = params.set('city', query.city);
    if (query.stage)    params = params.set('stage', query.stage);
    if (query.isActive !== undefined) params = params.set('isActive', String(query.isActive));
    if (query.sortBy)   params = params.set('sortBy', query.sortBy);
    if (query.sortDesc) params = params.set('sortDesc', String(query.sortDesc));

    return this.http.get<ApiResponse<PagedResult<SchoolListItem>>>(this.base, { params });
  }

  getById(id: number): Observable<ApiResponse<SchoolDetail>> {
    return this.http.get<ApiResponse<SchoolDetail>>(`${this.base}/${id}`);
  }

  create(body: SchoolCreateRequest): Observable<ApiResponse<SchoolDetail>> {
    return this.http.post<ApiResponse<SchoolDetail>>(this.base, body);
  }

  update(id: number, body: SchoolUpdateRequest): Observable<ApiResponse<SchoolDetail>> {
    return this.http.put<ApiResponse<SchoolDetail>>(`${this.base}/${id}`, body);
  }

  delete(id: number): Observable<ApiResponse<void>> {
    return this.http.delete<ApiResponse<void>>(`${this.base}/${id}`);
  }

  assignManager(id: number, body: AssignSchoolManagerRequest): Observable<ApiResponse<SchoolDetail>> {
    return this.http.post<ApiResponse<SchoolDetail>>(`${this.base}/${id}/assign-manager`, body);
  }

  activate(id: number): Observable<ApiResponse<void>> {
    return this.http.post<ApiResponse<void>>(`${this.base}/${id}/activate`, {});
  }

  deactivate(id: number): Observable<ApiResponse<void>> {
    return this.http.post<ApiResponse<void>>(`${this.base}/${id}/deactivate`, {});
  }

  listLocations(): Observable<ApiResponse<SchoolLocation[]>> {
    return this.http.get<ApiResponse<SchoolLocation[]>>(this.locationsBase);
  }

  createLocation(body: SchoolLocationCreateRequest): Observable<ApiResponse<SchoolLocation>> {
    return this.http.post<ApiResponse<SchoolLocation>>(this.locationsBase, body);
  }
}
