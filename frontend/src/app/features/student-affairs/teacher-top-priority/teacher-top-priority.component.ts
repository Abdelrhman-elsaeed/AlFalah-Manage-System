import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormsModule, ReactiveFormsModule, ValidationErrors, Validators, NonNullableFormBuilder } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CalendarModule } from 'primeng/calendar';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { Observable, Subscription, fromEvent } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ApiResponse } from '../../../core/models/api-response.model';
import {
  AcademicConcernDto,
  BehaviorIncidentDto,
  BehaviorSeverity,
  CreateAcademicConcernRequestDto,
  CreateBehaviorIncidentRequestDto,
  CreateRecognitionRequestDto,
  CreateSessionDelayRequestDto,
  RecognitionDto,
  SessionDelayDto,
  StudentSummaryDto,
  TeacherCurrentContextDto,
  TeacherTopPriorityDto
} from '../../../core/models/student-affairs-dashboard.models';
import { AuthService } from '../../../core/services/auth.service';
import { ClassroomDto } from '../../../core/models/daily-operations.models';
import { StudentAffairsDashboardService } from '../../../core/services/student-affairs-dashboard.service';
import { ToastService } from '../../../core/services/toast.service';
import {
  currentLessonReasonLabel,
  currentLessonStateLabel,
  teacherAlertLabel
} from '../../../core/utils/current-lesson-labels';

export type QuickAction = 'behavior' | 'academic' | 'delay' | 'recognition';
export type TeacherWorkspaceTab = 'gate-passes' | 'entry-permits' | 'classes';
export type QuickActionReceipt = BehaviorIncidentDto | AcademicConcernDto | SessionDelayDto | RecognitionDto;

function notMoreThanFiveMinutesInFuture(control: AbstractControl): ValidationErrors | null {
  const value = control.value as Date | null;
  return value && value.getTime() > Date.now() + 5 * 60_000 ? { futureTime: true } : null;
}

