import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { RepairProposal, ReviewTimetableOption, TimetableReviewResult, ViolationRuleCode, ViolationSeverity } from '../models/timetable-review.models';
import { TimetableService } from './timetable.service';

@Injectable({ providedIn: 'root' })
export class TimetableReviewService {
  private readonly http = inject(HttpClient);
  private readonly timetables = inject(TimetableService);
  private readonly base = `${environment.apiUrl}/api/v1/intelligent-timetable/review`;
  list() { return this.http.get<ApiResponse<ReviewTimetableOption[]>>(this.base); }
  evaluate(id: number) { return this.http.get<ApiResponse<TimetableReviewResult>>(`${this.base}/${id}/evaluate`).pipe(map(r => this.normalize(r))); }
  proposals(id: number, finding: number) { return this.http.get<ApiResponse<RepairProposal[]>>(`${this.base}/${id}/findings/${finding}/repairs`).pipe(map(r => ({ ...r,
    data: r.data?.map(p => ({ ...p, movements: p.movements.map(m => ({ ...m, fromDay: this.day(m.fromDay), toDay: this.day(m.toDay) })) })) ?? null }))); }
  override(finding: number, reason: string) { return this.http.post<ApiResponse<boolean>>(`${this.base}/findings/${finding}/override`, { reason }); }
  apply(id: number, proposal: RepairProposal) { return this.http.post<ApiResponse<TimetableReviewResult>>(`${this.base}/${id}/apply-repair`, proposal).pipe(map(r => this.normalize(r))); }
  publish(id: number, revision: number) { return this.timetables.publish(id, revision); }
  private day(value: number | string): number {
    return typeof value === 'number' ? value : ['Saturday', 'Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'].indexOf(value) + 1;
  }
  private normalize(r: ApiResponse<TimetableReviewResult>): ApiResponse<TimetableReviewResult> {
    if (!r.data) return r;
    return { ...r, data: { ...r.data, entries: r.data.entries.map(e => ({ ...e, day: this.day(e.day) })),
      findings: r.data.findings.map(f => ({ ...f, day: f.day == null ? null : this.day(f.day),
        severity: typeof f.severity === 'string' ? ViolationSeverity[f.severity as keyof typeof ViolationSeverity] : f.severity,
        ruleCode: typeof f.ruleCode === 'string' ? ViolationRuleCode[f.ruleCode as keyof typeof ViolationRuleCode] : f.ruleCode })) } };
  }
}
