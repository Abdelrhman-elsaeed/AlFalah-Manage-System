import { CommonModule } from '@angular/common';
import { Component, HostListener, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { finalize } from 'rxjs';
import { BellDay, BellPeriod, BellSchedule, SaveBellSchedule, effectivePeriods, periodErrors } from '../../../core/models/bell-schedule.models';
import { TimetableSettingsOverview } from '../../../core/models/timetable-settings.models';
import { BellScheduleService } from '../../../core/services/bell-schedule.service';
import { TimetableSettingsService } from '../../../core/services/timetable-settings.service';
import { ToastService } from '../../../core/services/toast.service';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import { ClearableSelectComponent } from '../../../shared/components/clearable-select/clearable-select.component';
import { TimetableBreaksEditorComponent } from '../breaks/timetable-breaks-editor.component';
import { breakErrors, effectiveBreaks, scheduleBreakIssues } from '../../../core/models/schedule-break.models';

@Component({
  selector: 'app-timetable-timings', standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ButtonModule, DialogModule, InputTextModule, ClearableSelectComponent, TimetableBreaksEditorComponent],
  templateUrl: './timetable-timings.component.html', styleUrl: './timetable-timings.component.css'
})
export class TimetableTimingsComponent implements OnInit {
  private readonly api = inject(BellScheduleService);
  private readonly settings = inject(TimetableSettingsService);
  private readonly toast = inject(ToastService);
  activeTab: 'periods' | 'breaks' = inject(ActivatedRoute, { optional: true })?.snapshot.data['tab'] === 'breaks' ? 'breaks' : 'periods';
  overview: TimetableSettingsOverview | null = null;
  templates: BellSchedule[] = [];
  draft: SaveBellSchedule | null = null;
  selectedId: number | null = null;
  profileId: number | null = null;
  yearId = 0; semester = 1; activeDay = 0;
  private contextVersion = 0;
  loading = true; saving = false; error = ''; baseline = 'null';
  selectedDays = new Set<number>();
  comparison: { day: number; before: BellPeriod[]; after: BellPeriod[] }[] = [];
  comparisonVisible = false;
  readonly dayNames = ['كل الأيام', 'السبت', 'الأحد', 'الاثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة'];
  readonly semesters = [{ label: 'الفصل الدراسي الأول', value: 1 }, { label: 'الفصل الدراسي الثاني', value: 2 }];
  readonly zones = [{ label: 'الرياض — Asia/Riyadh', value: 'Asia/Riyadh' }, { label: 'القاهرة — Africa/Cairo', value: 'Africa/Cairo' }, { label: 'UTC', value: 'UTC' }];
  get canManage() { return this.overview?.canManage ?? false; }
  get periods(): BellPeriod[] { return !this.draft ? [] : this.activeDay === 0 ? this.draft.defaultPeriods : effectivePeriods(this.draft, this.activeDay); }
  get studyDays(): BellDay[] { return this.draft?.days.filter(x => x.isStudyDay) ?? []; }
  get breakIssues() { return this.draft ? scheduleBreakIssues(this.draft) : []; }
  get otherBreakIssueDays() {
    const issues = this.breakIssues;
    const visible = new Set(issues.filter(x => x.day === this.activeDay).map(x => x.message));
    return [...new Set(issues.filter(x => x.day !== this.activeDay && !visible.has(x.message)).map(x => x.day))];
  }
  breakCount(day: number) { return this.draft ? effectiveBreaks(this.draft, day).length : 0; }
  get errors() {
    const errors = periodErrors(this.periods);
    if (this.draft) {
      const days = this.activeDay ? [this.activeDay] : [0, ...this.studyDays.filter(x => x.usesDefaultSchedule).map(x => x.day)];
      for (const day of days) for (const index of breakErrors(effectiveBreaks(this.draft, day), this.periods).lessons.keys())
        errors.set(index, [...new Set([...(errors.get(index) ?? []), 'يوجد تداخل زمني مع الاستراحات. يرجى مراجعة الأوقات.'])]);
    }
    return errors;
  }
  get valid() {
    return !!this.draft?.name.trim() && this.draft.name.length <= 120 && !!this.draft.schoolTimeZoneId && this.studyDays.length > 0
      && this.draft.defaultPeriods.length > 0 && !periodErrors(this.draft.defaultPeriods).size
      && this.studyDays.every(x => effectivePeriods(this.draft!, x.day).length > 0 && !periodErrors(effectivePeriods(this.draft!, x.day)).size)
      && this.breakIssues.length === 0;
  }
  ngOnInit() { this.loadContext(); }
  hasUnsavedChanges() { return this.canManage && JSON.stringify(this.draft) !== this.baseline; }
  @HostListener('window:beforeunload', ['$event']) beforeUnload(event: BeforeUnloadEvent) {
    if (this.hasUnsavedChanges()) { event.preventDefault(); event.returnValue = ''; }
  }
  confirmDiscard() { return !this.hasUnsavedChanges() || window.confirm('لديك تغييرات غير محفوظة. هل تريد تجاهلها؟'); }
  changeContext(year: number, semester: number) {
    if (year === this.yearId && semester === this.semester) return;
    if (!this.confirmDiscard()) return;
    this.yearId = year; this.semester = semester; this.loadContext();
  }
  loadContext() {
    const version = ++this.contextVersion;
    this.loading = true; this.error = ''; this.draft = null; this.baseline = 'null';
    this.settings.getOverview(this.yearId || undefined, this.semester).subscribe({ next: response => {
      if (version !== this.contextVersion) return;
      if (!response.data) { this.loading = false; this.error = response.message || 'تعذر تحميل إعدادات المدرسة.'; return; }
      this.overview = response.data; this.yearId = response.data.selectedAcademicYearId;
      this.profileId = response.data.selectedProfile?.id ?? null;
      this.loadTemplates();
    }, error: e => { if (version !== this.contextVersion) return; this.loading = false; this.error = extractHttpErrorMessage(e) ?? 'تعذر تحميل الإعدادات.'; } });
  }
  loadTemplates(preferredId?: number) {
    const version = this.contextVersion;
    this.loading = true;
    this.api.list(this.yearId, this.semester).subscribe({ next: response => {
      if (version !== this.contextVersion) return;
      this.loading = false;
      this.templates = response.data ?? [];
      const item = this.templates.find(x => x.id === preferredId)
        ?? this.templates.find(x => x.selectedByProfileIds.includes(this.profileId ?? 0)) ?? this.templates[0];
      item ? this.apply(item) : this.newTemplate(false);
    }, error: e => { if (version !== this.contextVersion) return; this.loading = false; this.error = extractHttpErrorMessage(e) ?? 'تعذر تحميل التوقيتات.'; } });
  }
  choose(id: number) {
    if (id === this.selectedId) return;
    if (!this.confirmDiscard()) return;
    const item = this.templates.find(x => x.id === id); if (item) this.apply(item);
  }
  private apply(item: BellSchedule) {
    this.selectedId = item.id;
    this.draft = structuredClone(item);
    this.draft.defaultBreaks ??= [];
    this.draft.days.forEach(day => { day.usesDefaultBreaks ??= true; day.breaks ??= []; });
    if (!this.zones.some(x => x.value === item.schoolTimeZoneId)) this.zones.push({ label: item.schoolTimeZoneId, value: item.schoolTimeZoneId });
    this.baseline = JSON.stringify(this.draft); this.activeDay = 0; this.selectedDays.clear(); this.error = '';
  }
  newTemplate(confirm = true) {
    if (confirm && !this.confirmDiscard()) return;
    this.selectedId = null; this.activeDay = 0; this.selectedDays.clear();
    this.draft = { academicYearId: this.yearId, semester: this.semester, name: '', revision: 0,
      schoolTimeZoneId: this.templates[0]?.schoolTimeZoneId ?? 'Asia/Riyadh',
      defaultPeriods: [{ sequence: 1, displayLabel: '', startLocalTime: '07:00:00', endLocalTime: '07:45:00' }], defaultBreaks: [],
      days: Array.from({ length: 7 }, (_, i) => ({ day: i + 1, isStudyDay: i >= 1 && i <= 5, usesDefaultSchedule: true, periods: [], usesDefaultBreaks: true, breaks: [] })) };
    this.baseline = JSON.stringify(this.draft);
  }
  setStudyDay(day: BellDay, study: boolean) {
    if (!study && (day.periods.length || day.breaks?.length) && !window.confirm('تحويل اليوم إلى عطلة سيحذف توقيته المخصص واستراحاته. هل تريد المتابعة؟')) return;
    day.isStudyDay = study;
    if (!study) { day.periods = []; day.usesDefaultSchedule = true; day.breaks = []; day.usesDefaultBreaks = true; this.selectedDays.delete(day.day); if (this.activeDay === day.day) this.activeDay = 0; }
  }
  effective(day: number) { return this.draft ? effectivePeriods(this.draft, day) : []; }
  inherited() { return this.activeDay !== 0 && this.draft?.days.find(x => x.day === this.activeDay)?.usesDefaultSchedule; }
  private editable(): BellPeriod[] {
    if (!this.draft) return [];
    if (this.activeDay === 0) return this.draft.defaultPeriods;
    const day = this.draft.days.find(x => x.day === this.activeDay)!;
    if (day.usesDefaultSchedule) { day.periods = structuredClone(this.draft.defaultPeriods); day.usesDefaultSchedule = false; }
    return day.periods;
  }
  trackPeriod(index: number) { return index; }
  edit(index: number, field: 'displayLabel' | 'startLocalTime' | 'endLocalTime', value: string) {
    this.editable()[index][field] = field === 'displayLabel' ? value : value.length === 5 ? value + ':00' : value;
  }
  resize(count: number) {
    if (!Number.isInteger(count) || count < 1 || count === this.periods.length) return;
    if (count < this.periods.length && !window.confirm(`سيتم حذف ${this.periods.length - count} حصة من نهاية هذا التوقيت. هل تريد المتابعة؟`)) return;
    const rows = this.editable();
    while (rows.length > count) rows.pop();
    while (rows.length < count) {
      const last = rows[rows.length - 1];
      const start = last?.endLocalTime ?? '07:00:00';
      const minutes = Number(start.slice(0, 2)) * 60 + Number(start.slice(3, 5)) + 45;
      rows.push({ sequence: rows.length + 1, displayLabel: '', startLocalTime: start,
        endLocalTime: minutes < 1440 ? `${String(Math.floor(minutes / 60)).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}:00` : '' });
    }
  }
  inherit() {
    if (!window.confirm('سيتم استبدال توقيت هذا اليوم بالتوقيت الافتراضي. هل تريد المتابعة؟')) return;
    const day = this.draft!.days.find(x => x.day === this.activeDay)!; day.usesDefaultSchedule = true; day.periods = [];
  }
  toggleDay(day: number, checked: boolean) { checked ? this.selectedDays.add(day) : this.selectedDays.delete(day); }
  previewApply() {
    this.comparison = [...this.selectedDays].map(day => ({ day, before: structuredClone(this.effective(day)), after: structuredClone(this.periods) }));
    this.comparisonVisible = this.comparison.length > 0;
  }
  applyToDays() {
    const candidate = structuredClone(this.draft!);
    for (const change of this.comparison) {
      const day = candidate.days.find(x => x.day === change.day)!;
      day.usesDefaultSchedule = false; day.periods = structuredClone(change.after);
    }
    const issues = scheduleBreakIssues(candidate).filter(x => this.comparison.some(day => day.day === x.day));
    if (issues.length) {
      this.error = `تعذر التطبيق. يرجى مراجعة الأوقات في: ${[...new Set(issues.map(x => this.dayNames[x.day]))].join('، ')}.`;
      this.comparisonVisible = false; return;
    }
    for (const change of this.comparison) {
      const day = this.draft!.days.find(x => x.day === change.day)!;
      day.usesDefaultSchedule = false; day.periods = structuredClone(change.after);
    }
    this.comparisonVisible = false; this.selectedDays.clear();
  }
  duration(period: BellPeriod) {
    const seconds = (time: string) => { const [h, m, s] = time.split(':').map(Number); return h * 3600 + m * 60 + (s || 0); };
    return Math.round((seconds(period.endLocalTime) - seconds(period.startLocalTime)) / 6) / 10;
  }
  save() {
    if (!this.valid || this.saving || !this.canManage || !this.draft) return;
    this.saving = true; this.error = '';
    this.api.save(this.selectedId, this.draft).pipe(finalize(() => this.saving = false)).subscribe({ next: response => {
      if (response.data) { this.apply(response.data); this.toast.success('تم حفظ جميع التغييرات'); this.refreshProfiles(); this.loadTemplates(response.data.id); }
      else this.error = response.errors?.join('، ') || response.message || 'تعذر الحفظ.';
    }, error: e => { this.error = extractHttpErrorMessage(e) ?? 'تعذر الحفظ. احتفظنا بتغييراتك؛ أعد التحميل عند تعارض النسخ.'; } });
  }
  selectForProfile() {
    const profile = this.overview?.profiles.find(x => x.id === this.profileId);
    if (!profile || !this.selectedId || this.saving || this.hasUnsavedChanges()) return;
    this.saving = true;
    this.api.select(profile.id, this.selectedId, profile.revision).pipe(finalize(() => this.saving = false)).subscribe({ next: response => {
      if (response.isSuccess) { this.toast.success('تم اختيار قالب التوقيت'); this.refreshProfiles(); this.loadTemplates(this.selectedId!); }
      else this.error = response.message ?? 'تعذر اختيار القالب.';
    }, error: e => this.error = extractHttpErrorMessage(e) ?? 'تعذر اختيار القالب.' });
  }
  private refreshProfiles() {
    this.settings.getOverview(this.yearId, this.semester, this.profileId ?? undefined).subscribe({ next: r => this.overview = r.data ?? this.overview });
  }
  discard() {
    if (!this.confirmDiscard()) return;
    this.draft = JSON.parse(this.baseline); this.activeDay = 0; this.error = '';
  }
}
