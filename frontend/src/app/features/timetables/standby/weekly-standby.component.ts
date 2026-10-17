import { CommonModule } from '@angular/common';
import { Component, DestroyRef, ElementRef, EventEmitter, HostListener, Input, OnChanges, Output, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { DialogModule } from 'primeng/dialog';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import { StandbyCell, StandbyPlacement, StandbyTeacher, WeeklyStandbyRequest, WeeklyStandbyWorkbench } from '../../../core/models/weekly-standby.models';
import { WeeklyStandbyService } from '../../../core/services/weekly-standby.service';
import { TimetableService } from '../../../core/services/timetable.service';

@Component({
  selector: 'app-weekly-standby', standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule, DialogModule],
  templateUrl: './weekly-standby.component.html', styleUrls: ['./weekly-standby.component.css']
})
export class WeeklyStandbyComponent implements OnChanges {
  private readonly api = inject(WeeklyStandbyService);
  private readonly timetables = inject(TimetableService);
  private readonly destroy = inject(DestroyRef);
  private readonly translate = inject(TranslateService);
  private readonly host = inject(ElementRef);
  @Input({ required: true }) timetableId!: number;
  @Input() publicationAllowed = true;
  @Input() integratedHeader = false;
  @Output() saved = new EventEmitter<void>();
  @Output() dirtyChange = new EventEmitter<boolean>();
  @Output() busyChange = new EventEmitter<boolean>();
  view: WeeklyStandbyWorkbench | null = null;
  mode: 'Fair' | 'Manual' = 'Fair';
  day = 2;
  search = '';
  busy = false;
  dirty = false;
  error = '';
  notice = '';
  accepted: WeeklyStandbyRequest | null = null;
  reviewOpen = false;
  inspectedTeacher: number | null = null;
  inspectedPeriod: number | null = null;
  private detailsTrigger: HTMLElement | null = null;
  inspect(teacher: number | null, period: number | null, event: Event) {
    this.inspectedTeacher = teacher; this.inspectedPeriod = period;
    this.detailsTrigger = event.currentTarget as HTMLElement;
    requestAnimationFrame(() => { if (!this.disposed) this.host.nativeElement.querySelector('.inspector')?.focus(); });
  }
  @HostListener('keydown.escape')
  closeInspector() {
    if (this.inspectedTeacher === null && this.inspectedPeriod === null) return;
    this.inspectedTeacher = null; this.inspectedPeriod = null; this.detailsTrigger?.focus();
  }
  private originalPlacements: StandbyPlacement[] = [];
  get inspectorTeacher() { return this.view?.teachers.find(t => t.id === this.inspectedTeacher); }
  get inspectorRows() { return this.view?.teachers.map(t => ({ teacher: t, cell: this.cell(t, this.inspectedPeriod ?? 0) })).filter(x => x.cell) ?? []; }
  remainingCapacity(t: StandbyTeacher) { return Math.max(0, t.cells.filter(c => c.canStandby).length - this.teacherCount(t)); }
  private key(p: StandbyPlacement) { return `${p.instructorProfileId}:${p.day}:${p.period}`; }
  get added() { const old = new Set(this.originalPlacements.map(p => this.key(p))); return this.view?.placements.filter(p => !old.has(this.key(p))) ?? []; }
  get removed() { const current = new Set(this.view?.placements.map(p => this.key(p))); return this.originalPlacements.filter(p => !current.has(this.key(p))); }
  teacherName(id: number) { return this.view?.teachers.find(t => t.id === id)?.name ?? String(id); }
  private disposed = false;
  constructor() { this.destroy.onDestroy(() => { this.disposed = true; }); }
  private epoch = 0;
  private original = '';
  private previousState: { settings: WeeklyStandbyWorkbench['settings']; placements: StandbyPlacement[] }[] = [];
  get canUndo() { return this.previousState.length > 0; }
  get canPublishSaved() { return this.publicationAllowed && !!this.view?.ready && !this.view.isPublished && !this.dirty && !this.accepted; }
  private remember() { if (this.view) this.previousState.push(structuredClone({ settings: this.view.settings, placements: this.view.placements })); }
  undo() {
    if (!this.view || this.busy) return;
    const old = this.previousState.pop(); if (!old) return;
    this.view.settings = old.settings; this.view.placements = old.placements;
    this.refreshDemand();
    this.changed();
  }
  availableCount(day: number, period: number) { return this.view?.teachers.filter(t => !this.excluded(t) && t.cells.some(c => c.day === day && c.period === period && c.canStandby)).length ?? 0; }

