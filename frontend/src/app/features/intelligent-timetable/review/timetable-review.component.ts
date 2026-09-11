import { CommonModule } from '@angular/common';
import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { ApiResponse } from '../../../core/models/api-response.model';
import { RepairProposal, ReviewEntry, ReviewTimetableOption, TimetableReviewResult, ValidationFinding } from '../../../core/models/timetable-review.models';
import { TimetableReviewService } from '../../../core/services/timetable-review.service';

@Component({
  selector: 'app-timetable-review', standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ButtonModule, DialogModule, DropdownModule, InputTextModule,
    InputTextareaModule, TableModule, TagModule, TooltipModule],
  templateUrl: './timetable-review.component.html', styleUrls: ['./timetable-review.component.css']
})
export class TimetableReviewComponent implements OnInit {
  private readonly api = inject(TimetableReviewService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroy = inject(DestroyRef);
  timetables: ReviewTimetableOption[] = []; timetableId: number | null = null;
  review: TimetableReviewResult | null = null; busy = false; error = ''; notice = '';
  severity: number | null = null; teacherId: number | null = null; classroomId: number | null = null;
  subjectId: number | null = null; search = ''; status: string | null = null;
  readonly severities = [{ label: 'أخطاء حرجة', value: 1 }, { label: 'تنبيهات', value: 2 }];
  readonly statuses = [{ label: 'نشطة', value: 'active' }, { label: 'متجاوزة', value: 'overridden' },
    { label: 'فترات مغلقة', value: 'closed' }, { label: 'إسناد مفقود', value: 'missing' }];
  readonly days = ['', 'السبت', 'الأحد', 'الاثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة'];
  proposals: RepairProposal[] = []; selected: RepairProposal | null = null; repairOpen = false;
  overrideOpen = false; overrideFinding: ValidationFinding | null = null; reason = '';
  view: 'classes' | 'teachers' = 'classes'; day = 2; density = 'normal';

  async ngOnInit() {
    await this.perform(async () => {
      this.timetables = this.unwrap(await firstValueFrom(this.api.list().pipe(takeUntilDestroyed(this.destroy))));
      const requested = Number(this.route.snapshot.queryParamMap.get('timetableId'));
      this.timetableId = this.timetables.find(x => x.id === requested)?.id ?? this.timetables[0]?.id ?? null;
      if (this.timetableId) await this.load();
    });
  }
  private unwrap<T>(r: ApiResponse<T>): T {
    if (!r.isSuccess || r.data == null) throw new Error(r.errors?.[0] ?? r.message ?? 'تعذر إتمام العملية.');
    return r.data;
  }
  private async perform(action: () => Promise<void>) {
    if (this.busy) return;
    this.busy = true; this.error = ''; this.notice = '';
    try { await action(); }
    catch (e: any) { this.error = e?.error?.errors?.[0] ?? e?.error?.message ?? e?.message ?? 'تعذر الاتصال بالخادم.'; }
    finally { this.busy = false; }
  }
  private async load() {
    if (!this.timetableId) { this.review = null; return; }
    this.review = this.unwrap(await firstValueFrom(this.api.evaluate(this.timetableId).pipe(takeUntilDestroyed(this.destroy))));
    if (!this.review.periods.some(p => p.day === this.day)) this.day = this.review.periods[0]?.day ?? 2;
  }
  async refresh() { await this.perform(() => this.load()); }
  async changeTimetable() {
    this.review = null; this.teacherId = this.classroomId = this.subjectId = null;
    this.repairOpen = this.overrideOpen = false;
    await this.refresh();
  }
  get findings() {
    return (this.review?.findings ?? []).filter(f => (!this.severity || f.severity === this.severity) &&
      (!this.teacherId || f.instructorProfileId === this.teacherId) && (!this.classroomId || f.classroomId === this.classroomId) &&
      (!this.subjectId || f.subjectId === this.subjectId) && (!this.status ||
        this.status === 'active' && !f.isOverridden || this.status === 'overridden' && f.isOverridden ||
        this.status === 'closed' && f.ruleCode === 4 || this.status === 'missing' && f.ruleCode === 6) &&
      (!this.search.trim() || [f.messageAr, f.ruleNameAr, f.teacherName, f.classroomName, f.subjectName].join(' ').includes(this.search.trim())));
  }
  async suggest(f: ValidationFinding) {
    await this.perform(async () => {
      this.proposals = []; this.selected = null; this.repairOpen = true;
      this.proposals = this.unwrap(await firstValueFrom(this.api.proposals(this.review!.timetableId, f.id).pipe(takeUntilDestroyed(this.destroy))));
    });
  }
  async apply() {
    if (!this.selected || !this.review) return;
    await this.perform(async () => {
      this.review = this.unwrap(await firstValueFrom(this.api.apply(this.review!.timetableId, this.selected!).pipe(takeUntilDestroyed(this.destroy))));
      this.repairOpen = false; this.notice = 'تم تطبيق الإصلاح وحفظ نسخة جديدة وإعادة التحليل.';
    });
  }
  openOverride(f: ValidationFinding) { this.overrideFinding = f; this.reason = ''; this.overrideOpen = true; }
  async override() {
    if (!this.overrideFinding || !this.reason.trim()) return;
    await this.perform(async () => {
      this.unwrap(await firstValueFrom(this.api.override(this.overrideFinding!.id, this.reason.trim()).pipe(takeUntilDestroyed(this.destroy))));
      this.overrideOpen = false; await this.load(); this.notice = 'تم تسجيل التجاوز وسببه.';
    });
  }
  async publish() {
    if (!this.review?.canPublish) return;
    await this.perform(async () => {
      this.unwrap(await firstValueFrom(this.api.publish(this.review!.timetableId, this.review!.timetableRevision).pipe(takeUntilDestroyed(this.destroy))));
      await this.load(); this.notice = 'تم نشر الجدول بعد التحقق من القيود الصارمة.';
    });
  }
  get gridRows() { return this.view === 'classes' ? (this.review?.classrooms ?? []).filter(x => !this.classroomId || x.id === this.classroomId)
    : (this.review?.teachers ?? []).filter(x => !this.teacherId || x.id === this.teacherId); }
  get gridPeriods() { return this.review?.periods.filter(x => x.day === Number(this.day)) ?? []; }
  get gridIntervals() { return [...this.gridPeriods.map(p => ({ ...p, name: `الحصة ${p.period}` })),
    ...(this.review?.breaks ?? []).filter(b => b.day === Number(this.day)).map(b => ({ ...b, period: 0 }))].sort((a,b) => a.start.localeCompare(b.start)); }
  get studyDays() { return [...new Set(this.review?.periods.map(x => x.day) ?? [])]; }
  navigateDay(delta: number) {
    const days = this.studyDays; if (days.length) this.day = days[(days.indexOf(Number(this.day)) + delta + days.length) % days.length];
  }
  cells(row: number, period: number): ReviewEntry[] {
    return this.review?.entries.filter(e => e.day === Number(this.day) && e.period === period &&
      (this.view === 'classes' ? e.classroomId === row : e.instructorProfileId === row)) ?? [];
  }
  closed(row: number, period: number) { return this.view === 'teachers' && this.review?.unavailable.some(x => x.instructorProfileId === row && x.day === Number(this.day) && x.period === period); }
  dim(e: ReviewEntry) { return !!(this.subjectId && e.subjectId !== this.subjectId || this.teacherId && e.instructorProfileId !== this.teacherId ||
    this.classroomId && e.classroomId !== this.classroomId || this.search.trim() && ![e.teacherName, e.subjectName].join(' ').includes(this.search.trim())); }
  swapDate(day: number) {
    const date = new Date(); const target = (day + 5) % 7;
    date.setDate(date.getDate() + (target - date.getDay() + 7) % 7);
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
  }
  teacherName(id: number) { return this.review?.teachers.find(x => x.id === id)?.name ?? `معلم ${id}`; }
  lessonName(id: number) { const e = this.review?.entries.find(x => x.id === id); return e ? `${e.subjectName ?? 'حصة'} · ${e.classroomName ?? ''}` : `حصة ${id}`; }
}
