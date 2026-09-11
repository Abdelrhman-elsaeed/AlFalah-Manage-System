import { CommonModule } from '@angular/common';
import { Component, HostListener, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { finalize } from 'rxjs';
import { TimetableSettingsService } from '../../../core/services/timetable-settings.service';
import { TeacherAvailabilityService } from '../../../core/services/teacher-availability.service';
import { TimetableSettingsOverview } from '../../../core/models/timetable-settings.models';
import { AvailabilityCell, AvailabilityTeacher, TeacherAvailability, UpdateTeacherProfile } from '../../../core/models/teacher-availability.models';
import { ClearableSelectComponent } from '../../../shared/components/clearable-select/clearable-select.component';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-teacher-settings', standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ButtonModule, CheckboxModule, InputTextModule, InputNumberModule, ClearableSelectComponent],
  templateUrl: './teacher-settings.component.html', styleUrl: './teacher-settings.component.css'
})
export class TeacherSettingsComponent implements OnInit {
  private readonly api = inject(TeacherAvailabilityService);
  private readonly settings = inject(TimetableSettingsService);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  overview: TimetableSettingsOverview | null = null;
  teachers: AvailabilityTeacher[] = [];
  draft: TeacherAvailability | null = null;
  setupId: number | null = null; teacherId: number | null = null;
  yearId = 0; semester = 1; loading = true; saving = false; error = '';
  confirmScheduleReview = false;
  private baseline = 'null'; private contextVersion = 0;
  readonly days = ['السبت', 'الأحد', 'الاثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة'].map((name, i) => ({ id: i + 1, name }));
  readonly semesters = [{ label: 'الفصل الدراسي الأول', value: 1 }, { label: 'الفصل الدراسي الثاني', value: 2 }];
  get columns() { return [...new Set(this.draft?.slots.map(s => s.sequence) ?? [])].sort((a, b) => a - b); }
  get availableCount() { return this.draft?.slots.filter(s => s.isAvailable).length ?? 0; }
  get remaining() { return (this.draft?.maximumWeeklyPeriods ?? 0) - (this.draft?.allocatedPeriods ?? 0); }
  get valid() {
    return !!this.draft && Number.isInteger(this.draft.maximumWeeklyPeriods) && this.draft.maximumWeeklyPeriods >= 0
      && this.draft.maximumWeeklyPeriods <= this.availableCount && this.remaining >= 0
      && this.draft.shortDisplayName.length <= 60 && (!this.draft.requiresScheduleReview || this.confirmScheduleReview);
  }
  ngOnInit() {
    this.yearId = Number(this.route.snapshot.queryParamMap.get('academicYearId')) || 0;
    this.semester = Number(this.route.snapshot.queryParamMap.get('semester')) || 1;
    this.setupId = Number(this.route.snapshot.queryParamMap.get('profileId')) || null;
    this.loadContext();
  }
  hasUnsavedChanges() { return this.saving || JSON.stringify(this.draft) !== this.baseline || this.confirmScheduleReview; }
  @HostListener('window:beforeunload', ['$event']) beforeUnload(event: BeforeUnloadEvent) {
    if (this.hasUnsavedChanges() || this.saving) { event.preventDefault(); event.returnValue = ''; }
  }
  private discardConfirmed() { return !this.saving && (!this.hasUnsavedChanges() || window.confirm('لديك تغييرات غير محفوظة. هل تريد تجاهلها؟')); }
  changeContext(year: number, semester: number) {
    if (!this.discardConfirmed()) return;
    this.yearId = year; this.semester = semester; this.setupId = null; this.loadContext();
  }
  loadContext() {
    const version = ++this.contextVersion;
    this.loading = true; this.error = ''; this.clearTeacher();
    this.settings.getOverview(this.yearId || undefined, this.semester, this.setupId ?? undefined).subscribe({ next: r => {
      if (version !== this.contextVersion) return;
      this.overview = r.data ?? null;
      if (!r.data) { this.loading = false; this.error = r.message || 'تعذر تحميل الإعدادات.'; return; }
      this.yearId = r.data.selectedAcademicYearId; this.setupId = r.data.selectedProfile?.id ?? null;
      this.loadTeachers();
    }, error: e => { if (version === this.contextVersion) { this.loading = false; this.error = extractHttpErrorMessage(e) ?? 'تعذر تحميل الإعدادات.'; } } });
  }
  chooseSetup(id: number) {
    if (id === this.setupId || !this.discardConfirmed()) return;
    ++this.contextVersion; this.setupId = id; this.clearTeacher(); this.loadTeachers();
  }
  private clearTeacher() { this.draft = null; this.baseline = 'null'; this.confirmScheduleReview = false; this.teacherId = null; this.teachers = []; }
  private loadTeachers() {
    if (!this.setupId) { this.loading = false; return; }
    const version = this.contextVersion;
    this.loading = true;
    this.api.list(this.setupId).subscribe({ next: r => {
      if (version !== this.contextVersion) return;
      this.teachers = r.data ?? []; this.loading = false;
      if (!r.isSuccess) this.error = r.errors?.join('، ') || r.message || 'تعذر تحميل المعلمين.';
      else if (this.teachers.length) this.chooseTeacher(this.teachers[0].id);
    }, error: e => { if (version === this.contextVersion) { this.loading = false; this.error = extractHttpErrorMessage(e) ?? 'تعذر تحميل المعلمين.'; } } });
  }
  chooseTeacher(id: number) {
    if (id === this.teacherId || !this.discardConfirmed() || !this.setupId) return;
    this.teacherId = id; this.fetchTeacher();
  }
  private fetchTeacher() {
    const version = ++this.contextVersion;
    this.loading = true; this.error = ''; this.draft = null; this.baseline = 'null'; this.confirmScheduleReview = false;
    this.api.get(this.setupId!, this.teacherId!).subscribe({ next: r => {
      if (version !== this.contextVersion) return;
      this.loading = false;
      if (r.data) this.apply(r.data); else this.error = r.errors?.join('، ') || r.message || 'تعذر تحميل إتاحة المعلم.';
    }, error: e => { if (version === this.contextVersion) { this.loading = false; this.error = extractHttpErrorMessage(e) ?? 'تعذر تحميل إتاحة المعلم.'; } } });
  }
  private apply(data: TeacherAvailability) {
    this.draft = structuredClone(data); this.baseline = JSON.stringify(this.draft); this.confirmScheduleReview = false;
  }
  cell(day: number, sequence: number) { return this.draft?.slots.find(s => s.day === day && s.sequence === sequence); }
  dayCells(day: number) { return this.draft?.slots.filter(s => s.day === day) ?? []; }
  commonTime(sequence: number) {
    const cells = this.draft?.slots.filter(s => s.sequence === sequence) ?? [];
    return cells.length && cells.every(s => s.startLocalTime === cells[0].startLocalTime && s.endLocalTime === cells[0].endLocalTime)
      ? `${cells[0].startLocalTime.slice(0, 5)} – ${cells[0].endLocalTime.slice(0, 5)}` : 'حسب اليوم';
  }
  cellLabel(cell: AvailabilityCell) {
    return `${this.draft?.teacherName}، ${this.days[cell.day - 1].name}، الحصة ${cell.sequence}، ${cell.startLocalTime.slice(0, 5)} إلى ${cell.endLocalTime.slice(0, 5)}، ${cell.isAvailable ? 'متاح' : 'غير متاح'}`;
  }
  bulk(available: boolean, day?: number) {
    if (this.saving || !this.draft) return;
    for (const cell of this.draft.slots) if (day === undefined || cell.day === day) cell.isAvailable = available;
  }
  reload() { if (this.discardConfirmed()) this.teacherId ? this.fetchTeacher() : this.loadContext(); }
  save() {
    if (!this.valid || !this.draft || this.saving) return;
    const d = this.draft;
    const request: UpdateTeacherProfile = { revision: d.revision, bellScheduleRevisionId: d.bellScheduleRevisionId,
      shortDisplayName: d.shortDisplayName, maximumWeeklyPeriods: d.maximumWeeklyPeriods, isVisiting: d.isVisiting,
      hideFromPrint: d.hideFromPrint, confirmScheduleReview: this.confirmScheduleReview,
      slots: d.slots.map(s => ({ day: s.day, bellPeriodId: s.bellPeriodId, isAvailable: s.isAvailable })) };
    this.saving = true; this.error = '';
    this.api.save(this.setupId!, this.teacherId!, request).pipe(finalize(() => this.saving = false)).subscribe({ next: r => {
      if (r.data) { this.apply(r.data); this.toast.success('تم حفظ إعدادات المعلم'); }
      else this.error = r.errors?.join('، ') || r.message || 'تعذر الحفظ.';
    }, error: e => this.error = extractHttpErrorMessage(e) ?? 'تعذر الحفظ. تغييراتك محفوظة في الشاشة؛ راجع التعارض ثم أعد المحاولة.' });
  }
}
