import { CommonModule } from '@angular/common';
import { Component, DestroyRef, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { CalendarModule } from 'primeng/calendar';
import { InputTextModule } from 'primeng/inputtext';
import { timer } from 'rxjs';
import { ClearableSelectComponent } from '../../../shared/components/clearable-select/clearable-select.component';
import { SchoolDutyService } from '../../../core/services/school-duty.service';
import { DutyChange, DutyDay, DutyHistory, DutyPreview, DutySettings, DutyWorkspace } from '../../../core/models/school-duty.models';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';

@Component({
  selector: 'app-school-duty', standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule, ButtonModule, DialogModule, CalendarModule, InputTextModule, ClearableSelectComponent],
  templateUrl: './school-duty.component.html', styleUrls: ['./duty-dialog.css', './school-duty.component.css']
})
export class SchoolDutyComponent implements OnInit {
  private readonly api = inject(SchoolDutyService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroy = inject(DestroyRef);
  readonly data = signal<DutyWorkspace | null>(null);
  readonly language = signal(this.translate.currentLang || 'ar');
  readonly loading = signal(true); readonly busy = signal(false); readonly error = signal(''); readonly success = signal('');
  readonly view = signal<'week' | 'term'>('week'); readonly week = signal(''); readonly filter = signal('all'); readonly teacher = signal<string | null>(null);
  readonly today = computed(() => this.data()?.days.find(x => x.date === this.data()?.today));
  readonly next = computed(() => this.data()?.days.find(x => x.date >= (this.data()?.today ?? '') && x.isMine));
  readonly exceptions = computed(() => this.data()?.days.filter(x => x.exceptionCode).length ?? 0);
  readonly debts = computed(() => this.data()?.history.filter(x => x.unresolved) ?? []);
  readonly ownCount = computed(() => this.data()?.days.filter(x => x.isMine).length ?? 0);
  readonly weeks = computed(() => [...new Set(this.data()?.days.map(x => this.weekOf(x.date)) ?? [])]);
  readonly visibleDays = computed(() => (this.data()?.days ?? []).filter(x =>
    (!this.teacher() || x.teacherId === this.teacher()) && (this.view() === 'term' || this.weekOf(x.date) === this.week()) &&
    (this.filter() === 'all' || this.filter() === 'exceptions' && !!x.exceptionCode || this.filter() === 'mine' && x.isMine || this.filter() === 'exchanged' && x.teacherId !== x.originalTeacherId)));
  readonly groups = computed(() => [...new Set(this.visibleDays().map(x => this.weekOf(x.date)))].map(week => ({ week, days: this.visibleDays().filter(x => this.weekOf(x.date) === week) })));
  readonly termOptions = computed(() => (this.data()?.terms ?? []).map(x => ({ id: x.id, name: `${this.t('TERM')} ${x.semester} · ${x.year}` })));
  readonly teacherOptions = computed(() => (this.data()?.teachers ?? []).filter(x => x.isEligible !== false));
  readonly scheduleTeachers = computed(() => [...new Map((this.data()?.days ?? []).map(x => [x.teacherId, { userId: x.teacherId, name: x.teacher }])).values()]);
  readonly order = computed(() => this.draft()?.teacherIds.map(id => this.data()?.teachers.find(x => x.userId === id)).filter(Boolean) ?? []);
  readonly draft = signal<DutySettings | null>(null);
  setupOpen = false; previewOpen = false; manualOpen = false; detailOpen = false; historyOpen = false;
  preview: DutyPreview | null = null; manualPreview: DutyPreview | null = null; selected: DutyDay | null = null;
  search = ''; termId: number | null = null; reason = ''; substitute: string | null = null; otherId: number | null = null;
  mode = 'Exchange'; acceptException = false; resolveId: number | null = null; manualRequest: DutyChange | null = null;
  exclusionTeacher: string | null = null; exclusionFrom: Date | null = null; exclusionTo: Date | null = null; exclusionReason = '';
  closureFrom: Date | null = null; closureTo: Date | null = null; closureReason = '';
  private publishRequest: Parameters<SchoolDutyService['publish']>[0] | null = null;
  private loadSequence = 0;
  private refreshingReview = false;
  private readonly dateCache = new Map<string, Date>();
  readonly weekdays = [0,1,2,3,4,5,6];
  constructor() {
    // PrimeNG 17 binds Escape when a dialog opens and does not re-check a later
    // closeOnEscape change. Own the top modal's Escape without losing settings.
    const escape = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return;
      const target = event.target instanceof Element ? event.target : null;
      const control = target?.closest('.p-calendar,.p-datepicker,.p-dropdown,.p-dropdown-panel');
      const popupOpen = Array.from(document.querySelectorAll<HTMLElement>('.p-datepicker,.p-dropdown-panel')).some(x => x.getBoundingClientRect().width > 0 && x.getBoundingClientRect().height > 0);
      if (control && popupOpen) return;
      this.closeTopDialog(event);
    };
    document.addEventListener('keydown', escape, true);
    this.destroy.onDestroy(() => document.removeEventListener('keydown', escape, true));
  }
  readonly filteredTeachers = computed(() => this.data()?.teachers ?? []);
  readonly swapDates = computed(() => (this.data()?.days ?? []).filter(x => !x.locked && x.date >= (this.data()?.today ?? '') && x.id !== this.selected?.id && x.teacherId === this.substitute).map(x => ({ id: x.id, name: `${this.date(x.date)} · ${x.teacher}` })));
  // Plain editable dialog fields are intentionally evaluated on change detection.
  get availableSwapDates() { return (this.data()?.days ?? []).filter(x => !x.locked && x.date >= (this.data()?.today ?? '') && x.id !== this.selected?.id && x.teacherId === this.substitute).map(x => ({ id: x.id, name: this.date(x.date) })); }
  get availableCompensationDates() { return (this.data()?.days ?? []).filter(x => !x.locked && x.date >= (this.data()?.today ?? '')).map(x => ({ id: x.id, name: `${this.date(x.date, true)} · ${x.teacher}` })); }
  get roster() { return (this.data()?.teachers ?? []).filter(x => !this.search || x.name.includes(this.search)); }
  ngOnInit() {
    this.translate.onLangChange.pipe(takeUntilDestroyed(this.destroy)).subscribe(event => this.language.set(event.lang));
    this.load(); timer(10000, 10000).pipe(takeUntilDestroyed(this.destroy)).subscribe(() => {
      if (!this.hasDraft() && !this.detailOpen && !this.historyOpen && !this.busy() && document.visibilityState === 'visible') this.load(true);
    });
  }
  t(key: string) { this.language(); return this.translate.instant(`DUTY.${key}`); }
  get dir() { return this.language() === 'en' ? 'ltr' : 'rtl'; }
  date(value: string, weekday = false) { return new Intl.DateTimeFormat(this.dir === 'rtl' ? 'ar-SA-u-ca-gregory' : 'en-GB', { day: 'numeric', month: 'short', ...(weekday ? { weekday: 'long' as const } : {}) }).format(this.asDate(value)); }
  weekday(value: string) { return new Intl.DateTimeFormat(this.dir === 'rtl' ? 'ar-SA' : 'en-GB', { weekday: 'long' }).format(this.asDate(value)); }
  dayNumber(value: string) { return this.asDate(value).getDate(); }
  asDate(value: string) { let date = this.dateCache.get(value); if (!date) { date = new Date(`${value}T12:00:00`); this.dateCache.set(value, date); } return date; }
  iso(date: Date) { return `${date.getFullYear()}-${String(date.getMonth()+1).padStart(2,'0')}-${String(date.getDate()).padStart(2,'0')}`; }
  weekOf(date: string) { const d = new Date(this.asDate(date)); d.setDate(d.getDate()-d.getDay()); return this.iso(d); }
  hasDraft() { return this.setupOpen || this.manualOpen || this.previewOpen; }
  @HostListener('window:beforeunload', ['$event']) beforeUnload(event: BeforeUnloadEvent) { if (this.hasDraft()) event.preventDefault(); }
  closeTopDialog(event: KeyboardEvent) {
    if (!(this.previewOpen || this.manualOpen || this.setupOpen || this.detailOpen || this.historyOpen)) return;
    event.preventDefault(); event.stopImmediatePropagation();
    if (this.busy()) return;
    if (this.previewOpen) this.previewOpen = false;
    else if (this.manualOpen) this.manualOpen = false;
    else if (this.detailOpen) this.closeDetails();
    else if (this.historyOpen) this.historyOpen = false;
    else this.setupOpen = false;
  }
  load(background = false) {
    const sequence = ++this.loadSequence;
    if (!background) { this.loading.set(true); this.error.set(''); }
    this.api.context(this.termId).pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: r => {
      if (sequence !== this.loadSequence) return;
      this.loading.set(false);
      if (!r.isSuccess || !r.data) { this.error.set(r.message || this.t('ERROR')); return; }
      this.data.set(r.data); this.termId = r.data.termId;
      if (this.refreshingReview) {
        this.refreshingReview = false;
        if (this.selected) this.selected = r.data.days.find(x => x.id === this.selected?.id) ?? this.selected;
        this.invalidateManual(); this.previewOpen = false; this.preview = null; this.publishRequest = null;
        this.error.set(this.t('REVIEW_REFRESHED'));
      }
      if (!this.week() || !this.weeks().includes(this.week())) this.week.set(this.weeks().find(x => x >= this.weekOf(r.data!.today)) ?? this.weeks()[0] ?? '');
      const linked = this.route.snapshot.queryParamMap.get('date');
      if (linked && !background) { const linkedDay = r.data.days.find(x => x.date === linked); if (linkedDay) { this.week.set(this.weekOf(linked)); this.selected = linkedDay; this.detailOpen = true; } }
    }, error: e => { if (sequence !== this.loadSequence) return; this.loading.set(false); if (!background) this.error.set(extractHttpErrorMessage(e) || this.t('ERROR')); } });
  }
  switchTerm(id: number) { this.termId = id; this.week.set(''); this.load(); }
  moveWeek(delta: number) { const index = this.weeks().indexOf(this.week()); this.week.set(this.weeks()[Math.max(0, Math.min(this.weeks().length-1,index+delta))] ?? ''); }
  applyFilter(value: string) { this.filter.set(value); this.view.set('term'); }
  status(day: DutyDay) {
    if (day.exceptionCode) return this.t(`CODE_${day.exceptionCode}`);
    if (day.protected || ['Replace','Exchange','ResolveCompensation'].includes(day.changeKind || '')) return this.t('MANUAL');
    if (day.changeKind === 'Regenerate') return this.t('REGENERATED');
    if (day.changeKind === 'Undo') return this.t('CORRECTED');
    if (['AutoExchange','UnresolvedCompensation'].includes(day.changeKind || '')) return this.t('EXCHANGED');
    return this.t(day.teacherId !== day.originalTeacherId ? 'ADJUSTED' : 'SCHEDULED');
  }
  attendance(day: DutyDay) { return this.t(`ATTENDANCE_${day.attendance || 'Unknown'}`); }
  initials(name: string) { return name.split(' ').filter(Boolean).slice(0,2).map(x => x[0]).join(' '); }
  openDetails(day: DutyDay) { this.selected = day; this.detailOpen = true; void this.router.navigate([], { relativeTo: this.route, queryParams: { date: day.date }, queryParamsHandling: 'merge', replaceUrl: true }); }
  closeDetails() { this.detailOpen = false; void this.router.navigate([], { relativeTo: this.route, queryParams: { date: null }, queryParamsHandling: 'merge', replaceUrl: true }); }
  dayHistory() { return this.data()?.history.filter(x => x.date === this.selected?.date || x.otherDate === this.selected?.date) ?? []; }
  historyReason(history: DutyHistory) {
    if (history.kind === 'Publish' || history.kind === 'Regenerate') return this.t(`KIND_${history.kind}`);
    if (history.actor !== 'system') return history.reason;
    if (history.kind === 'AutoExchange' || history.kind === 'UnresolvedCompensation') return this.t(history.reason.includes('Excused') ? 'AUTOMATIC_EXCUSED' : 'AUTOMATIC_ABSENT');
    return this.t(`KIND_${history.kind}`);
  }
  compensation(day: DutyDay) { const history = this.data()?.history ?? []; return history.find(x => x.date === day.date && x.kind === 'AutoExchange' && !history.some(u => u.kind === 'Undo' && u.relatedChangeId === x.id))?.otherDate; }
  openSetup() {
    const d = this.data(); const term = d?.terms.find(x => x.id === d.termId); if (!d || !term) return;
    this.error.set(''); this.search = ''; this.preview = null;
    this.draft.set(d.settings ? structuredClone(d.settings) : { termId: term.id, from: term.from, to: term.to, weekdays: [...d.suggestedWeekdays], teacherIds: [], exclusions: [], closures: [], regenerateFrom: d.today > term.from && d.today <= term.to ? d.today : term.from, regenerateTo: term.to, acceptAttendanceCalendarMismatch: false });
    if (d.settings) { this.draft.update(x => ({ ...x!, regenerateFrom: d.today > x!.from && d.today <= x!.to ? d.today : x!.from, regenerateTo: x!.to })); }
    this.setupOpen = true;
  }
  setDate(field: 'from'|'to'|'regenerateFrom'|'regenerateTo', value: Date) { if (value) this.draft.update(x => ({ ...x!, [field]: this.iso(value) })); }
  toggleTeacher(id: string) { this.draft.update(x => ({ ...x!, teacherIds: x!.teacherIds.includes(id) ? x!.teacherIds.filter(t => t !== id) : [...x!.teacherIds,id], exclusions: x!.teacherIds.includes(id) ? x!.exclusions.filter(e => e.userId !== id) : x!.exclusions })); }
  toggleDay(day: number) { this.draft.update(x => ({ ...x!, weekdays: x!.weekdays.includes(day) ? x!.weekdays.filter(d => d !== day) : [...x!.weekdays,day].sort() })); }
  reorder(index: number, delta: number) { const ids = [...this.draft()!.teacherIds]; if (index+delta < 0 || index+delta >= ids.length) return; [ids[index],ids[index+delta]] = [ids[index+delta],ids[index]]; this.draft.update(x => ({ ...x!, teacherIds: ids })); }
  addExclusion() { if (!this.exclusionTeacher || !this.exclusionFrom || !this.exclusionTo || !this.exclusionReason.trim()) { this.error.set(this.t('FIELDS_REQUIRED')); return; } this.draft.update(x => ({ ...x!, exclusions: [...x!.exclusions, { userId: this.exclusionTeacher!, from: this.iso(this.exclusionFrom!), to: this.iso(this.exclusionTo!), reason: this.exclusionReason.trim() }] })); this.exclusionReason = ''; this.error.set(''); }
  addClosure() { if (!this.closureFrom || !this.closureTo || !this.closureReason.trim()) { this.error.set(this.t('FIELDS_REQUIRED')); return; } this.draft.update(x => ({ ...x!, closures: [...x!.closures, { from: this.iso(this.closureFrom!), to: this.iso(this.closureTo!), reason: this.closureReason.trim() }] })); this.closureReason = ''; this.error.set(''); }
  restore(index: number) { this.draft.update(x => ({ ...x!, exclusions: x!.exclusions.filter((_,i) => i !== index) })); }
  removeClosure(index: number) { this.draft.update(x => ({ ...x!, closures: x!.closures.filter((_,i) => i !== index) })); }
  teacherName(id: string) { return this.data()?.teachers.find(x => x.userId === id)?.name ?? id; }
  previewPlan() {
    this.busy.set(true); this.error.set(''); this.api.preview(this.draft()!, this.data()!.planRevision).subscribe({ next: r => {
      this.busy.set(false); if (!r.data || !r.isSuccess) { this.error.set(r.message || this.t('ERROR')); return; }
      this.preview = r.data; this.publishRequest = { settings: structuredClone(this.draft()!), expectedRevision: this.data()!.planRevision, previewDigest: r.data.digest, requestId: crypto.randomUUID() }; this.previewOpen = true;
    }, error: e => this.fail(e) });
  }
  publish() {
    if (!this.publishRequest) return; this.busy.set(true); this.error.set('');
    this.api.publish(this.publishRequest).subscribe({ next: r => { this.busy.set(false); if (!r.isSuccess) { this.error.set(r.message || this.t('ERROR')); return; } this.previewOpen = false; this.setupOpen = false; this.success.set(this.t('PUBLISHED')); this.load(); }, error: e => this.fail(e) });
  }
  openManual(day: DutyDay, mode = 'Exchange', debt?: DutyHistory) {
    this.closeDetails(); this.selected = day; this.manualOpen = true; this.mode = mode; this.reason = ''; this.substitute = mode === 'Unprotect' ? day.teacherId : null;
    if (debt) { this.resolveId = debt.id; this.substitute = debt.beforeTeacherId; }
    else this.resolveId = null;
    this.otherId = null; this.acceptException = false; this.manualPreview = null; this.manualRequest = null; this.error.set('');
  }
  resolve(debt: DutyHistory) {
    if (!this.data()?.teachers.some(x => x.userId === debt.beforeTeacherId && x.isEligible !== false)) { this.error.set(this.t('COMPENSATION_INELIGIBLE')); return; }
    const day = this.data()?.days.find(x => x.date >= this.data()!.today && !x.locked); if (day) this.openManual(day, 'ResolveCompensation', debt); else this.error.set(this.t('NO_FUTURE_DATE'));
  }
  selectCompensationDate(id: number) { this.selected = this.data()?.days.find(x => x.id === id) ?? this.selected; this.invalidateManual(); }
  invalidateManual() { this.manualPreview = null; this.manualRequest = null; }
  reviewManual() {
    if (!this.selected || !this.substitute || !this.reason.trim()) { this.error.set(this.t('FIELDS_REQUIRED')); return; }
    this.manualRequest = { assignmentId: this.selected.id, expectedRevision: this.data()!.planRevision, teacherId: this.substitute, otherAssignmentId: this.mode === 'Exchange' ? this.otherId : null,
      reason: this.reason.trim(), mode: this.mode, acceptException: true, resolveChangeId: this.resolveId, requestId: crypto.randomUUID() };
    this.busy.set(true); this.error.set(''); this.api.changePreview(this.manualRequest).subscribe({ next: r => { this.busy.set(false); if (r.isSuccess && r.data) { this.manualPreview = r.data; setTimeout(() => document.getElementById(r.data!.warnings.length ? 'duty-exception-accept' : 'duty-confirm-change')?.focus()); } else this.error.set(r.message || this.t('ERROR')); }, error: e => this.fail(e) });
  }
  confirmManual() {
    if (!this.manualRequest || !this.manualPreview || this.manualPreview.warnings.length && !this.acceptException) return;
    this.busy.set(true); this.api.change(this.manualRequest).subscribe({ next: r => { this.busy.set(false); if (!r.isSuccess) { this.error.set(r.message || this.t('ERROR')); return; } this.manualOpen = false; this.success.set(this.t('CHANGED')); this.load(); }, error: e => this.fail(e) });
  }
  retry() { this.busy.set(true); this.api.retry().subscribe({ next: () => { this.busy.set(false); this.success.set(this.t('RETRY_QUEUED')); this.load(); }, error: e => this.fail(e) }); }
  refreshForReview() { this.refreshingReview = true; this.load(true); }
  private fail(e: unknown) { this.busy.set(false); this.error.set(extractHttpErrorMessage(e) || this.t('ERROR')); }
}
