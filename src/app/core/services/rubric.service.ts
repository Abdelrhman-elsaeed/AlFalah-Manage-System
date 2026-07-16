import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse } from '../models/api-response.model';
import { environment } from '../../../environments/environment';
import {
  CreateRubricVersionDto,
  RubricVersionDto,
  RubricVersionListDto,
  ScoreScaleDto
} from '../models/rubric.models';

@Injectable({ providedIn: 'root' })
export class RubricService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/v1/rubric`;

  /** GET /api/v1/rubric/active — full tree for active version */
  getActive(): Observable<ApiResponse<RubricVersionDto>> {
    return this.http.get<ApiResponse<RubricVersionDto>>(`${this.base}/active`);
  }

  /** GET /api/v1/rubric/versions — lightweight version list */
  getVersions(): Observable<ApiResponse<RubricVersionListDto[]>> {
    return this.http.get<ApiResponse<RubricVersionListDto[]>>(`${this.base}/versions`);
  }

  /** GET /api/v1/rubric/versions/:id — full tree for one version */
  getVersionById(id: number): Observable<ApiResponse<RubricVersionDto>> {
    return this.http.get<ApiResponse<RubricVersionDto>>(`${this.base}/versions/${id}`);
  }

  /** POST /api/v1/rubric/versions — copy-on-write: creates new version */
  createVersion(dto: CreateRubricVersionDto): Observable<ApiResponse<RubricVersionDto>> {
    return this.http.post<ApiResponse<RubricVersionDto>>(`${this.base}/versions`, dto);
  }

  /** POST /api/v1/rubric/versions/:id/activate */
  activateVersion(id: number): Observable<ApiResponse<RubricVersionDto>> {
    return this.http.post<ApiResponse<RubricVersionDto>>(`${this.base}/versions/${id}/activate`, {});
  }

  /** GET /api/v1/rubric/score-scale — read-only constants */
  getScoreScale(): Observable<ApiResponse<ScoreScaleDto>> {
    return this.http.get<ApiResponse<ScoreScaleDto>>(`${this.base}/score-scale`);
  }
}
