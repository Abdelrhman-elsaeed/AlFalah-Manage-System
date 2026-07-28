import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse } from '../models/api-response.model';
import { environment } from '../../../environments/environment';
import {
  PagedResult,
  UserCreateRequest,
  UserDetail,
  UserListItem,
  UserListQuery,
  UserUpdateRequest
} from '../models/phase2.models';

@Injectable({ providedIn: 'root' })
export class UsersService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/v1/users`;

  list(query: UserListQuery): Observable<ApiResponse<PagedResult<UserListItem>>> {
    let params = new HttpParams()
      .set('page', String(query.page ?? 1))
      .set('pageSize', String(query.pageSize ?? 20));
    if (query.search)   params = params.set('search', query.search);
    if (query.role)     params = params.set('role', query.role);
    if (query.schoolId !== undefined) params = params.set('schoolId', String(query.schoolId));
    if (query.isActive !== undefined) params = params.set('isActive', String(query.isActive));
    if (query.sortBy)   params = params.set('sortBy', query.sortBy);
    if (query.sortDesc) params = params.set('sortDesc', String(query.sortDesc));

    return this.http.get<ApiResponse<PagedResult<UserListItem>>>(this.base, { params });
  }

  getById(id: string): Observable<ApiResponse<UserDetail>> {
    return this.http.get<ApiResponse<UserDetail>>(`${this.base}/${id}`);
  }

  create(body: UserCreateRequest): Observable<ApiResponse<UserDetail>> {
    return this.http.post<ApiResponse<UserDetail>>(this.base, body);
  }

  update(id: string, body: UserUpdateRequest): Observable<ApiResponse<UserDetail>> {
    return this.http.put<ApiResponse<UserDetail>>(`${this.base}/${id}`, body);
  }

  changePassword(id: string, newPassword: string): Observable<ApiResponse<void>> {
    return this.http.post<ApiResponse<void>>(`${this.base}/${id}/password`, { newPassword });
  }

  deactivate(id: string): Observable<ApiResponse<void>> {
    return this.http.post<ApiResponse<void>>(`${this.base}/${id}/deactivate`, {});
  }
}
