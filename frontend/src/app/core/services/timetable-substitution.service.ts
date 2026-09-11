import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { DailySubstitution, SwapCandidates, SubstitutionHistory } from '../models/timetable-substitution.models';
import { ReviewTimetableOption } from '../models/timetable-review.models';

@Injectable({ providedIn: 'root' })
export class TimetableSubstitutionService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/v1/intelligent-timetable/substitutions`;
  list() { return this.http.get<ApiResponse<ReviewTimetableOption[]>>(this.base); }
  daily(id: number, date: string) { return this.http.get<ApiResponse<DailySubstitution>>(`${this.base}/${id}`, { params: { date } }); }
  candidates(id: number, date: string, sourceEntryId: number, mode: string) {
    return this.http.get<ApiResponse<SwapCandidates>>(`${this.base}/${id}/candidates`, { params: { date, sourceEntryId, mode } });
  }
  execute(proposals: SwapCandidates, proposalId: string, requestId: string, overrideReason: string | null) {
    return this.http.post<ApiResponse<SubstitutionHistory>>(`${this.base}/${proposals.timetableId}/execute`, {
      requestId, revision: proposals.revision, date: proposals.date, sourceEntryId: proposals.sourceEntryId,
      mode: proposals.mode, proposalId, expiresAt: proposals.expiresAt, overrideReason
    });
  }
}