@Component({
  selector: 'app-teacher-top-priority',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterLink,
    ButtonModule,
    CalendarModule,
    DialogModule,
    DropdownModule,
    InputNumberModule,
    InputTextModule,
    InputTextareaModule,
    ProgressSpinnerModule,
    TableModule,
    TagModule,
    TooltipModule
  ],
  templateUrl: './teacher-top-priority.component.html',
  styleUrl: './teacher-top-priority.component.css'
})
export class TeacherTopPriorityComponent implements OnInit, OnDestroy {
  private readonly api = inject(StudentAffairsDashboardService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  private loadSubscription?: Subscription;
  private boundaryTimer?: ReturnType<typeof setTimeout>;
  private lastContext: TeacherCurrentContextDto | null = null;
  private dialogEntryId: number | null = null;

  readonly loading = signal(true);
  readonly refreshing = signal(false);
  readonly denied = signal(false);
  readonly errorMessage = signal('');
  readonly topPriority = signal<TeacherTopPriorityDto | null>(null);
  readonly activeTab = signal<TeacherWorkspaceTab>('gate-passes');
  readonly myClassrooms = signal<readonly ClassroomDto[]>([]);
  readonly classroomsLoading = signal(true);
  readonly classroomsError = signal('');
  readonly classroomRoster = signal<readonly StudentSummaryDto[]>([]);
  readonly classroomRosterLoading = signal(false);
  readonly detailClassroomId = signal<number | null>(null);
  readonly selectedStudent = signal<StudentSummaryDto | null>(null);
  readonly rosterSearch = signal('');
  readonly activeAction = signal<QuickAction | null>(null);
  readonly submitting = signal(false);
  readonly acknowledgingId = signal<string | null>(null);
  readonly submissionErrors = signal<readonly string[]>([]);
  maxOccurredAt = new Date(Date.now() + 5 * 60_000);

  readonly trackByStudentId = (_index: number, item: StudentSummaryDto): number => item.id;

  readonly context = computed(() => this.topPriority()?.context ?? null);

  readonly detailMode = computed(() => this.detailClassroomId() !== null);

  readonly detailClassroom = computed(() =>
    this.myClassrooms().find(classroom => classroom.id === this.detailClassroomId()) ?? null);

  readonly effectiveRoster = computed(() => this.detailMode()
    ? this.classroomRoster()
    : this.context()?.roster ?? []);

  readonly filteredRoster = computed(() => {
    const query = this.rosterSearch().trim().toLocaleLowerCase('ar');
    const roster = this.effectiveRoster();
    return query
      ? roster.filter(student => `${student.displayName} ${student.studentNumber}`.toLocaleLowerCase('ar').includes(query))
      : roster;
  });

  readonly activeClassroomLabel = computed(() => {
    if (this.detailMode()) {
      return this.detailClassroom()?.label ?? this.classroomRoster()[0]?.classLabel ?? 'الفصل المحدد';
    }
    return this.context()?.currentPeriod?.classroom.label ?? 'لا يوجد فصل نشط';
  });

  readonly behaviorForm = this.fb.group({
    category: ['', Validators.required],
    severity: ['Medium' as BehaviorSeverity, Validators.required],
    description: ['', [Validators.required, Validators.maxLength(2000)]],
    occurredAt: new FormControl<Date | null>(null, notMoreThanFiveMinutesInFuture),
    location: ['', Validators.maxLength(250)],
    immediateAction: ['', Validators.maxLength(1000)]
  });

  readonly academicForm = this.fb.group({
    category: ['', Validators.required],
    description: ['', [Validators.required, Validators.maxLength(2000)]],
    occurredAt: new FormControl<Date | null>(null, notMoreThanFiveMinutesInFuture)
  });

  readonly delayForm = this.fb.group({
    occurredAt: new FormControl<Date | null>(null, notMoreThanFiveMinutesInFuture),
    delayMinutes: new FormControl<number | null>(null, [Validators.min(0), Validators.pattern(/^\d+$/)]),
    reason: ['', Validators.maxLength(500)]
  });

  readonly recognitionForm = this.fb.group({
    recognitionType: ['', Validators.required],
    title: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', [Validators.required, Validators.maxLength(2000)]],
    recognizedAt: new FormControl<Date | null>(null, notMoreThanFiveMinutesInFuture)
  });

  readonly severityOptions: ReadonlyArray<{ label: string; value: BehaviorSeverity }> = [
    { label: 'منخفضة', value: 'Low' },
    { label: 'متوسطة', value: 'Medium' },
    { label: 'عالية', value: 'High' },
    { label: 'حرجة', value: 'Critical' }
  ];

  readonly behaviorCategories = [
    { label: 'تعطيل الحصة', value: 'ClassroomDisruption' },
    { label: 'عدم الالتزام بالتعليمات', value: 'InstructionNonCompliance' },
    { label: 'سلوك غير لائق', value: 'InappropriateConduct' },
    { label: 'أخرى', value: 'Other' }
  ];

  readonly academicCategories = [
    { label: 'ضعف المشاركة', value: 'LowParticipation' },
    { label: 'عدم إكمال المهام', value: 'IncompleteWork' },
    { label: 'تراجع المستوى', value: 'PerformanceDecline' },
    { label: 'أخرى', value: 'Other' }
  ];

  readonly recognitionTypes = [
    { label: 'تفوق أكاديمي', value: 'AcademicExcellence' },
    { label: 'سلوك إيجابي', value: 'PositiveConduct' },
    { label: 'مبادرة وتعاون', value: 'InitiativeAndCollaboration' },
    { label: 'تحسن ملحوظ', value: 'NotableImprovement' }
  ];

  ngOnInit(): void {
    const classroomId = Number(this.route.snapshot.queryParamMap.get('classroomId'));
    if (Number.isInteger(classroomId) && classroomId > 0) this.detailClassroomId.set(classroomId);
    this.load();
    this.loadMyClassrooms();
    if (this.detailClassroomId()) this.loadClassroomRoster(this.detailClassroomId()!);
    if (typeof window !== 'undefined') {
      fromEvent(window, 'focus').pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.load(true));
      fromEvent(document, 'visibilitychange').pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
        if (document.visibilityState === 'visible') this.load(true);
      });
    }
  }

  ngOnDestroy(): void {
    this.loadSubscription?.unsubscribe();
    if (this.boundaryTimer) clearTimeout(this.boundaryTimer);
  }

  load(silent = false): void {
    this.loadSubscription?.unsubscribe();
    if (this.boundaryTimer) clearTimeout(this.boundaryTimer);

    if (!this.topPriority() && !silent) {
      this.loading.set(true);
    } else {
      this.refreshing.set(true);
    }
    this.denied.set(false);
    this.errorMessage.set('');

    this.loadSubscription = this.api.getTeacherTopPriority().subscribe({
      next: response => {
        this.loading.set(false);
        this.refreshing.set(false);
        if (!response.isSuccess || !response.data) {
          this.errorMessage.set(response.errors[0] ?? response.message ?? 'تعذر تحميل الحصة الحالية.');
          return;
        }
        this.applyTopPriority(response.data);
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.refreshing.set(false);
        this.denied.set(error.status === 401 || error.status === 403);
        const message = error.status === 401
          ? 'انتهت جلسة الدخول. سجّل الدخول مرة أخرى لعرض سياق الحصة.'
          : error.status === 403
            ? 'لا تملك صلاحية عرض إجراءات المعلم السريعة في المدرسة النشطة.'
            : error.status === 404
              ? 'تعذر العثور على ملف المعلم في المدرسة النشطة.'
              : error.status === 409
                ? 'تغيّر سياق الحصة أثناء التحميل. أعد المحاولة للحصول على السياق الحالي.'
                : 'تعذر تحميل الحصة الحالية. حاول مرة أخرى.';
        this.errorMessage.set(message);
      }
    });
  }

  refreshWorkspace(): void {
    this.load();
    this.loadMyClassrooms();
    const classroomId = this.detailClassroomId();
    if (classroomId) this.loadClassroomRoster(classroomId);
  }

  selectStudent(student: StudentSummaryDto): void {
    this.selectedStudent.set(student);
  }

  setRosterSearch(event: Event): void {
    this.rosterSearch.set((event.target as HTMLInputElement).value);
  }

  selectTab(tab: TeacherWorkspaceTab): void {
    this.activeTab.set(tab);
  }

  openClassroom(classroom: ClassroomDto): void {
    const tree = this.router.createUrlTree(['/student-affairs/teacher'], {
      queryParams: { view: 'classroom', classroomId: classroom.id }
    });
    const url = this.router.serializeUrl(tree);
    window.open(url, '_blank', 'noopener');
  }

  openAction(action: QuickAction, student?: StudentSummaryDto): void {
    if (student) {
      this.selectStudent(student);
    }
    if (!this.selectedStudent() || !this.isActionAllowed(action)) return;
    this.submissionErrors.set([]);
    this.submitting.set(false);
    this.maxOccurredAt = new Date(Date.now() + 5 * 60_000);
    this.resetForm(action);
    this.dialogEntryId = this.context()?.currentPeriod?.timetableEntryId ?? null;
    this.activeAction.set(action);
  }

  closeDialog(): void {
    if (!this.submitting()) {
      this.activeAction.set(null);
      this.dialogEntryId = null;
      this.submissionErrors.set([]);
    }
  }

  submit(): void {
    const action = this.activeAction();
    const student = this.selectedStudent();
    const period = this.context()?.currentPeriod;
    if (!action || !student || this.submitting()) return;

    if (this.refreshing() || this.errorMessage()) {
      this.submissionErrors.set([
        'تعذر تأكيد سياق الحصة الحالي. احتفظنا بالمدخلات؛ أعد تحميل السياق ثم حاول مرة أخرى.'
      ]);
      return;
    }

    if (!period || this.dialogEntryId !== period.timetableEntryId) {
      this.submissionErrors.set([
        'تغيّر سياق الحصة منذ فتح النموذج. احتفظنا بالمدخلات؛ أغلق النموذج واختر الطالب من الحصة الحالية.'
      ]);
      return;
    }

    const form = this.formFor(action);
    form.markAllAsTouched();
    if (form.invalid) return;

    let request$: Observable<ApiResponse<QuickActionReceipt>>;
    if (action === 'behavior' && period) {
      const value = this.behaviorForm.getRawValue();
      const request: CreateBehaviorIncidentRequestDto = {
        studentId: student.id,
        schoolTimetableEntryId: period.timetableEntryId,
        category: value.category.trim(),
        severity: value.severity,
        description: value.description.trim(),
        occurredAt: this.toIso(value.occurredAt),
        location: this.trimOrNull(value.location),
        immediateAction: this.trimOrNull(value.immediateAction)
      };
      request$ = this.api.createBehaviorIncident(request);
    } else if (action === 'academic' && period) {
      const value = this.academicForm.getRawValue();
      const request: CreateAcademicConcernRequestDto = {
        studentId: student.id,
        schoolTimetableEntryId: period.timetableEntryId,
        category: value.category.trim(),
        description: value.description.trim(),
        occurredAt: this.toIso(value.occurredAt)
      };
      request$ = this.api.createAcademicConcern(request);
    } else if (action === 'delay' && period) {
      const value = this.delayForm.getRawValue();
      const request: CreateSessionDelayRequestDto = {
        studentId: student.id,
        schoolTimetableEntryId: period.timetableEntryId,
        occurredAt: this.toIso(value.occurredAt),
        delayMinutes: value.delayMinutes,
        reason: this.trimOrNull(value.reason)
      };
      request$ = this.api.createSessionDelay(request);
    } else if (action === 'recognition' && period) {
      const value = this.recognitionForm.getRawValue();
      const request: CreateRecognitionRequestDto = {
        studentId: student.id,
        recognitionType: value.recognitionType.trim(),
        title: value.title.trim(),
        description: value.description.trim(),
        recognizedAt: this.toIso(value.recognizedAt)
      };
      request$ = this.api.createRecognition(request);
    } else {
      return;
    }

    this.submitting.set(true);
    this.submissionErrors.set([]);
    request$.subscribe({
      next: (response: ApiResponse<QuickActionReceipt>) => {
        this.submitting.set(false);
        if (!response.isSuccess || !response.data) {
          this.submissionErrors.set(response.errors.length ? response.errors : [response.message || 'تعذر حفظ الإجراء.']);
          return;
        }
        const detail = this.receiptDetail(response.data);
        this.toast.success('تم حفظ الإجراء', detail);
        this.activeAction.set(null);
        this.dialogEntryId = null;
        this.load(true);
      },
      error: (error: HttpErrorResponse) => {
        this.submitting.set(false);
        this.submissionErrors.set(this.httpErrors(error));
        if (error.status === 403) this.load(true);
      }
    });
  }

  isActionAllowed(action: QuickAction): boolean {
    const permission = this.actionPermission(action);
    const allowlist = this.context()?.permittedQuickActions ?? [];
    const aliases = [permission, action, permission.split('.')[0] ?? ''].map(value => value.toLocaleLowerCase('en'));
    const selectedClassroomMatchesCurrent = !this.detailMode()
      || this.detailClassroomId() === this.context()?.currentPeriod?.classroom.id;
    return selectedClassroomMatchesCurrent && !this.refreshing() && !this.errorMessage()
      && this.auth.hasRole('Instructor') && this.auth.hasPermission(permission)
      && allowlist.some(value => aliases.includes(value.toLocaleLowerCase('en')));
  }

  dialogTitle(): string {
    switch (this.activeAction()) {
      case 'behavior': return 'تسجيل مخالفة سلوكية';
      case 'academic': return 'تسجيل ملاحظة أكاديمية';
      case 'delay': return 'تسجيل تأخر عن الحصة';
      case 'recognition': return 'منح إشادة وتميّز';
      default: return '';
    }
  }

  formatTime(value: string): string {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return value;
    try {
      return new Intl.DateTimeFormat('ar-SA', {
        hour: '2-digit', minute: '2-digit', timeZone: this.context()?.schoolTimeZone
      }).format(date);
    } catch {
      return new Intl.DateTimeFormat('ar-SA', { hour: '2-digit', minute: '2-digit' }).format(date);
    }
  }

  lessonStateLabel(context: TeacherCurrentContextDto): string {
    return currentLessonStateLabel(context.resolutionKind);
  }

  lessonReason(context: TeacherCurrentContextDto): string {
    return currentLessonReasonLabel(context.resolutionKind, context.resolutionReason);
  }

  alertLabel(alert: string, context: TeacherCurrentContextDto): string {
    return teacherAlertLabel(alert, context.resolutionKind, context.resolutionReason);
  }

  initials(name: string): string {
    return name.split(/\s+/).filter(Boolean).slice(0, 2).map(part => part[0]).join('');
  }

  private mergeRoster(current: readonly StudentSummaryDto[], incoming: readonly StudentSummaryDto[]): readonly StudentSummaryDto[] {
    if (!current || current.length === 0) return incoming;
    if (current.length !== incoming.length) return incoming;
    let changed = false;
    const merged: StudentSummaryDto[] = [];
    for (let i = 0; i < incoming.length; i++) {
      const prev = current[i];
      const next = incoming[i];
      if (
        !prev ||
        prev.id !== next.id ||
        prev.displayName !== next.displayName ||
        prev.studentNumber !== next.studentNumber ||
        prev.classLabel !== next.classLabel ||
        prev.photoUrl !== next.photoUrl
      ) {
        changed = true;
        merged.push(next);
      } else {
        merged.push(prev);
      }
    }
    return changed ? merged : current;
  }

  private loadMyClassrooms(): void {
    this.classroomsLoading.set(true);
    this.classroomsError.set('');
    this.api.getTeacherClassrooms().subscribe({
      next: response => {
        this.classroomsLoading.set(false);
        if (!response.isSuccess || !response.data) {
          this.classroomsError.set(response.errors[0] ?? response.message ?? 'تعذر تحميل قائمة الفصول المسندة إليك.');
          return;
        }
        this.myClassrooms.set(response.data);
      },
      error: () => {
        this.classroomsLoading.set(false);
        this.classroomsError.set('تعذر تحميل قائمة الفصول المسندة إليك. حاول التحديث مرة أخرى.');
      }
    });
  }

  private loadClassroomRoster(classroomId: number): void {
    this.classroomRosterLoading.set(true);
    this.api.getClassroomStudents(classroomId).subscribe({
      next: response => {
        this.classroomRosterLoading.set(false);
        if (!response.isSuccess || !response.data) {
          this.classroomsError.set(response.errors[0] ?? response.message ?? 'تعذر تحميل طلاب الفصل.');
          return;
        }
        this.classroomRoster.set(response.data);
      },
      error: () => {
        this.classroomRosterLoading.set(false);
        this.classroomsError.set('تعذر تحميل طلاب الفصل. حاول مرة أخرى.');
      }
    });
  }

  private applyTopPriority(value: TeacherTopPriorityDto): void {
    const previous = this.topPriority();
    const previousContext = this.lastContext;
    const mergedRoster = previous?.context.roster
      ? this.mergeRoster(previous.context.roster, value.context.roster)
      : value.context.roster;

    const mergedValue: TeacherTopPriorityDto = {
      ...value,
      context: {
        ...value.context,
        roster: mergedRoster
      }
    };

    this.topPriority.set(mergedValue);
    this.lastContext = mergedValue.context;
    const selected = this.selectedStudent();
    if (selected && !mergedValue.context.roster.some(student => student.id === selected.id)) {
      this.selectedStudent.set(null);
      if (this.activeAction()) this.submissionErrors.set(['لم يعد الطالب ضمن قائمة الحصة الحالية. احتفظنا بمسودة النموذج.']);
    } else if (previousContext?.currentPeriod?.timetableEntryId !== mergedValue.context.currentPeriod?.timetableEntryId && this.activeAction()) {
      this.submissionErrors.set(['تغيّرت الحصة الحالية. احتفظنا بمسودة النموذج، ولن تُرسل إلى الحصة القديمة.']);
    }
    this.scheduleBoundaryRefresh(mergedValue.context);
  }

  private scheduleBoundaryRefresh(context: TeacherCurrentContextDto): void {
    if (!context.currentPeriod) return;
    const delay = new Date(context.currentPeriod.endsAt).getTime() - new Date(context.schoolLocalTime).getTime();
    if (Number.isFinite(delay) && delay > 0) {
      this.boundaryTimer = setTimeout(() => this.load(true), Math.min(delay + 1_000, 2_147_000_000));
    }
  }

  private formFor(action: QuickAction) {
    switch (action) {
      case 'behavior': return this.behaviorForm;
      case 'academic': return this.academicForm;
      case 'delay': return this.delayForm;
      case 'recognition': return this.recognitionForm;
    }
  }

  private resetForm(action: QuickAction): void {
    if (action === 'behavior') this.behaviorForm.reset({ category: '', severity: 'Medium', description: '', occurredAt: null, location: '', immediateAction: '' });
    if (action === 'academic') this.academicForm.reset({ category: '', description: '', occurredAt: null });
    if (action === 'delay') this.delayForm.reset({ occurredAt: null, delayMinutes: null, reason: '' });
    if (action === 'recognition') this.recognitionForm.reset({ recognitionType: '', title: '', description: '', recognizedAt: null });
  }

  private actionPermission(action: QuickAction): string {
    switch (action) {
      case 'behavior': return 'Behavior.Create';
      case 'academic': return 'AcademicConcern.Create';
      case 'delay': return 'SessionDelay.Create';
      case 'recognition': return 'Recognition.Create';
    }
  }

  private receiptDetail(receipt: QuickActionReceipt): string {
    if ('dispatchDecision' in receipt) {
      const referral = receipt.referralId ? `، الإحالة رقم ${receipt.referralId}` : '';
      return `السجل رقم ${receipt.id} — بانتظار الاعتماد، قيمة المؤشر ${receipt.metric.eligibleTermCount}${referral}`;
    }
    if ('guardianNotification' in receipt && receipt.guardianNotification) {
      const metric = 'metric' in receipt ? `، قيمة المؤشر ${receipt.metric.eligibleTermCount}` : '';
      return `السجل رقم ${receipt.id} — حالة الإشعار: ${receipt.guardianNotification.status}${metric}`;
    }
    if ('metric' in receipt) return `السجل رقم ${receipt.id} — قيمة المؤشر ${receipt.metric.eligibleTermCount}.`;
    return `تم إنشاء السجل رقم ${receipt.id}.`;
  }

  acknowledgeGatePass(id: number, rowVersion: string): void {
    this.acknowledge(`gate-${id}`, this.api.acknowledgeGatePass(id, rowVersion));
  }

  acknowledgeEntryPermit(id: number, rowVersion: string): void {
    this.acknowledge(`entry-${id}`, this.api.acknowledgeEntryPermit(id, rowVersion));
  }

  private acknowledge(key: string, request$: Observable<ApiResponse<unknown>>): void {
    if (this.acknowledgingId()) return;
    this.acknowledgingId.set(key);
    request$.subscribe({
      next: response => {
        this.acknowledgingId.set(null);
        if (!response.isSuccess) {
          this.toast.error('تعذر الإقرار', response.errors[0] ?? response.message);
          return;
        }
        this.toast.success('تم الإقرار', 'تم تسجيل إقرار الاستلام بنجاح.');
        this.load(true);
      },
      error: (error: HttpErrorResponse) => {
        this.acknowledgingId.set(null);
        if (error.status === 409) {
          this.toast.warn('تغيّرت الحالة', 'تم تحديث السجل بواسطة مستخدم آخر. أعدنا تحميل أحدث حالة.');
          this.load(true);
          return;
        }
        this.toast.error('تعذر الإقرار', this.httpErrors(error)[0]);
      }
    });
  }

  private httpErrors(error: HttpErrorResponse): readonly string[] {
    const body = error.error as { errors?: unknown; message?: unknown } | null;
    if (Array.isArray(body?.errors)) {
      const errors = body.errors.filter((item): item is string => typeof item === 'string');
      if (errors.length) return errors;
    }
    if (typeof body?.message === 'string' && body.message) return [body.message];
    return [error.status === 403 ? 'انتهى نطاق الحصة أو لم تعد تملك صلاحية هذا الإجراء.' : 'تعذر حفظ الإجراء. راجع البيانات وحاول مرة أخرى.'];
  }

  private toIso(value: Date | null): string | null {
    return value ? value.toISOString() : null;
  }

  private trimOrNull(value: string): string | null {
    return value.trim() || null;
  }
}

// Alias for spec naming parity
export { TeacherTopPriorityComponent as TeacherWorkspaceComponent };
