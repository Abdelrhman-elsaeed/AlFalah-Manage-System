import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, ReactiveFormsModule, ValidationErrors, Validators, NonNullableFormBuilder } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CalendarModule } from 'primeng/calendar';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TagModule } from 'primeng/tag';
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
import { StudentAffairsDashboardService } from '../../../core/services/student-affairs-dashboard.service';
import { ToastService } from '../../../core/services/toast.service';

type QuickAction = 'behavior' | 'academic' | 'delay' | 'recognition';
type QuickActionReceipt = BehaviorIncidentDto | AcademicConcernDto | SessionDelayDto | RecognitionDto;

function notMoreThanFiveMinutesInFuture(control: AbstractControl): ValidationErrors | null {
  const value = control.value as Date | null;
  return value && value.getTime() > Date.now() + 5 * 60_000 ? { futureTime: true } : null;
}

@Component({
  selector: 'app-teacher-top-priority',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    ButtonModule,
    CalendarModule,
    DialogModule,
    DropdownModule,
    InputNumberModule,
    InputTextModule,
    InputTextareaModule,
    ProgressSpinnerModule,
    TagModule
  ],
  templateUrl: './teacher-top-priority.component.html',
  styleUrl: './teacher-top-priority.component.css'
})
export class TeacherTopPriorityComponent implements OnInit, OnDestroy {
  private readonly api = inject(StudentAffairsDashboardService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  private loadSubscription?: Subscription;
  private boundaryTimer?: ReturnType<typeof setTimeout>;
  private lastContext: TeacherCurrentContextDto | null = null;

  readonly loading = signal(true);
  readonly denied = signal(false);
  readonly errorMessage = signal('');
  readonly topPriority = signal<TeacherTopPriorityDto | null>(null);
  readonly selectedStudent = signal<StudentSummaryDto | null>(null);
  readonly rosterSearch = signal('');
  readonly activeAction = signal<QuickAction | null>(null);
  readonly submitting = signal(false);
  readonly submissionErrors = signal<readonly string[]>([]);
  maxOccurredAt = new Date(Date.now() + 5 * 60_000);

  readonly context = computed(() => this.topPriority()?.context ?? null);
  readonly filteredRoster = computed(() => {
    const query = this.rosterSearch().trim().toLocaleLowerCase('ar');
    const roster = this.context()?.roster ?? [];
    return query
      ? roster.filter(student => `${student.displayName} ${student.studentNumber}`.toLocaleLowerCase('ar').includes(query))
      : roster;
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
    this.load();
    if (typeof window !== 'undefined') {
      fromEvent(window, 'focus').pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.load());
    }
  }

  ngOnDestroy(): void {
    this.loadSubscription?.unsubscribe();
    if (this.boundaryTimer) clearTimeout(this.boundaryTimer);
  }

  load(): void {
    this.loadSubscription?.unsubscribe();
    if (this.boundaryTimer) clearTimeout(this.boundaryTimer);
    this.loading.set(true);
    this.denied.set(false);
    this.errorMessage.set('');
    this.topPriority.set(null);

    this.loadSubscription = this.api.getTeacherTopPriority().subscribe({
      next: response => {
        this.loading.set(false);
        if (!response.isSuccess || !response.data) {
          this.errorMessage.set(response.errors[0] ?? response.message ?? 'تعذر تحميل الحصة الحالية.');
          return;
        }
        this.applyTopPriority(response.data);
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.denied.set(error.status === 403);
        this.errorMessage.set(error.status === 403
          ? 'لا تملك صلاحية عرض إجراءات المعلم السريعة في المدرسة النشطة.'
          : 'تعذر تحميل الحصة الحالية. حاول مرة أخرى.');
      }
    });
  }

  selectStudent(student: StudentSummaryDto): void {
    this.selectedStudent.set(student);
  }

  setRosterSearch(event: Event): void {
    this.rosterSearch.set((event.target as HTMLInputElement).value);
  }

  openAction(action: QuickAction): void {
    if (!this.selectedStudent() || !this.context()?.currentPeriod || !this.isActionAllowed(action)) return;
    this.submissionErrors.set([]);
    this.submitting.set(false);
    this.maxOccurredAt = new Date(Date.now() + 5 * 60_000);
    this.resetForm(action);
    this.activeAction.set(action);
  }

  closeDialog(): void {
    if (!this.submitting()) {
      this.activeAction.set(null);
      this.submissionErrors.set([]);
    }
  }

  submit(): void {
    const action = this.activeAction();
    const student = this.selectedStudent();
    const period = this.context()?.currentPeriod;
    if (!action || !student || !period || this.submitting()) return;

    const form = this.formFor(action);
    form.markAllAsTouched();
    if (form.invalid) return;

    let request$: Observable<ApiResponse<QuickActionReceipt>>;
    if (action === 'behavior') {
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
    } else if (action === 'academic') {
      const value = this.academicForm.getRawValue();
      const request: CreateAcademicConcernRequestDto = {
        studentId: student.id,
        schoolTimetableEntryId: period.timetableEntryId,
        category: value.category.trim(),
        description: value.description.trim(),
        occurredAt: this.toIso(value.occurredAt)
      };
      request$ = this.api.createAcademicConcern(request);
    } else if (action === 'delay') {
      const value = this.delayForm.getRawValue();
      const request: CreateSessionDelayRequestDto = {
        studentId: student.id,
        schoolTimetableEntryId: period.timetableEntryId,
        occurredAt: this.toIso(value.occurredAt),
        delayMinutes: value.delayMinutes,
        reason: this.trimOrNull(value.reason)
      };
      request$ = this.api.createSessionDelay(request);
    } else {
      const value = this.recognitionForm.getRawValue();
      const request: CreateRecognitionRequestDto = {
        studentId: student.id,
        recognitionType: value.recognitionType.trim(),
        title: value.title.trim(),
        description: value.description.trim(),
        recognizedAt: this.toIso(value.recognizedAt)
      };
      request$ = this.api.createRecognition(request);
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
        this.load();
      },
      error: (error: HttpErrorResponse) => {
        this.submitting.set(false);
        this.submissionErrors.set(this.httpErrors(error));
        if (error.status === 403) this.load();
      }
    });
  }

  isActionAllowed(action: QuickAction): boolean {
    const permission = this.actionPermission(action);
    const allowlist = this.context()?.permittedQuickActions ?? [];
    const aliases = [permission, action, permission.split('.')[0] ?? ''].map(value => value.toLocaleLowerCase('en'));
    return this.auth.hasPermission(permission)
      && allowlist.some(value => aliases.includes(value.toLocaleLowerCase('en')));
  }

  dialogTitle(): string {
    switch (this.activeAction()) {
      case 'behavior': return 'مخالفة سلوكية';
      case 'academic': return 'ملاحظة أكاديمية';
      case 'delay': return 'تأخر عن الحصة';
      case 'recognition': return 'إشادة وتميّز';
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

  initials(name: string): string {
    return name.split(/\s+/).filter(Boolean).slice(0, 2).map(part => part[0]).join('');
  }

  private applyTopPriority(value: TeacherTopPriorityDto): void {
    const previousContext = this.lastContext;
    this.topPriority.set(value);
    this.lastContext = value.context;
    const selected = this.selectedStudent();
    if (selected && !value.context.roster.some(student => student.id === selected.id)) {
      this.selectedStudent.set(null);
      if (this.activeAction()) this.toast.warn('تغيّر نطاق الحصة', 'أعد اختيار الطالب من قائمة الحصة الحالية. احتفظنا بمسودة النموذج.');
    } else if (previousContext?.currentPeriod?.timetableEntryId !== value.context.currentPeriod?.timetableEntryId && this.activeAction()) {
      this.selectedStudent.set(null);
      this.toast.warn('تغيّرت الحصة الحالية', 'أعد اختيار الطالب قبل إرسال المسودة.');
    }
    this.scheduleBoundaryRefresh(value.context);
  }

  private scheduleBoundaryRefresh(context: TeacherCurrentContextDto): void {
    if (!context.currentPeriod) return;
    const delay = new Date(context.currentPeriod.endsAt).getTime() - new Date(context.schoolLocalTime).getTime();
    if (Number.isFinite(delay) && delay > 0) {
      this.boundaryTimer = setTimeout(() => this.load(), Math.min(delay + 1_000, 2_147_000_000));
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
