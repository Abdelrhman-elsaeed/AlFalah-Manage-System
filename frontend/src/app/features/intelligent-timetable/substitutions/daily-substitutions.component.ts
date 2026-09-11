import { CommonModule } from '@angular/common';
import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { CalendarModule } from 'primeng/calendar';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { TagModule } from 'primeng/tag';
import { ApiResponse } from '../../../core/models/api-response.model';
import { DailySubstitution, SwapCandidate, SwapCandidates, SwapLesson } from '../../../core/models/timetable-substitution.models';
import { ReviewTimetableOption } from '../../../core/models/timetable-review.models';
import { TimetableSubstitutionService } from '../../../core/services/timetable-substitution.service';

@Component({
  selector: 'app-daily-substitutions', standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ButtonModule, CalendarModule, DialogModule, DropdownModule,
    InputTextModule, InputTextareaModule, TagModule],
  templateUrl: './daily-substitutions.component.html', styleUrls: ['./daily-substitutions.component.css']
})
export class DailySubstitutionsComponent implements OnInit {
  private readonly api = inject(TimetableSubstitutionService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroy = inject(DestroyRef);
  timetables: ReviewTimetableOption[] = []; timetableId: number | null = null;
  date = new Date(); dashboard: DailySubstitution | null = null; teacherId: number | null = null;
  busy = false; error = ''; notice = ''; search = ''; candidateSearch = '';
  mode = 'Substitution'; source: SwapLesson | null = null; proposals: SwapCandidates | null = null;
  selected: SwapCandidate | null = null; dialog = false; reason = ''; requestId = '';
  readonly modes = [{ label: 'احتياطي لهذا اليوم فقط', value: 'Substitution' }, { label: 'تبديل حصص الجدول', value: 'Swap' }];
  readonly labels = { Green: 'متاح تماماً', Yellow: 'متاح مع تجاوز', Red: 'غير متاح' };
  readonly severities = { Green: 'success', Yellow: 'warning', Red: 'danger' } as const;
  readonly icons = { Green: 'pi pi-check-circle', Yellow: 'pi pi-exclamation-triangle', Red: 'pi pi-ban' };
  async ngOnInit() {
    await this.perform(async () => {
      this.timetables = this.unwrap(await firstValueFrom(this.api.list().pipe(takeUntilDestroyed(this.destroy))));
      const id = Number(this.route.snapshot.queryParamMap.get('timetableId'));
      this.timetableId = this.timetables.find(t => t.id === id)?.id ?? this.timetables.find(t => t.isPublished)?.id ?? this.timetables[0]?.id ?? null;
      const date = this.route.snapshot.queryParamMap.get('date');
      if (date && /^\d{4}-\d{2}-\d{2}$/.test(date)) this.date = new Date(date + 'T12:00:00');
      await this.load();
      const sourceId = Number(this.route.snapshot.queryParamMap.get('sourceEntryId'));
      const source = this.dashboard?.lessons.find(l => l.entryIds.includes(sourceId));
      if (source && this.dashboard?.canManage) { this.mode = 'Swap'; await this.find(source); }
    });
  }
  private unwrap<T>(r: ApiResponse<T>): T { if (!r.isSuccess || r.data == null) throw new Error(r.errors?.[0] ?? r.message); return r.data; }
  private async perform(action: () => Promise<void>) {
    if (this.busy) return; this.busy = true; this.error = ''; this.notice = '';
    try { await action(); }
    catch (e: any) {
      this.error = e?.error?.errors?.[0] ?? e?.error?.message ?? e?.message ?? 'تعذر الاتصال بالخادم.';
      if (e?.status === 409) {
        this.dialog = false; this.selected = null; this.proposals = null;
        this.error = 'تغير الجدول أو انتهت صلاحية الاقتراح. تم تحديث البيانات؛ راجع البدائل مجدداً.';
        try { await this.load(); if (this.source) await this.find(this.source); } catch { this.source = null; }
      }
    } finally { this.busy = false; }
  }
  private dateKey() { return `${this.date.getFullYear()}-${String(this.date.getMonth() + 1).padStart(2, '0')}-${String(this.date.getDate()).padStart(2, '0')}`; }
  private async load() {
    this.dashboard = this.timetableId ? this.unwrap(await firstValueFrom(this.api.daily(this.timetableId, this.dateKey()).pipe(takeUntilDestroyed(this.destroy)))) : null;
  }
  async refresh() { this.cancel(); this.dashboard = null; await this.perform(() => this.load()); }
  cancel() { if (this.busy) return; this.source = null; this.proposals = null; this.selected = null; this.dialog = false; this.candidateSearch = ''; }
  get teachers() { return [...new Map((this.dashboard?.lessons ?? []).map(l => [l.teacherId, { id: l.teacherId, name: l.teacherName }])).values()]; }
  get lessons() { return (this.dashboard?.lessons ?? []).filter(l => (!this.teacherId || l.teacherId === this.teacherId) &&
    [l.subject, l.classroom, l.teacherName, l.periods.join(' ')].join(' ').includes(this.search.trim())); }
  get candidates() { return this.proposals?.candidates.filter(c => c.label.includes(this.candidateSearch.trim())) ?? []; }
  async selectSource(lesson: SwapLesson) { await this.perform(() => this.find(lesson)); }
  private async find(lesson: SwapLesson) {
    this.source = lesson; this.proposals = null;
    this.proposals = this.unwrap(await firstValueFrom(this.api.candidates(this.timetableId!, this.dateKey(), lesson.entryId, this.mode).pipe(takeUntilDestroyed(this.destroy))));
  }
  choose(candidate: SwapCandidate) {
    if (this.busy || candidate.color === 'Red' || candidate.color === 'Yellow' && !this.proposals?.canOverride) return;
    this.selected = candidate; this.reason = ''; this.requestId = crypto.randomUUID(); this.dialog = true;
  }
  get expired() { return !!this.proposals && Date.parse(this.proposals.expiresAt) <= Date.now(); }
  async confirm() {
    if (!this.selected || !this.proposals || this.expired || this.selected.color === 'Yellow' && (!this.proposals.canOverride || !this.reason.trim())) return;
    await this.perform(async () => {
      const result = this.unwrap(await firstValueFrom(this.api.execute(this.proposals!, this.selected!.id, this.requestId, this.reason.trim() || null).pipe(takeUntilDestroyed(this.destroy))));
      this.dialog = false; this.source = null; this.proposals = null; this.selected = null;
      await this.load(); this.notice = `تم اعتماد التغيير — المراجعة ${result.afterRevision}. ${this.dashboard?.isPublished ? 'التغيير سارٍ وتمت جدولة إشعارات المعلمين.' : 'تم تحديث المسودة.'}`;
    });
  }
}
