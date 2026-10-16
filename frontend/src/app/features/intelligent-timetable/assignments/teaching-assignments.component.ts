import { CommonModule } from '@angular/common';
import { Component, DestroyRef, HostListener, OnInit, ViewChild, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DialogModule } from 'primeng/dialog';
import { TableLazyLoadEvent } from 'primeng/table';
import { finalize } from 'rxjs';
import { TeachingAssignmentService } from '../../../core/services/teaching-assignment.service';
import { TimetableSettingsService } from '../../../core/services/timetable-settings.service';
import { TeachingCell, TeachingMember, TeachingMode, TeachingOverview, TeachingTeacher } from '../../../core/models/teaching-assignment.models';
import { SubjectRequirement } from '../../../core/models/subject.models';
import { TimetableSettingsOverview } from '../../../core/models/timetable-settings.models';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import { AssignmentStatementComponent } from './assignment-statement.component';
import { buildLedger, emptyLoad, gradeKey, gradeLabel, sameCell, slotKey } from './assignment-ledger';

@Component({ selector: 'app-teaching-assignments', standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, DialogModule, AssignmentStatementComponent],
  templateUrl: './teaching-assignments.component.html', styleUrl: './teaching-assignments.component.css' })
export class TeachingAssignmentsComponent implements OnInit {
  private readonly api = inject(TeachingAssignmentService); private readonly settings = inject(TimetableSettingsService); private readonly route = inject(ActivatedRoute);
  private readonly destroy = inject(DestroyRef);
  context: TimetableSettingsOverview | null = null; data: TeachingOverview | null = null;
  setupId = 0; yearId = 0; semester = 1; loading = false; saving = false; error = ''; message = '';
  drafts = new Map<number, TeachingCell>(); search = ''; classroomId: number | null = null; subjectId: number | null = null;
  stage: number | null = null; grade: number | null = null; status: string | null = null; teacherView = false;
  dialogOpen = false; editing: SubjectRequirement | null = null; mode: TeachingMode | null = 'SingleTeacher'; members: TeachingMember[] = [];
  teacherRows: TeachingTeacher[] = []; teacherTotal = 0; teacherLoading = false; teacherSearch = ''; specialization = '';
  activeFilter: boolean | null = true; visitingFilter: boolean | null = null; capacityOnly = false; first = 0; pageSize = 10;
  private searchVersion = 0; private dialogBaseline = ''; private sort = 'name'; private descending = false;
  private contextVersion = 0; private loadVersion = 0;
  statementOpen = false;
  @ViewChild(AssignmentStatementComponent) statementEditor?: AssignmentStatementComponent;
  openStatement() { if (this.loading || this.saving || this.hasUnsavedChanges()) return; this.statementOpen = true; }
  closeStatement() { this.statementOpen = false; setTimeout(() => document.getElementById('assignment-statement-trigger')?.focus({preventScroll:true})); }
  statementSaved(value: TeachingOverview) { this.data = value; }
  refreshStatement() { this.statementOpen = false; this.reload(); }
  private pickerTrigger: HTMLElement | null = null;
  teacherError = ''; conflict = false; denied = false; gradeScope = '';
  matrixPage = 1; matrixPageSize = 10; summaryPage = 1; summaryPageSize = 10; summarySearch = ''; summarySort = 'name';
  private cachedData: TeachingOverview | null = null; private cachedDrafts: Map<number, TeachingCell> | null = null;
  private cachedLedger: ReturnType<typeof buildLedger> | null = null;
  private summaryKey = ''; private cachedSummary: TeachingTeacher[] = [];
  private viewKey = ''; private cachedView: { rows: TeachingOverview['classrooms']; matches: Set<number> } = { rows: [], matches: new Set() };
  readonly gradeLabel = gradeLabel;
  readonly byId = (_: number, item: { id: number }) => item.id;
  readonly byKey = (_: number, item: { key: string }) => item.key;
  get ledger() {
    if (this.data !== this.cachedData || this.drafts !== this.cachedDrafts) {
      this.cachedData = this.data; this.cachedDrafts = this.drafts; this.viewKey = ''; this.summaryKey = '';
      this.cachedLedger = this.data ? buildLedger(this.data, this.drafts) : null;
    }
    return this.cachedLedger;
  }
  readonly modes = [{ label: 'معلم واحد', value: 'SingleTeacher' }, { label: 'تدريس مشترك / معلم مساعد', value: 'CoTeaching' }, { label: 'تقسيم الحصص', value: 'SplitQuota' }];
  readonly stages = [{ label: 'الابتدائية', value: 1 }, { label: 'المتوسطة', value: 2 }, { label: 'الثانوية', value: 3 }];
  readonly statuses = [{ label: 'بانتظار الإسناد', value: 'pending' }, { label: 'مكتمل', value: 'assigned' }];
  readonly activeOptions = [{ label: 'نشط', value: true }, { label: 'غير نشط', value: false }];
  readonly visitingOptions = [{ label: 'زائر', value: true }, { label: 'مقيم', value: false }];
  get grades() { return [...new Set(this.data?.classrooms.map(c => c.gradeLevel) ?? [])].sort((a,b) => a-b).map(value => ({ label: `${value}`, value })); }
  get columns() { return this.data?.subjects.filter(s => !this.subjectId || s.id === this.subjectId) ?? []; }
  get rows() {
    const ledger = this.ledger; if (!ledger || !this.data) return [];
    const key = JSON.stringify([this.search, this.classroomId, this.subjectId, this.stage, this.grade, this.status, this.gradeScope]);
    if (key !== this.viewKey) {
      this.viewKey = key; const matches = new Set<number>(), classes = new Set<number>(), query = this.search.trim().toLocaleLowerCase('ar');
      const subjects = new Map(this.data.subjects.map(s => [s.id, s.name]));
      for (const r of this.data.requirements) {
        const cell = this.cell(r.id);
        if (this.subjectId && r.subjectId !== this.subjectId || this.status && (this.status === 'pending' ? !!cell.members.length : !cell.members.length)) continue;
        if (query && ![r.classroomName, subjects.get(r.subjectId) ?? '', ...cell.members.map(m => this.teacher(m.teacherTimetableProfileId)?.name ?? '')]
          .some(text => text.toLocaleLowerCase('ar').includes(query))) continue;
        matches.add(r.id); classes.add(r.classroomId);
      }
      this.cachedView = { matches, rows: this.data.classrooms.filter(c => classes.has(c.id) && (!this.classroomId || c.id === this.classroomId) &&
        (!this.stage || c.stage === this.stage) && (!this.grade || c.gradeLevel === this.grade) && (!this.gradeScope || gradeKey(c) === this.gradeScope)) };
    }
    return this.cachedView.rows;
  }
  get gradeGroups() { return this.ledger?.groups ?? []; }
  get matrixPages() { return Math.max(1, Math.ceil(this.rows.length / this.matrixPageSize)); }
  get currentMatrixPage() { return Math.min(this.matrixPage, this.matrixPages); }
  get pageGroups() {
    const rows = this.rows.slice((this.currentMatrixPage - 1) * this.matrixPageSize, this.currentMatrixPage * this.matrixPageSize);
    return this.gradeGroups.map(g => ({ ...g, classrooms: rows.filter(c => gradeKey(c) === g.key) })).filter(g => g.classrooms.length);
  }
  get pending() { return this.ledger?.pending ?? 0; }
  get completion() { const total = this.data?.requirements.length ?? 0; return total ? Math.round((total - this.pending) * 100 / total) : 0; }
  get orphanedAssignments() { return this.ledger?.orphaned ?? []; }
  get allCells() { return [...(this.ledger?.cells.values() ?? [])]; }
  get summaryTeachers() {
    void this.ledger;
    const key = JSON.stringify([this.summarySearch, this.summarySort]);
    if (key === this.summaryKey) return this.cachedSummary;
    this.summaryKey = key;
    const q = this.summarySearch.trim().toLocaleLowerCase('ar');
    return this.cachedSummary = [...(this.data?.teachers ?? [])].filter(t => !q || `${t.name} ${t.specialization ?? ''}`.toLocaleLowerCase('ar').includes(q))
      .sort((a, b) => this.summarySort === 'remaining' ? (a.maximumWeeklyPeriods - this.workload(a.id).periods) - (b.maximumWeeklyPeriods - this.workload(b.id).periods) || a.id - b.id :
        this.summarySort === 'load' ? this.workload(b.id).periods - this.workload(a.id).periods || a.id - b.id : a.name.localeCompare(b.name, 'ar'));
  }
  get summaryPages() { return Math.max(1, Math.ceil(this.summaryTeachers.length / this.summaryPageSize)); }
  get currentSummaryPage() { return Math.min(this.summaryPage, this.summaryPages); }
  get summaryRows() { return this.summaryTeachers.slice((this.currentSummaryPage - 1) * this.summaryPageSize, this.currentSummaryPage * this.summaryPageSize); }
  get teacherPages() { return Math.max(1, Math.ceil(this.teacherTotal / this.pageSize)); }
  get teacherPage() { return Math.floor(this.first / this.pageSize) + 1; }
  get selectedSetupName() { return this.context?.profiles.find(p => p.id === this.setupId)?.name ?? ''; }
  get invalidDraft() { return [...this.drafts.values()].some(c => c.members.length > 0 && this.validation(c, this.data!.requirements.find(r => r.id === c.classSubjectRequirementId)!)) ||
    this.data?.teachers.some(t => this.workload(t.id).periods > t.maximumWeeklyPeriods) || false; }
  get dialogError() { if (!this.editing) return ''; return this.validation({ classSubjectRequirementId: this.editing.id, mode: this.mode!, members: this.members }, this.editing) ||
    (this.members.some(m => !this.teacher(m.teacherTimetableProfileId)) ? 'أزل المعلم غير المتاح قبل التأكيد.' : '') ||
    this.members.map(m => this.teacher(m.teacherTimetableProfileId)).filter((t): t is TeachingTeacher => !!t)
      .map(t => this.workload(t.id, true).periods > t.maximumWeeklyPeriods ? `${t.name}: تجاوز الحد الأسبوعي ${t.maximumWeeklyPeriods}.` : !t.isActive ? `${t.name}: المعلم غير نشط.` : '').find(Boolean) || ''; }
  get remaining() { return (this.editing?.totalWeeklyPeriods ?? 0) - this.members.reduce((sum,m) => sum + (m.allocatedPeriodCount || 0), 0); }
  get remainingPairs() { return (this.editing?.rules.pairedBlockCount ?? 0) - this.members.reduce((sum,m) => sum + (m.allocatedPairedBlockCount || 0), 0); }
  ngOnInit() {
    this.yearId = Number(this.route.snapshot.queryParamMap.get('academicYearId')) || 0;
    this.semester = Number(this.route.snapshot.queryParamMap.get('semester')) || 1;
    this.setupId = Number(this.route.snapshot.queryParamMap.get('profileId')) || 0; this.loadContext();
  }
  loadContext() { const version = ++this.contextVersion; ++this.loadVersion; this.loading = true; this.error = ''; this.denied = false; this.data = null;
    this.settings.getOverview(this.yearId || undefined, this.semester, this.setupId || undefined).pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: r => {
      if (version !== this.contextVersion) return;
      this.context = r.data ?? null; this.yearId = r.data?.selectedAcademicYearId ?? 0; this.setupId = r.data?.selectedProfile?.id ?? 0;
      if (r.isSuccess && this.setupId) this.load(); else { this.loading = false; if (!r.isSuccess) this.error = r.message || 'تعذر تحميل الإعدادات.'; }
    }, error: e => { if (version === this.contextVersion) { this.loading = false; this.fail(e); } } }); }
  changeContext(year: number, semester: number) { if (!this.allowDiscard()) return; this.yearId = year; this.semester = semester; this.setupId = 0; this.resetWorkspace(); this.loadContext(); }
  chooseSetup(id: number) { if (!this.allowDiscard()) return; this.setupId = id; this.resetWorkspace(); this.load(); }
  private resetWorkspace() { this.data = null; this.drafts = new Map(); this.dialogOpen = false; this.statementOpen = false; ++this.searchVersion; this.resetFilters(); this.summaryPage = 1; this.message = ''; this.conflict = false; }
  reload() { if (this.allowDiscard()) { this.dialogOpen = false; this.setupId ? this.load() : this.loadContext(); } }
  load() { const version = ++this.loadVersion; this.loading = true; this.error = ''; this.denied = false;
    this.api.get(this.setupId).pipe(takeUntilDestroyed(this.destroy), finalize(() => { if (version === this.loadVersion) this.loading = false; })).subscribe({ next: r => {
    if (version !== this.loadVersion) return;
    if (r.isSuccess && r.data) { this.data = r.data; this.drafts = new Map(); this.conflict = false; } else this.error = r.errors?.join('، ') || r.message || 'تعذر تحميل الإسنادات.';
  }, error: e => { if (version === this.loadVersion) this.fail(e); } }); }
  cell(id: number): TeachingCell { const a = this.ledger?.cells.get(id);
    return a ?? { classSubjectRequirementId: id, mode: 'SingleTeacher', members: [] }; }
  requirement(classroom: number, subject: number) { return this.ledger?.slots.get(slotKey(classroom, subject)); }
  visibleRequirement(classroom: number, subject: number) { const r = this.requirement(classroom, subject); void this.rows; return !!r && this.cachedView.matches.has(r.id); }
  teacher(id: number) { return this.ledger?.teachers.get(id) ?? this.teacherRows.find(t => t.id === id); }
  modeLabel(mode: string) { return this.modes.find(m => m.value === mode)?.label ?? mode; }
  workload(id: number, preview = false) {
    const ledger = this.ledger, load = ledger?.loads.get(id) ?? emptyLoad;
    if (!preview || !this.editing) return load;
    const old = this.cell(this.editing.id).members.find(m => m.teacherTimetableProfileId === id)?.allocatedPeriodCount || 0;
    const member = this.members.find(m => m.teacherTimetableProfileId === id);
    const requirements = (ledger?.entries.get(id) ?? []).filter(e => e.requirement.id !== this.editing!.id).map(e => e.requirement);
    if (member) requirements.push(this.editing);
    return { periods: load.periods - old + (member?.allocatedPeriodCount || 0),
      subjects: new Set(requirements.map(r => r.subjectId)).size, classrooms: new Set(requirements.map(r => r.classroomId)).size };
  }
  loadPercent(t: TeachingTeacher, preview = false) { return t.maximumWeeklyPeriods ? Math.min(100, this.workload(t.id, preview).periods / t.maximumWeeklyPeriods * 100) : this.workload(t.id, preview).periods ? 100 : 0; }
  teacherEntries(id: number) { return this.ledger?.entries.get(id) ?? []; }
  memberPeriods(cell: TeachingCell, id: number) { return cell.members.find(m => m.teacherTimetableProfileId === id)?.allocatedPeriodCount ?? 0; }
  get teacherSort() { return this.sort; }
  subjectName(id: number) { return this.data?.subjects.find(s => s.id === id)?.name ?? ''; }
  filtersChanged() { this.matrixPage = 1; }
  selectGrade(key: string) { this.gradeScope = key; this.stage = this.grade = this.classroomId = null; this.filtersChanged(); }
  resetFilters() { this.search = ''; this.classroomId = this.subjectId = this.stage = this.grade = null; this.gradeScope = ''; this.status = null; this.filtersChanged(); }
  open(r: SubjectRequirement, trigger?: EventTarget | null) { if (this.saving || this.loading) return;
    this.pickerTrigger = trigger instanceof HTMLElement ? trigger : document.activeElement as HTMLElement | null; this.editing = r; const cell = this.cell(r.id); this.mode = cell.mode;
    this.members = structuredClone(cell.members); this.dialogBaseline = this.dialogState(); this.dialogOpen = true;
    this.teacherSearch = ''; this.specialization = ''; this.first = 0; this.searchTeachers(); }
  selected(id: number) { return this.members.some(m => m.teacherTimetableProfileId === id); }
  toggle(t: TeachingTeacher) { if (this.saving || this.teacherLoading) return; if (this.selected(t.id)) { this.removeMember(t.id); return; }
    else if (t.isActive) this.members = [...this.members, { teacherTimetableProfileId: t.id, allocatedPeriodCount: this.editing!.totalWeeklyPeriods, allocatedPairedBlockCount: this.editing!.rules.pairedBlockCount,
      statementCategory: this.cell(this.editing!.id).members.find(m => m.teacherTimetableProfileId === t.id)?.statementCategory ??
        this.data?.assignments.find(a => a.classSubjectRequirementId === this.editing!.id)?.members.find(m => m.teacherTimetableProfileId === t.id)?.statementCategory ?? null }];
    if (this.members.length > 1 && this.mode === 'SingleTeacher') this.mode = null;
    if (this.members.length === 1) { this.mode = 'SingleTeacher'; this.modeChanged(); }
  }
  removeMember(id: number) { if (this.saving) return; this.members = this.members.filter(m => m.teacherTimetableProfileId !== id);
    if (this.members.length <= 1) { this.mode = 'SingleTeacher'; this.modeChanged(); } }
  modeChanged() { if (this.mode !== 'SplitQuota') this.members = this.members.map(m => ({ ...m, allocatedPeriodCount: this.editing!.totalWeeklyPeriods,
    allocatedPairedBlockCount: this.editing!.rules.pairedBlockCount })); }
  confirmCell() { if (this.saving || this.dialogError || !this.editing) return; this.commit(
    { classSubjectRequirementId: this.editing.id, mode: this.mode!, members: structuredClone(this.members) }); this.dialogOpen = false; ++this.searchVersion; }
  private commit(cell: TeachingCell) {
    const baseline = this.data?.assignments.find(a => a.classSubjectRequirementId === cell.classSubjectRequirementId) ?? { classSubjectRequirementId: cell.classSubjectRequirementId, mode: 'SingleTeacher' as const, members: [] };
    const next = new Map(this.drafts); if (sameCell(cell, baseline)) next.delete(cell.classSubjectRequirementId); else next.set(cell.classSubjectRequirementId, cell);
    this.drafts = next; this.message = '';
  }
  clear(r: SubjectRequirement) { this.clearId(r.id); }
  clearId(id: number) { if (!this.saving && !this.loading) this.commit({ classSubjectRequirementId: id, mode: 'SingleTeacher', members: [] }); }
  closeDialog() { if (this.saving || this.dialogState() !== this.dialogBaseline && !window.confirm('تجاهل تعديل هذه الخلية؟')) return; this.dialogOpen = false; ++this.searchVersion; }
  restorePickerFocus() {
    const target = this.pickerTrigger?.isConnected ? this.pickerTrigger : document.getElementById('ledger-search');
    this.pickerTrigger = null; target?.focus({preventScroll:true});
  }
  @HostListener('document:keydown.escape') escape() { if (this.dialogOpen) this.closeDialog(); }
  private dialogState() { return JSON.stringify({ mode: this.members.length ? this.mode : 'SingleTeacher',
    members: [...this.members].sort((a,b) => a.teacherTimetableProfileId-b.teacherTimetableProfileId)
      .map(m => [m.teacherTimetableProfileId,m.allocatedPeriodCount,m.allocatedPairedBlockCount]) }); }
  hasUnsavedChanges() { return this.saving || !!this.statementEditor?.hasUnsavedWork() || this.drafts.size > 0 || this.dialogOpen && this.dialogState() !== this.dialogBaseline; }
  @HostListener('window:beforeunload', ['$event']) beforeUnload(e: BeforeUnloadEvent) { if (this.hasUnsavedChanges()) { e.preventDefault(); e.returnValue = ''; } }
  private allowDiscard() { return !this.saving && (!this.hasUnsavedChanges() || window.confirm('تجاهل التغييرات غير المحفوظة؟')); }
  discard() { if (this.allowDiscard()) { this.drafts = new Map(); this.dialogOpen = false; this.error = ''; this.conflict = false; this.message = 'تم تجاهل التغييرات غير المحفوظة.'; } }
  save() { if (this.saving || this.loading || this.dialogOpen || !this.data || !this.drafts.size || this.invalidDraft) return; this.saving = true; this.error = ''; this.message = ''; this.conflict = false;
    this.api.save(this.setupId, this.data!.revision, [...this.drafts.values()]).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.saving = false)).subscribe({ next: r => {
      if (r.isSuccess && r.data) { this.data = r.data; this.drafts = new Map(); this.message = 'تم حفظ إسنادات المواد وتحديث أنصبة المعلمين.'; }
      else this.error = r.errors?.join('، ') || r.message || 'تعذر حفظ الإسنادات.';
    }, error: e => this.fail(e) }); }
  lazyTeachers(event: TableLazyLoadEvent) { this.first = event.first ?? 0; this.pageSize = event.rows ?? 10;
    this.sort = typeof event.sortField === 'string' ? event.sortField : 'name'; this.descending = event.sortOrder === -1; this.searchTeachers(false); }
  teacherPageChanged(page: number) { if (this.teacherLoading || page < 1 || page > this.teacherPages) return; this.first = (page - 1) * this.pageSize; this.searchTeachers(false); }
  sortTeachers(value: string) { this.sort = value; this.searchTeachers(); }
  reverseTeachers() { this.descending = !this.descending; this.searchTeachers(); }
  searchTeachers(reset = true) { if (!this.dialogOpen) return; if (reset) this.first = 0;
    const version = ++this.searchVersion; this.teacherLoading = true; this.teacherError = '';
    const params: Record<string, string | number | boolean> = { search: this.teacherSearch, specialization: this.specialization,
      hasCapacity: this.capacityOnly, page: Math.floor(this.first / this.pageSize) + 1, pageSize: this.pageSize, sort: this.sort, descending: this.descending };
    if (this.activeFilter !== null) params['isActive'] = this.activeFilter; else params['isActive'] = '';
    if (this.visitingFilter !== null) params['isVisiting'] = this.visitingFilter;
    this.api.teachers(this.setupId, params).pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: r => { if (version !== this.searchVersion) return; this.teacherLoading = false;
      if (r.isSuccess && r.data) { this.teacherRows = r.data.items; this.teacherTotal = r.data.totalCount; }
      else { this.teacherRows = []; this.teacherError = r.message || 'تعذر البحث عن المعلمين.'; }
    }, error: e => { if (version === this.searchVersion) { this.teacherLoading = false; this.teacherRows = []; this.teacherError = extractHttpErrorMessage(e) || 'تعذر تحميل المعلمين. أعد المحاولة؛ اختياراتك محفوظة.'; } } }); }
  specializationWarning(t: TeachingTeacher) { const subject = this.data?.subjects.find(s => s.id === this.editing?.subjectId)?.name;
    return !!t.specialization && !!subject && !t.specialization.includes(subject); }
  private validation(cell: TeachingCell, r?: SubjectRequirement) {
    if (!cell.mode) return 'اختر التدريس المشترك أو تقسيم الحصص عند اختيار أكثر من معلم.';
    if (!cell.members.length) return '';
    if (!r) return 'المادة أو الفصل لم يعد متاحًا؛ أزل الإسناد وأعد المراجعة.';
    if (cell.mode === 'SingleTeacher' && cell.members.length !== 1 || cell.mode !== 'SingleTeacher' && cell.members.length < 2) return 'عدد المعلمين لا يطابق نوع الإسناد.';
    if (cell.members.some(m => !Number.isInteger(m.allocatedPeriodCount) || !Number.isInteger(m.allocatedPairedBlockCount) ||
      m.allocatedPeriodCount < 0 || m.allocatedPeriodCount > r.totalWeeklyPeriods || m.allocatedPairedBlockCount < 0 ||
      2 * m.allocatedPairedBlockCount > m.allocatedPeriodCount)) return 'أدخل أعداداً صحيحة؛ كل حصة مزدوجة تحتاج حصتين لدى المعلم نفسه.';
    if (cell.mode === 'SplitQuota' && (cell.members.reduce((s,m) => s + m.allocatedPeriodCount, 0) !== r.totalWeeklyPeriods ||
      cell.members.reduce((s,m) => s + m.allocatedPairedBlockCount, 0) !== r.rules.pairedBlockCount)) return 'أكمل توزيع جميع الحصص والأزواج دون تجزئة أي زوج.';
    if (cell.mode !== 'SplitQuota' && cell.members.some(m => m.allocatedPeriodCount !== r.totalWeeklyPeriods || m.allocatedPairedBlockCount !== r.rules.pairedBlockCount)) return 'كل معلم في التدريس المشترك يحصل على النصاب الكامل.';
    return '';
  }
  private fail(e: unknown) { const status = (e as { status?: number })?.status; this.conflict = status === 409; this.denied = status === 403;
    this.error = this.conflict ? 'تغيّرت الإسنادات في جلسة أخرى. احتفظنا بمسودتك؛ أعد تحميل النسخة الأحدث بعد مراجعة تغييراتك.' : this.denied ? 'ليس لديك صلاحية إدارة إسنادات هذه المدرسة.' : extractHttpErrorMessage(e) ?? 'تعذر إتمام الطلب؛ احتفظنا بتعديلاتك. أعد المحاولة أو تجاهل لإعادة تحميل أحدث نسخة.'; }
}
