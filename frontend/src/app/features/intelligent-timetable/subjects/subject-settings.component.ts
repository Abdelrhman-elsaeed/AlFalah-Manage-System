import { CommonModule } from '@angular/common';
import { Component, HostListener, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { MultiSelectModule } from 'primeng/multiselect';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { ColorPickerModule } from 'primeng/colorpicker';
import { TableModule } from 'primeng/table';
import { finalize } from 'rxjs';
import { SubjectService } from '../../../core/services/subject.service';
import { TimetableSettingsService } from '../../../core/services/timetable-settings.service';
import { Subject, SubjectBulkResult, SubjectOverview, SubjectRequirement, SubjectRules } from '../../../core/models/subject.models';
import { TimetableSettingsOverview } from '../../../core/models/timetable-settings.models';
import { effectivePeriods } from '../../../core/models/bell-schedule.models';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';

@Component({ selector: 'app-subject-settings', standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ButtonModule, DialogModule, DropdownModule, MultiSelectModule, InputNumberModule, InputTextModule, ColorPickerModule, TableModule],
  templateUrl: './subject-settings.component.html', styleUrl: './subject-settings.component.css' })
export class SubjectSettingsComponent implements OnInit {
  private readonly api = inject(SubjectService); private readonly settings = inject(TimetableSettingsService); private readonly route = inject(ActivatedRoute);
  context: TimetableSettingsOverview | null = null; data: SubjectOverview | null = null;
  setupId = 0; yearId = 0; semester = 1; loading = false; saving = false; error = ''; message = '';
  search = ''; filter = ''; stage: number | null = null; grade: number | null = null;
  subjectId = 0; selectedClasses: number[] = []; selectedSubjects: Subject[] = []; rules = this.emptyRules();
  editorOpen = false; catalogOpen = false; roomsOpen = false; overwriteOpen = false; roomName = '';
  subjectDraft: Subject = { id: 0, name: '', color: '#2563eb', revision: 0 };
  results: SubjectBulkResult | null = null; editingId: number | null = null; private baseline = ''; private version = 0;
  readonly preferences = [{ label: 'بدون تفضيل', value: 'None' }, { label: 'تفضيل مبكر', value: 'Early' }, { label: 'تفضيل متأخر', value: 'Late' }];
  readonly dayNames = ['السبت', 'الأحد', 'الاثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة'];
  readonly stages = [{ label: 'الابتدائية', value: 1 }, { label: 'المتوسطة', value: 2 }, { label: 'الثانوية', value: 3 }];
  get total() { return (this.rules.individualPeriodCount || 0) + 2 * (this.rules.pairedBlockCount || 0); }
  get subjects() { return (this.data?.subjects ?? []).filter(s => s.name.includes(this.search) && (!this.filter ||
    (this.filter === 'configured' ? this.requirements(s.id).length > 0 : this.filter === 'empty' ? !this.requirements(s.id).length :
      this.requirements(s.id).some(r => this.filter === 'early' ? r.rules.timePreference === 'Early' : r.rules.pairedBlockCount > 0)))); }
  get classes() { return (this.data?.classrooms ?? []).filter(c => (!this.stage || c.stage === this.stage) && (!this.grade || c.gradeLevel === this.grade)); }
  get grades() { return [...new Set((this.data?.classrooms ?? []).map(c => c.gradeLevel))].sort((a,b) => a-b).map(value => ({ label: `${value}`, value })); }
  get days() { return (this.data?.schedule?.days ?? []).filter(d => d.isStudyDay).map(d => ({ label: this.dayNames[d.day - 1], value: d.day })); }
  get preferredRooms() { return (this.data?.rooms ?? []).filter(r => this.rules.roomIds.includes(r.id)); }
  get overlapping() { return this.requirements(this.subjectId).filter(r => this.selectedClasses.includes(r.classroomId)); }
  get earlyCount() { return this.data?.requirements.filter(r => r.rules.timePreference === 'Early').length ?? 0; }
  get singleCount() { return this.data?.requirements.filter(r => r.rules.individualPeriodCount > 0).length ?? 0; }
  get pairedCount() { return this.data?.requirements.filter(r => r.rules.pairedBlockCount > 0).length ?? 0; }
  get valid() { return this.subjectId > 0 && this.selectedClasses.length > 0 && this.total > 0 &&
    Number.isInteger(this.rules.individualPeriodCount) && this.rules.individualPeriodCount >= 0 && this.rules.individualPeriodCount <= 100 &&
    Number.isInteger(this.rules.pairedBlockCount) && this.rules.pairedBlockCount >= 0 && this.rules.pairedBlockCount <= 50 &&
    (this.rules.timePreference === 'None' || ((this.rules.earliestPeriodSequence ?? 0) > 0 && (this.rules.latestPreferredPeriodSequence ?? 0) >= this.rules.earliestPeriodSequence!)); }
  ngOnInit() { this.yearId = Number(this.route.snapshot.queryParamMap.get('academicYearId')) || 0;
    this.semester = Number(this.route.snapshot.queryParamMap.get('semester')) || 1;
    this.setupId = Number(this.route.snapshot.queryParamMap.get('profileId')) || 0; this.loadContext(); }
  private emptyRules(): SubjectRules { return { individualPeriodCount: 1, pairedBlockCount: 0, timePreference: 'None', earliestPeriodSequence: 1,
    latestPreferredPeriodSequence: 1, allowedDays: [], fixedSlots: [], roomIds: [], preferredRoomId: null }; }
  private state() { return JSON.stringify({ subjectId: this.subjectId, classes: this.selectedClasses, rules: this.rules }); }
  hasUnsavedChanges() { return this.saving || this.editorOpen && this.state() !== this.baseline || this.catalogOpen && !!this.subjectDraft.name.trim() || this.roomsOpen && !!this.roomName.trim(); }
  @HostListener('window:beforeunload', ['$event']) beforeUnload(e: BeforeUnloadEvent) { if (this.hasUnsavedChanges()) { e.preventDefault(); e.returnValue = ''; } }
  loadContext() { const version = ++this.version; this.loading = true; this.data = null; this.error = '';
    this.settings.getOverview(this.yearId || undefined, this.semester, this.setupId || undefined).subscribe({ next: r => {
      if (version !== this.version) return; this.context = r.data ?? null; this.yearId = r.data?.selectedAcademicYearId ?? 0;
      this.setupId = r.data?.selectedProfile?.id ?? 0;
      if (this.setupId) this.load(); else { this.loading = false; this.error = r.isSuccess ? '' : r.message || 'تعذر تحميل الإعدادات.'; }
    }, error: e => { if (version === this.version) { this.loading = false; this.fail(e); } } }); }
  changeContext(year: number, semester: number) { this.yearId = year; this.semester = semester; this.setupId = 0; this.loadContext(); }
  chooseSetup(id: number) { this.setupId = id; this.selectedSubjects = []; this.load(); }
  load() { const version = ++this.version; this.loading = true;
    this.api.get(this.setupId).subscribe({ next: r => { if (version !== this.version) return; this.loading = false;
      if (r.isSuccess && r.data) this.data = r.data; else this.error = r.errors?.join('، ') || r.message || 'تعذر تحميل المواد.';
    }, error: e => { if (version === this.version) { this.loading = false; this.fail(e); } } }); }
  requirements(subjectId: number) { return this.data?.requirements.filter(r => r.subjectId === subjectId) ?? []; }
  openRules(subjectId = 0, requirement?: SubjectRequirement) { this.subjectId = subjectId; this.editingId = requirement?.id ?? null;
    this.selectedClasses = requirement ? [requirement.classroomId] : []; this.rules = requirement ? structuredClone(requirement.rules) : this.emptyRules();
    this.stage = null; this.grade = null; this.error = ''; this.editorOpen = true; this.baseline = this.state(); }
  closeEditor() { if (this.saving || this.state() !== this.baseline && !window.confirm('تجاهل التغييرات غير المحفوظة؟')) return; this.editorOpen = false; this.overwriteOpen = false; }
  selectAll() { this.selectedClasses = this.classes.map(c => c.id); }
  periods(day: number) { return this.data?.schedule ? effectivePeriods(this.data.schedule, day).map(p => ({ label: `الحصة ${p.sequence}`, value: p.sequence })) : []; }
  addFixed() { const day = this.rules.allowedDays[0] ?? this.days[0]?.value; const period = this.periods(day)[0]?.value;
    if (day && period) this.rules.fixedSlots.push({ day, period }); }
  prepareSave() { if (!this.valid || this.saving) return;
    if (!this.editingId && this.overlapping.length) this.overwriteOpen = true; else this.save(!!this.editingId); }
  save(overwrite: boolean) { if (!this.valid || this.saving) return; this.saving = true; this.error = ''; this.overwriteOpen = false;
    const current = this.data!.requirements.find(r => r.id === this.editingId);
    const call = current ? this.api.update(this.setupId, current.id, current.revision, this.rules) : this.api.allocate(this.setupId, {
      subjectId: this.subjectId, classes: this.selectedClasses.map(classroomId => ({ classroomId,
        revision: this.requirements(this.subjectId).find(r => r.classroomId === classroomId)?.revision ?? 0 })), rules: this.rules, overwriteExisting: overwrite });
    call.pipe(finalize(() => this.saving = false)).subscribe({ next: r => {
      if (r.isSuccess && r.data) { this.results = r.data; const skipped = r.data.results.filter(x => x.status === 'Skipped');
        if (skipped.length) this.error = skipped.map(x => `${x.classroomName}: ${x.message}`).join(' — ');
        else this.editorOpen = false;
        this.message = `تم إنشاء ${r.data.results.filter(x => x.status === 'Created').length}، تحديث ${r.data.results.filter(x => x.status === 'Updated').length}، تخطي ${skipped.length}.`;
        this.load();
      } else this.error = r.errors?.join('، ') || r.message || 'تعذر الحفظ.';
    }, error: e => this.fail(e) }); }
  openCatalog(subject?: Subject) { this.subjectDraft = subject ? { ...subject } : { id: 0, name: '', color: '#2563eb', revision: 0 }; this.error = ''; this.catalogOpen = true; }
  saveCatalog() { if (!this.subjectDraft.name.trim() || this.saving) return; this.saving = true; this.error = '';
    const subject = { ...this.subjectDraft, color: '#' + this.subjectDraft.color.replace(/^#/, '') };
    this.api.saveSubject(this.setupId, subject).pipe(finalize(() => this.saving = false)).subscribe({ next: r => {
      if (r.isSuccess) { this.catalogOpen = false; this.message = 'تم حفظ المادة في دليل المدرسة.'; this.load(); }
      else this.error = r.errors?.join('، ') || r.message || 'تعذر الحفظ.';
    }, error: e => this.fail(e) }); }
  saveRoom() { if (!this.roomName.trim() || this.saving) return; this.saving = true; this.error = '';
    this.api.createRoom(this.setupId, this.roomName).pipe(finalize(() => this.saving = false)).subscribe({ next: r => {
      if (r.isSuccess) { this.roomName = ''; this.roomsOpen = false; this.message = 'تمت إضافة الغرفة.'; this.load(); }
      else this.error = r.errors?.join('، ') || r.message || 'تعذر الحفظ.';
    }, error: e => this.fail(e) }); }
  remove(r: SubjectRequirement) { if (this.saving || !window.confirm(`إزالة إعداد المادة من ${r.classroomName} فقط؟ ستبقى المادة في دليل المدرسة.`)) return;
    this.saving = true; this.error = ''; this.api.remove(this.setupId, r.id, r.revision).pipe(finalize(() => this.saving = false)).subscribe({ next: response => {
      if (response.isSuccess) { this.message = 'تمت إزالة إعداد الفصل.'; this.load(); }
      else this.error = response.errors?.join('، ') || response.message || 'تعذر الإزالة.';
    }, error: e => this.fail(e) }); }
  private fail(e: unknown) { this.error = extractHttpErrorMessage(e) ?? 'تعذر إتمام الطلب. احتفظنا بالتغييرات؛ أعد المحاولة.'; }
}
