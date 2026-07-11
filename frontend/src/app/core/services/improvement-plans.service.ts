import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse } from '../models/api-response.model';
import { environment } from '../../../environments/environment';
import {
  ImprovementPlan,
  CreatePlanRequest,
  UpdatePlanRequest,
  PlanFollowUp,
  CreateFollowUpRequest,
  UpdateFollowUpRequest,
  PlanProgress,
  WeakDomainSuggestion
} from '../models/improvement-plan.models';

@Injectable({ providedIn: 'root' })
export class ImprovementPlansService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/v1`;

  getPlansForVisit(visitId: number): Observable<ApiResponse<ImprovementPlan[]>> {
    return this.http.get<ApiResponse<ImprovementPlan[]>>(`${this.base}/visits/${visitId}/improvement-plans`);
  }

  getWeakDomainSuggestions(visitId: number): Observable<ApiResponse<WeakDomainSuggestion[]>> {
    return this.http.get<ApiResponse<WeakDomainSuggestion[]>>(`${this.base}/visits/${visitId}/weak-domains-suggestions`);
  }

  getPlanById(id: number): Observable<ApiResponse<ImprovementPlan>> {
    return this.http.get<ApiResponse<ImprovementPlan>>(`${this.base}/improvement-plans/${id}`);
  }

  createPlan(body: CreatePlanRequest): Observable<ApiResponse<ImprovementPlan>> {
    return this.http.post<ApiResponse<ImprovementPlan>>(`${this.base}/improvement-plans`, body);
  }

  updatePlan(id: number, body: UpdatePlanRequest): Observable<ApiResponse<ImprovementPlan>> {
    return this.http.put<ApiResponse<ImprovementPlan>>(`${this.base}/improvement-plans/${id}`, body);
  }

  deletePlan(id: number): Observable<ApiResponse<void>> {
    return this.http.delete<ApiResponse<void>>(`${this.base}/improvement-plans/${id}`);
  }

  addFollowUp(planId: number, body: CreateFollowUpRequest): Observable<ApiResponse<PlanFollowUp>> {
    return this.http.post<ApiResponse<PlanFollowUp>>(`${this.base}/improvement-plans/${planId}/follow-ups`, body);
  }

  updateFollowUp(id: number, body: UpdateFollowUpRequest): Observable<ApiResponse<PlanFollowUp>> {
    return this.http.put<ApiResponse<PlanFollowUp>>(`${this.base}/follow-ups/${id}`, body);
  }

  deleteFollowUp(id: number): Observable<ApiResponse<void>> {
    return this.http.delete<ApiResponse<void>>(`${this.base}/follow-ups/${id}`);
  }

  getPlanProgress(planId: number): Observable<ApiResponse<PlanProgress>> {
    return this.http.get<ApiResponse<PlanProgress>>(`${this.base}/improvement-plans/${planId}/progress`);
  }
}
