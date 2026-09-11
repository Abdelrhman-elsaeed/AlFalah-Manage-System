import { CommonModule } from '@angular/common';
import { Component, HostListener, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { finalize } from 'rxjs';
import { TeachingAssignmentService } from '../../../core/services/teaching-assignment.service';
import { TimetableSettingsService } from '../../../core/services/timetable-settings.service';
import { TeachingCell, TeachingMember, TeachingMode, TeachingOverview, TeachingTeacher } from '../../../core/models/teaching-assignment.models';
import { SubjectRequirement } from '../../../core/models/subject.models';
import { TimetableSettingsOverview } from '../../../core/models/timetable-settings.models';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';

@Component({ selector: 'app-teaching-assignments', standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ButtonModule, DialogModule, DropdownModule, InputNumberModule, InputTextModule, TableModule],
  templateUrl: './teaching-assignments.component.html', styleUrl: './teaching-assignments.component.css' })
export class TeachingAssignmentsComponent implements OnInit {
  private readonly api = inject(TeachingAssignmentService); private readonly settings = inject(TimetableSettingsService); private readonly route = inject(ActivatedRoute);
  context: TimetableSettingsOverview | null = null; data: TeachingOverview | null = null;
  setupId = 0; yearId = 0; semester = 1; loading = false; saving = false; error = ''; message = '';
  drafts = new Map<number, TeachingCell>(); search = ''; classroomId: number | null = null; subjectId: number | null = null;
  stage: number | null = null; grade: number | null = null; status: string | null = null; teacherView = false;
  dialogOpen = false; editing: SubjectRequirement | null = null; mode: TeachingMode | null = 'SingleTeacher'; members: TeachingMember[] = [];
  teacherRows: TeachingTeacher[] = []; teacherTotal = 0; teacherLoading = false; teacherSearch = ''; specialization = '';
  activeFilter: boolean | null = true; visitingFilter: boolean | null = null; capacityOnly = false; first = 0; pageSize = 10;
  private searchVersion = 0; private dialogBaseline = ''; private sort = 'name'; private descending = false;
  readonly modes = [{ label: 'معلم واحد', value: 'SingleTeacher' }, { label: 'تدريس مشترك / معلم مساعد', value: 'CoTeaching' }, { label: 'تقسيم الحصص', value: 'SplitQuota' }];
  readonly stages = [{ label: 'الابتدائية', value: 1 }, { label: 'المتوسطة', value: 2 }, { label: 'الثانوية', value: 3 }];
  readonly statuses = [{ label: 'بانتظار الإسناد', value: 'pending' }, { label: 'مكتمل', value: 'assigned' }];
  readonly activeOptions = [{ label: 'نشط', value: true }, { label: 'غير نشط', value: false }];
  readonly visitingOptions = [{ label: 'زائر', value: true }, { label: 'مقيم', value: false }];
  get grades() { return [...new Set(this.data?.classrooms.map(c => c.gradeLevel) ?? [])].sort((a,b) => a-b).map(value => ({ label: `${value}`, value })); }
  get columns() { return this.data?.subjects.filter(s => !this.subjectId || s.id === this.subjectId) ?? []; }
  get rows() { return this.data?.classrooms.filter(c => (!this.classroomId || c.id === this.classroomId) && (!this.stage || c.stage === this.stage) &&
    (!this.grade || c.gradeLevel === this.grade) && this.columns.some(s => this.visibleRequirement(c.id, s.id))) ?? []; }
  get pending() { return this.data?.requirements.filter(r => !this.cell(r.id).members.length).length ?? 0; }
  get orphanedAssignments() { return this.data?.assignments.filter(a => !this.data!.requirements.some(r => r.id === a.classSubjectRequirementId)) ?? []; }
  get allCells() { return [...(this.data?.requirements.map(r => this.cell(r.id)) ?? []), ...this.orphanedAssignments.map(a => this.cell(a.classSubjectRequirementId))]; }
  get invalidDraft() { return [...this.drafts.values()].some(c => c.members.length > 0 && this.validation(c, this.data!.requirements.find(r => r.id === c.classSubjectRequirementId)!)) ||
    this.data?.teachers.some(t => this.workload(t.id).periods > t.maximumWeeklyPeriods) || false; }
  get dialogError() { if (!this.editing) return ''; return this.validation({ classSubjectRequirementId: this.editing.id, mode: this.mode!, members: this.members }, this.editing) ||
    this.members.map(m => this.teacher(m.teacherTimetableProfileId)).filter((t): t is TeachingTeacher => !!t)
      .map(t => this.workload(t.id, true).periods > t.maximumWeeklyPeriods ? `${t.name}: تجاوز الحد الأسبوعي ${t.maximumWeeklyPeriods}.` : !t.isActive ? `${t.name}: المعلم غير نشط.` : '').find(Boolean) || ''; }
  get remaining() { return (this.editing?.totalWeeklyPeriods ?? 0) - this.members.reduce((sum,m) => sum + (m.allocatedPeriodCount || 0), 0); }
  get remainingPairs() { return (this.editing?.rules.pairedBlockCount ?? 0) - this.members.reduce((sum,m) => sum + (m.allocatedPairedBlockCount || 0), 0); }
  ngOnInit() {
    this.yearId = Number(this.route.snapshot.queryParamMap.get('academicYearId')) || 0;
    this.semester = Number(this.route.snapshot.queryParamMap.get('semester')) || 1;
    this.setupId = Number(this.route.snapshot.queryParamMap.get('profileId')) || 0; this.loadContext();
  }
  loadContext() { this.loading = true; this.error = ''; this.data = null;
    this.settings.getOverview(this.yearId || undefined, this.semester, this.setupId || undefined).subscribe({ next: r => {
      this.context = r.data ?? null; this.yearId = r.data?.selectedAcademicYearId ?? 0; this.setupId = r.data?.selectedProfile?.id ?? 0;
      if (this.setupId) this.load(); else { this.loading = false; if (!r.isSuccess) this.error = r.message || 'تعذر تحميل الإعدادات.'; }
    }, error: e => { this.loading = false; this.fail(e); } }); }
  changeContext(year: number, semester: number) { if (!this.allowDiscard()) return; this.yearId = year; this.semester = semester; this.setupId = 0; this.drafts.clear(); this.loadContext(); }
  chooseSetup(id: number) { if (!this.allowDiscard()) return; this.setupId = id; this.drafts.clear(); this.load(); }
  load() { this.loading = true; this.error = ''; this.api.get(this.setupId).pipe(finalize(() => this.loading = false)).subscribe({ next: r => {
    if (r.isSuccess && r.data) { this.data = r.data; this.drafts.clear(); } else this.error = r.errors?.join('، ') || r.message || 'تعذر تحميل الإسنادات.';
  }, error: e => this.fail(e) }); }
  cell(id: number): TeachingCell { const a = this.drafts.get(id) ?? this.data?.assignments.find(a => a.classSubjectRequirementId === id);
    return a ?? { classSubjectRequirementId: id, mode: 'SingleTeacher', members: [] }; }
  requirement(classroom: number, subject: number) { return this.data?.requirements.find(r => r.classroomId === classroom && r.subjectId === subject); }
  visibleRequirement(classroom: number, subject: number) { const r = this.requirement(classroom, subject); if (!r) return false;
    const cell = this.cell(r.id); return (!this.status || (this.status === 'pending' ? !cell.members.length : !!cell.members.length)) &&
      (!this.search || this.data?.subjects.find(s => s.id === subject)?.name.includes(this.search) || cell.members.some(m => this.teacher(m.teacherTimetableProfileId)?.name.includes(this.search))); }
  teacher(id: number) { return this.data?.teachers.find(t => t.id === id); }
  modeLabel(mode: string) { return this.modes.find(m => m.value === mode)?.label ?? mode; }
  workload(id: number, preview = false) {
    const cells = this.allCells.map(c => preview && c.classSubjectRequirementId === this.editing?.id ? { ...c, members: this.members } : c);
    const assigned = cells.filter(c => c.members.some(m => m.teacherTimetableProfileId === id));
    const requirements = assigned.map(c => this.data!.requirements.find(r => r.id === c.classSubjectRequirementId)).filter((r): r is SubjectRequirement => !!r);
    return { periods: assigned.reduce((sum,c) => sum + (c.members.find(m => m.teacherTimetableProfileId === id)?.allocatedPeriodCount || 0), 0),
      subjects: new Set(requirements.map(r => r.subjectId)).size, classrooms: new Set(requirements.map(r => r.classroomId)).size };
  }
  resetFilters() { this.search = ''; this.classroomId = this.subjectId = this.stage = this.grade = null; this.status = null; }
  open(r: SubjectRequirement) { if (this.saving) return; this.editing = r; const cell = this.cell(r.id); this.mode = cell.mode;
    this.members = structuredClone(cell.members); this.dialogBaseline = this.dialogState(); this.dialogOpen = true;
    this.teacherSearch = ''; this.specialization = ''; this.first = 0; this.searchTeachers(); }
  selected(id: number) { return this.members.some(m => m.teacherTimetableProfileId === id); }
  toggle(t: TeachingTeacher) { if (this.selected(t.id)) { this.removeMember(t.id); return; }
    else if (t.isActive) this.members = [...this.members, { teacherTimetableProfileId: t.id, allocatedPeriodCount: this.editing!.totalWeeklyPeriods, allocatedPairedBlockCount: this.editing!.rules.pairedBlockCount }];
    if (this.members.length > 1 && this.mode === 'SingleTeacher') this.mode = null;
    if (this.members.length === 1) { this.mode = 'SingleTeacher'; this.modeChanged(); }
  }
  removeMember(id: number) { this.members = this.members.filter(m => m.teacherTimetableProfileId !== id);
    if (this.members.length <= 1) { this.mode = 'SingleTeacher'; this.modeChanged(); } }
  modeChanged() { if (this.mode !== 'SplitQuota') this.members = this.members.map(m => ({ ...m, allocatedPeriodCount: this.editing!.totalWeeklyPeriods,
    allocatedPairedBlockCount: this.editing!.rules.pairedBlockCount })); }
  confirmCell() { if (this.dialogError || !this.editing) return; this.drafts.set(this.editing.id,
    { classSubjectRequirementId: this.editing.id, mode: this.mode!, members: structuredClone(this.members) }); this.dialogOpen = false; }
  clear(r: SubjectRequirement) { this.clearId(r.id); }
  clearId(id: number) { this.drafts.set(id, { classSubjectRequirementId: id, mode: 'SingleTeacher', members: [] }); }
  closeDialog() { if (this.dialogState() !== this.dialogBaseline && !window.confirm('تجاهل تعديل هذه الخلية؟')) return; this.dialogOpen = false; }
  private dialogState() { return JSON.stringify({ mode: this.mode, members: this.members }); }
  hasUnsavedChanges() { return this.saving || this.drafts.size > 0 || this.dialogOpen && this.dialogState() !== this.dialogBaseline; }
  @HostListener('window:beforeunload', ['$event']) beforeUnload(e: BeforeUnloadEvent) { if (this.hasUnsavedChanges()) { e.preventDefault(); e.returnValue = ''; } }
  private allowDiscard() { return !this.saving && (!this.hasUnsavedChanges() || window.confirm('تجاهل التغييرات غير المحفوظة؟')); }
  discard() { if (this.allowDiscard()) { this.drafts.clear(); this.dialogOpen = false; this.load(); } }
  save() { if (this.saving || !this.drafts.size || this.invalidDraft) return; this.saving = true; this.error = ''; this.message = '';
    this.api.save(this.setupId, this.data!.revision, [...this.drafts.values()]).pipe(finalize(() => this.saving = false)).subscribe({ next: r => {
      if (r.isSuccess && r.data) { this.data = r.data; this.drafts.clear(); this.message = 'تم حفظ إسنادات المواد وتحديث أنصبة المعلمين.'; }
      else this.error = r.errors?.join('، ') || r.message || 'تعذر حفظ الإسنادات.';
    }, error: e => this.fail(e) }); }
  lazyTeachers(event: TableLazyLoadEvent) { this.first = event.first ?? 0; this.pageSize = event.rows ?? 10;
    this.sort = typeof event.sortField === 'string' ? event.sortField : 'name'; this.descending = event.sortOrder === -1; this.searchTeachers(false); }
  searchTeachers(reset = true) { if (!this.dialogOpen) return; if (reset) this.first = 0;
    const version = ++this.searchVersion; this.teacherLoading = true;
    const params: Record<string, string | number | boolean> = { search: this.teacherSearch, specialization: this.specialization,
      hasCapacity: this.capacityOnly, page: Math.floor(this.first / this.pageSize) + 1, pageSize: this.pageSize, sort: this.sort, descending: this.descending };
    if (this.activeFilter !== null) params['isActive'] = this.activeFilter; else params['isActive'] = '';
    if (this.visitingFilter !== null) params['isVisiting'] = this.visitingFilter;
    this.api.teachers(this.setupId, params).subscribe({ next: r => { if (version !== this.searchVersion) return; this.teacherLoading = false;
      if (r.isSuccess && r.data) { this.teacherRows = r.data.items; this.teacherTotal = r.data.totalCount; }
      else this.error = r.message || 'تعذر البحث عن المعلمين.';
    }, error: e => { if (version === this.searchVersion) { this.teacherLoading = false; this.fail(e); } } }); }
  specializationWarning(t: TeachingTeacher) { const subject = this.data?.subjects.find(s => s.id === this.editing?.subjectId)?.name;
    return !!t.specialization && !!subject && !t.specialization.includes(subject); }
  private validation(cell: TeachingCell, r: SubjectRequirement) {
    if (!cell.mode) return 'اختر التدريس المشترك أو تقسيم الحصص عند اختيار أكثر من معلم.';
    if (!cell.members.length) return '';
    if (cell.mode === 'SingleTeacher' && cell.members.length !== 1 || cell.mode !== 'SingleTeacher' && cell.members.length < 2) return 'عدد المعلمين لا يطابق نوع الإسناد.';
    if (cell.members.some(m => !Number.isInteger(m.allocatedPeriodCount) || !Number.isInteger(m.allocatedPairedBlockCount) ||
      m.allocatedPeriodCount < 0 || m.allocatedPeriodCount > r.totalWeeklyPeriods || m.allocatedPairedBlockCount < 0 ||
      2 * m.allocatedPairedBlockCount > m.allocatedPeriodCount)) return 'أدخل أعداداً صحيحة؛ كل حصة مزدوجة تحتاج حصتين لدى المعلم نفسه.';
    if (cell.mode === 'SplitQuota' && (cell.members.reduce((s,m) => s + m.allocatedPeriodCount, 0) !== r.totalWeeklyPeriods ||
      cell.members.reduce((s,m) => s + m.allocatedPairedBlockCount, 0) !== r.rules.pairedBlockCount)) return 'أكمل توزيع جميع الحصص والأزواج دون تجزئة أي زوج.';
    if (cell.mode !== 'SplitQuota' && cell.members.some(m => m.allocatedPeriodCount !== r.totalWeeklyPeriods || m.allocatedPairedBlockCount !== r.rules.pairedBlockCount)) return 'كل معلم في التدريس المشترك يحصل على النصاب الكامل.';
    return '';
  }
  private fail(e: unknown) { this.error = extractHttpErrorMessage(e) ?? 'تعذر إتمام الطلب؛ احتفظنا بتعديلاتك. أعد المحاولة أو تجاهل لإعادة تحميل أحدث نسخة.'; }
}
