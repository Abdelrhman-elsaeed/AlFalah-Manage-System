import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import {
  AcademicConcernDto,
  BehaviorIncidentDto,
  CreateAcademicConcernRequestDto,
  CreateBehaviorIncidentRequestDto,
  CreateRecognitionRequestDto,
  CreateSessionDelayRequestDto,
  GuardianStudentAffairsDashboardDto,
  GuardianStudentDto,
  GuardianStudentSummaryDto,
  OfficerStudentAffairsDashboardDto,
  RecognitionDto,
  SchoolOversightDashboardDto,
  SecurityStudentAffairsDashboardDto,
  SessionDelayDto,
  TeacherCurrentContextDto,
  TeacherStudentAffairsDashboardDto,
  TeacherTopPriorityDto
} from '../models/student-affairs-dashboard.models';

@Injectable({ providedIn: 'root' })
export class StudentAffairsDashboardService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/api/v1`;

  getTeacherCurrentContext(): Observable<ApiResponse<TeacherCurrentContextDto>> {
    return this.http.get<ApiResponse<TeacherCurrentContextDto>>(`${this.api}/teacher/student-affairs/current-context`);
  }

  getTeacherTopPriority(): Observable<ApiResponse<TeacherTopPriorityDto>> {
    return this.http.get<ApiResponse<TeacherTopPriorityDto>>(`${this.api}/teacher/student-affairs/top-priority`);
  }

  getTeacherDashboard(): Observable<ApiResponse<TeacherStudentAffairsDashboardDto>> {
    return this.http.get<ApiResponse<TeacherStudentAffairsDashboardDto>>(`${this.api}/student-affairs/dashboard/teacher`);
  }

  createBehaviorIncident(request: CreateBehaviorIncidentRequestDto): Observable<ApiResponse<BehaviorIncidentDto>> {
    return this.http.post<ApiResponse<BehaviorIncidentDto>>(`${this.api}/behaviors`, request);
  }

  createAcademicConcern(request: CreateAcademicConcernRequestDto): Observable<ApiResponse<AcademicConcernDto>> {
    return this.http.post<ApiResponse<AcademicConcernDto>>(`${this.api}/academic-concerns`, request);
  }

  createSessionDelay(request: CreateSessionDelayRequestDto): Observable<ApiResponse<SessionDelayDto>> {
    return this.http.post<ApiResponse<SessionDelayDto>>(`${this.api}/session-delays`, request);
  }

  createRecognition(request: CreateRecognitionRequestDto): Observable<ApiResponse<RecognitionDto>> {
    return this.http.post<ApiResponse<RecognitionDto>>(`${this.api}/recognitions`, request);
  }

  getSecurityDashboard(): Observable<ApiResponse<SecurityStudentAffairsDashboardDto>> {
    return this.http.get<ApiResponse<SecurityStudentAffairsDashboardDto>>(`${this.api}/student-affairs/dashboard/security`);
  }

  getGuardianDashboard(): Observable<ApiResponse<GuardianStudentAffairsDashboardDto>> {
    return this.http.get<ApiResponse<GuardianStudentAffairsDashboardDto>>(`${this.api}/student-affairs/dashboard/guardian`);
  }

  getGuardianStudents(): Observable<ApiResponse<readonly GuardianStudentDto[]>> {
    return this.http.get<ApiResponse<readonly GuardianStudentDto[]>>(`${this.api}/guardian/students`);
  }

  getGuardianStudentSummary(studentId: number): Observable<ApiResponse<GuardianStudentSummaryDto>> {
    return this.http.get<ApiResponse<GuardianStudentSummaryDto>>(`${this.api}/guardian/students/${studentId}/summary`);
  }

  getOfficerDashboard(): Observable<ApiResponse<OfficerStudentAffairsDashboardDto>> {
    return this.http.get<ApiResponse<OfficerStudentAffairsDashboardDto>>(`${this.api}/student-affairs/dashboard/officer`);
  }

  getSchoolOversightDashboard(): Observable<ApiResponse<SchoolOversightDashboardDto>> {
    return this.http.get<ApiResponse<SchoolOversightDashboardDto>>(`${this.api}/student-affairs/dashboard/school-oversight`);
  }
}
