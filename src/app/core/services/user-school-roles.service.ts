import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse } from '../models/api-response.model';
import { environment } from '../../../environments/environment';
import { UserSchoolRoleCreateRequest, UserSchoolRoleDetail } from '../models/phase2.models';

@Injectable({ providedIn: 'root' })
export class UserSchoolRolesService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/v1/user-school-roles`;

  getBySchool(schoolId?: number): Observable<ApiResponse<UserSchoolRoleDetail[]>> {
    let params = new HttpParams();
    if (schoolId !== undefined && schoolId !== null) {
      params = params.set('schoolId', String(schoolId));
    }
    return this.http.get<ApiResponse<UserSchoolRoleDetail[]>>(this.base, { params });
  }

  create(body: UserSchoolRoleCreateRequest): Observable<ApiResponse<UserSchoolRoleDetail>> {
    return this.http.post<ApiResponse<UserSchoolRoleDetail>>(this.base, body);
  }

  delete(id: number): Observable<ApiResponse<void>> {
    return this.http.delete<ApiResponse<void>>(`${this.base}/${id}`);
  }
}