  ngOnChanges() { void this.load(); }
  get days() { return [...new Set(this.view?.periods.map(p => p.day) ?? [])]; }
  get periods() { return this.view?.periods.filter(p => p.day === this.day) ?? []; }
  get teachers() { return this.view?.teachers.filter(t => t.name.toLocaleLowerCase().includes(this.search.trim().toLocaleLowerCase())) ?? []; }
  get totalRequired() { return this.view?.periods.reduce((sum, p) => sum + p.required, 0) ?? 0; }
  get totalAllocated() { return this.view?.placements.length ?? 0; }
  get shortages() { return this.view?.periods.reduce((sum, p) => sum + Math.max(0, p.required - this.count(p.day, p.period)), 0) ?? 0; }
  get lockedCount() { return this.view?.placements.filter(p => p.isLocked).length ?? 0; }
  get invalidPlacements() { return this.view?.placements.filter(p => !this.view?.teachers.some(t => t.id === p.instructorProfileId && !this.excluded(t) && t.cells.some(c => c.day === p.day && c.period === p.period && c.canStandby))) ?? []; }
  removeInvalid() {
    if (!this.view || this.busy) return;
    const invalid = this.invalidPlacements.filter(p => !this.view?.teachers.find(t => t.id === p.instructorProfileId)?.cells.find(c => c.day === p.day && c.period === p.period)?.isProtected);
    this.remember(); this.view.placements = this.view.placements.filter(p => !invalid.includes(p)); this.changed();
  }
  dayLabel(day: number) { return this.translate.instant(`STANDBY.DAY_${day}`); }
  cell(t: StandbyTeacher, period: number): StandbyCell | undefined { return t.cells.find(c => c.day === this.day && c.period === period); }
  placement(t: StandbyTeacher, period: number): StandbyPlacement | undefined {
    return this.view?.placements.find(p => p.instructorProfileId === t.id && p.day === this.day && p.period === period);
  }
  excluded(t: StandbyTeacher) { return this.view?.settings.excludedTeacherIds.includes(t.id) ?? false; }
  teacherCount(t: StandbyTeacher) { return this.view?.placements.filter(p => p.instructorProfileId === t.id).length ?? 0; }
  count(day: number, period: number) { return this.view?.placements.filter(p => p.day === day && p.period === period).length ?? 0; }
  private state(view = this.view) { return JSON.stringify({ settings: view?.settings, placements: view?.placements }); }
  private setBusy(value: boolean) { this.busy = value; this.busyChange.emit(value); }
  private changed() {
    this.accepted = null; this.reviewOpen = false; this.error = ''; this.notice = '';
    this.dirty = this.state() !== this.original;
    this.dirtyChange.emit(this.dirty);
  }
  async load() {
    if (this.busy) return;
    if (this.dirty && !window.confirm(this.translate.instant('STANDBY.DISCARD'))) return;
    const epoch = ++this.epoch;
    this.setBusy(true); this.error = ''; this.notice = '';
    try {
      const response = await firstValueFrom(this.api.get(this.timetableId).pipe(takeUntilDestroyed(this.destroy)));
      if (epoch !== this.epoch || this.disposed) return;
      if (!response.data) throw new Error(this.translate.instant('STANDBY.LOAD_FAILED'));
      this.adopt(response.data);
    } catch (e) { if (!this.disposed) this.error = extractHttpErrorMessage(e) ?? this.translate.instant('STANDBY.LOAD_FAILED'); }
    finally { if (!this.disposed && epoch === this.epoch) this.setBusy(false); }
  }
  private adopt(view: WeeklyStandbyWorkbench) {
    this.view = structuredClone(view);
    this.previousState = [];
    if (!this.days.includes(this.day)) this.day = this.days[0] ?? 2;
    this.original = this.state(); this.dirty = false; this.accepted = null; this.reviewOpen = false; this.dirtyChange.emit(false);
    this.originalPlacements = structuredClone(view.placements);
  }
  setMode(mode: 'Fair' | 'Manual') { if (this.busy || mode === this.mode) return; this.mode = mode; this.accepted = null; this.reviewOpen = false; this.notice = ''; }
  setDefault(value: number) {
    if (!this.view || this.busy) return;
    this.remember();
    this.view.settings.requiredPerPeriod = this.validDemand(value);
    this.refreshDemand();
    this.changed();
  }
  private refreshDemand() {
    if (!this.view) return;
    for (const p of this.view.periods) p.required = this.view.settings.overrides.find(o => o.day === p.day && o.period === p.period)?.required ??
      (this.view.teachers.some(t => t.cells.some(c => c.day === p.day && c.period === p.period && c.kind === 'Lesson')) ? this.view.settings.requiredPerPeriod : 0);
  }
  setDemand(period: number, value: number) {
    if (!this.view || this.busy) return;
    this.remember();
    const required = this.validDemand(value);
    this.view.settings.overrides = this.view.settings.overrides.filter(p => p.day !== this.day || p.period !== period);
    this.view.settings.overrides.push({ day: this.day, period, required });
    const slot = this.periods.find(p => p.period === period); if (slot) slot.required = required;
    this.changed();
  }
  private validDemand(value: number) { return Math.min(this.view?.teachers.length ?? 0, Math.max(0, Math.trunc(Number(value) || 0))); }
  toggleTeacher(t: StandbyTeacher) {
    if (!this.view || this.busy) return;
    if (!this.excluded(t) && this.view.placements.some(p => p.instructorProfileId === t.id && p.isLocked)) {
      this.error = this.translate.instant('STANDBY.UNLOCK_FIRST'); return;
    }
    const ids = new Set(this.view.settings.excludedTeacherIds);
    this.remember();
    if (ids.has(t.id)) ids.delete(t.id);
    else { ids.add(t.id); this.view.placements = this.view.placements.filter(p => p.instructorProfileId !== t.id); }
    this.view.settings.excludedTeacherIds = [...ids]; this.changed();
  }
  toggleCell(t: StandbyTeacher, period: number) {
    if (!this.view || this.busy || !this.view.ready || this.mode !== 'Manual') return;
    const cell = this.cell(t, period); const selected = this.placement(t, period);
    if (cell?.isProtected) return;
    if (!selected && (!cell?.canStandby || this.excluded(t))) return;
    this.remember();
    if (selected) this.view.placements = this.view.placements.filter(p => p !== selected);
    else if (cell?.canStandby && !this.excluded(t)) this.view.placements.push({ instructorProfileId: t.id, day: this.day, period, isLocked: true, source: 'Manual' });
    else return;
    this.changed();
  }
  toggleLock(t: StandbyTeacher, period: number) {
    const p = this.placement(t, period);
    if (this.busy || !p || this.cell(t, period)?.isProtected) return;
    this.remember();
    p.isLocked = !p.isLocked; this.changed();
  }
  async preview() {
    if (!this.view?.ready || this.busy) return;
    const body: WeeklyStandbyRequest = structuredClone({ revision: this.view.revision, mode: this.mode,
      settings: this.view.settings, placements: this.view.placements });
    this.setBusy(true); this.error = ''; this.notice = ''; this.accepted = null; this.reviewOpen = false;
    try {
      const result = await firstValueFrom(this.api.preview(this.timetableId, body).pipe(takeUntilDestroyed(this.destroy)));
      if (this.disposed) return;
      if (!result.data) throw new Error(this.translate.instant('STANDBY.PREVIEW_FAILED'));
      if (this.state(result.data.workbench) !== this.state()) this.remember();
      this.view = structuredClone(result.data.workbench);
      this.dirty = this.state() !== this.original; this.dirtyChange.emit(this.dirty);
      this.accepted = { ...body, proposalId: result.data.proposalId };
      this.reviewOpen = true;
    } catch (e) { if (!this.disposed) this.error = extractHttpErrorMessage(e) ?? this.translate.instant('STANDBY.PREVIEW_FAILED'); }
    finally { if (!this.disposed) this.setBusy(false); }
  }
  async reviewForSave() {
    if (this.busy || !this.view?.ready) return;
    if (this.accepted) this.reviewOpen = true;
    else await this.preview();
  }
  async save(andPublish = false) {
    if (!this.accepted || this.busy || !this.view?.ready || (andPublish && !this.publicationAllowed)) return;
    this.setBusy(true); this.error = '';
    let applied = false;
    try {
      const result = await firstValueFrom(this.api.apply(this.timetableId, this.accepted).pipe(takeUntilDestroyed(this.destroy)));
      if (this.disposed) return;
      if (!result.isSuccess || !result.data) throw new Error(result.message || this.translate.instant('STANDBY.SAVE_FAILED'));
      this.adopt(result.data); applied = true;
      if (andPublish) await this.publishCurrent();
      else this.notice = this.translate.instant(this.publicationAllowed ? 'STANDBY.SAVED' : 'STANDBY.CLONE_HINT');
    } catch (e) { if (!this.disposed) {
      this.accepted = null; this.reviewOpen = false;
      this.error = (applied ? this.translate.instant('STANDBY.SAVED_NOT_PUBLISHED') + ' ' : '') +
        (extractHttpErrorMessage(e) ?? this.translate.instant(applied ? 'STANDBY.PUBLISH_FAILED' : 'STANDBY.SAVE_FAILED'));
    } }
    finally { if (!this.disposed) { this.setBusy(false); if (applied) this.saved.emit(); } }
  }
  async publishSaved() {
    if (this.busy || !this.canPublishSaved) return;
    this.setBusy(true); this.error = ''; this.notice = '';
    try { await this.publishCurrent(); if (!this.disposed) this.saved.emit(); }
    catch (e) { if (!this.disposed) this.error = this.translate.instant('STANDBY.SAVED_NOT_PUBLISHED') + ' ' +
      (extractHttpErrorMessage(e) ?? this.translate.instant('STANDBY.PUBLISH_FAILED')); }
    finally { if (!this.disposed) this.setBusy(false); }
  }
  private async publishCurrent() {
    const view = this.view!;
    const result = await firstValueFrom(this.timetables.publish(view.timetableId, view.revision).pipe(takeUntilDestroyed(this.destroy)));
    if (this.disposed) return;
    if (!result.isSuccess || !result.data?.isPublished || result.data.id !== view.timetableId)
      throw new Error(result.message || this.translate.instant('STANDBY.PUBLISH_FAILED'));
    view.revision = result.data.revision; view.isPublished = true;
    this.notice = this.translate.instant('STANDBY.PUBLISHED');
  }
}
