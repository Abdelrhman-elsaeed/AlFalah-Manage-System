import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { AllocateSubject, Subject, SubjectBulkResult, SubjectOverview, SubjectRoom, SubjectRules } from '../models/subject.models';
import { SUPPRESS_ERROR_TOAST } from '../http/http-context.tokens';

@Injectable({ providedIn: 'root' })
export class SubjectService {
  private readonly http = inject(HttpClient);
  private readonly options = { context: new HttpContext().set(SUPPRESS_ERROR_TOAST, true) };
  private url(setup: number) { return `${environment.apiUrl}/api/v1/intelligent-timetable/settings/profiles/${setup}/subjects`; }
  get(setup: number) { return this.http.get<ApiResponse<SubjectOverview>>(this.url(setup), this.options); }
  saveSubject(setup: number, subject: Subject) {
    return subject.id ? this.http.put<ApiResponse<Subject>>(`${this.url(setup)}/${subject.id}`, subject, this.options)
      : this.http.post<ApiResponse<Subject>>(this.url(setup), subject, this.options);
  }
  createRoom(setup: number, name: string) { return this.http.post<ApiResponse<SubjectRoom>>(`${this.url(setup)}/rooms`, { name }, this.options); }
  allocate(setup: number, request: AllocateSubject) { return this.http.post<ApiResponse<SubjectBulkResult>>(`${this.url(setup)}/allocations`, request, this.options); }
  update(setup: number, id: number, revision: number, rules: SubjectRules) { return this.http.put<ApiResponse<SubjectBulkResult>>(`${this.url(setup)}/requirements/${id}`, { revision, rules }, this.options); }
  remove(setup: number, id: number, revision: number) { return this.http.delete<ApiResponse<number>>(`${this.url(setup)}/requirements/${id}?revision=${revision}`, this.options); }
}
