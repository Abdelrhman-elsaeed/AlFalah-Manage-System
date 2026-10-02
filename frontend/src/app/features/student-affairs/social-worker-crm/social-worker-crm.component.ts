import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CalendarModule } from 'primeng/calendar';
import { DataViewModule } from 'primeng/dataview';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TagModule } from 'primeng/tag';
import { Observable, finalize, forkJoin, merge } from 'rxjs';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import {
  GuardianSummonStatus,
  REFERRAL_STATUSES,
  ReferralDto,
  StudentCaseActionDto,
  StudentCaseActionType,
  StudentGuardianLinkDto,
  StudentReferralStatus,
  SUMMON_STATUSES,
  SummonDto,
  SummonHistoryDto
} from '../../../core/models/phase5.models';
import { ApiResponse } from '../../../core/models/api-response.model';
import { AuthService } from '../../../core/services/auth.service';
import { Phase5Service } from '../../../core/services/phase5.service';
import { ToastService } from '../../../core/services/toast.service';
import { DashboardCountDto } from '../../../core/models/student-affairs-dashboard.models';

type ReferralActionMode = 'accept' | 'addAction' | 'resolve' | 'reopen';
type SummonActionMode = 'schedule' | 'attend' | 'observe' | 'improve';
type EngagementMode = 'summon' | 'message';

@Component({
  selector: 'app-social-worker-crm',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    ButtonModule,
    CalendarModule,
    DataViewModule,
    DialogModule,
    DropdownModule,
    InputTextModule,
    InputTextareaModule,
    ProgressSpinnerModule,
    TagModule
  ],
  templateUrl: './social-worker-crm.component.html',
  styleUrls: ['../manager-workspace.css', './social-worker-crm.component.css']
})
export class SocialWorkerCrmComponent implements OnInit {
  private readonly api = inject(Phase5Service);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  readonly section = signal<'cases' | 'summons'>('cases');
  readonly referrals = signal<readonly ReferralDto[]>([]);
  readonly summons = signal<readonly SummonDto[]>([]);
  readonly loading = signal(true);
  readonly errorMessage = signal('');
  readonly dashboardCounts = signal<readonly DashboardCountDto[]>([]);
  readonly page = signal(1);
  readonly pageSize = 20;
  readonly totalCount = signal(0);
  readonly search = new FormControl('', { nonNullable: true });
  readonly statusFilter = new FormControl<StudentReferralStatus | GuardianSummonStatus | null>(null);
  readonly priorityFilter = new FormControl<ReferralDto['priority'] | null>(null);
  readonly listMode = signal<'kanban' | 'list'>('kanban');
  readonly referralStatuses = REFERRAL_STATUSES;
  readonly summonStatuses = SUMMON_STATUSES;
  readonly priorityFilterOptions = [
    { label: 'كل الأولويات', value: null },
    { label: 'عادية', value: 'Normal' },
    { label: 'عالية', value: 'High' },
    { label: 'حرجة', value: 'Critical' }
  ];
  readonly referralStatusFilterOptions = [
    { label: 'كل الحالات', value: null },
    ...REFERRAL_STATUSES.map(value => ({ label: this.referralStatusLabel(value), value }))
  ];
  readonly summonStatusFilterOptions = [
    { label: 'كل الحالات', value: null },
    ...SUMMON_STATUSES.map(value => ({ label: this.rawSummonStatusLabel(value), value }))
  ];

  readonly referralDialogVisible = signal(false);
  readonly selectedReferral = signal<ReferralDto | null>(null);
  readonly referralActionMode = signal<ReferralActionMode>('accept');
  readonly referralSaving = signal(false);
  readonly referralReconciling = signal(false);
  readonly referralConflict = signal(false);
  readonly referralActionForm = new FormGroup({
    actionType: new FormControl<StudentCaseActionType>('CounselingSession', { nonNullable: true, validators: [Validators.required] }),
    description: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(4000)] }),
    actionAt: new FormControl<Date | null>(null),
    result: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2000)] }),
    narrative: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(4000)] })
  });
  readonly caseActionOptions: readonly { label: string; value: StudentCaseActionType }[] = [
    { label: 'جلسة إرشاد', value: 'CounselingSession' },
    { label: 'توثيق تواصل أو استدعاء تم داخل الحالة', value: 'GuardianSummon' },
    { label: 'توصية بحسم درجات', value: 'GradeDeductionRecommendation' },
    { label: 'توصية بإيقاف', value: 'SuspensionRecommendation' },
    { label: 'إحالة إلى لجنة حقوق الطفل', value: 'ChildRightsCommitteeReferral' },
    { label: 'إجراء آخر', value: 'Other' }
  ];
  readonly priorityOptions = [
    { label: 'عادية', value: 'Normal' },
    { label: 'عالية', value: 'High' },
    { label: 'حرجة', value: 'Critical' }
  ] as const;
  readonly engagementDialogVisible = signal(false);
  readonly engagementMode = signal<EngagementMode>('summon');
  readonly engagementSaving = signal(false);
  readonly engagementIdempotencyKey = signal<string | null>(null);
  readonly engagementForm = new FormGroup({
    guardianProfileId: new FormControl<number | null>(null, { validators: [Validators.required] }),
    priority: new FormControl<ReferralDto['priority']>('Normal', { nonNullable: true, validators: [Validators.required] }),
    reason: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(2000)] }),
    subject: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(250)] }),
    body: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(4000)] })
  });

  readonly summonDialogVisible = signal(false);
  readonly selectedSummon = signal<SummonDto | null>(null);
  readonly summonHistory = signal<SummonHistoryDto | null>(null);
  readonly activeGuardians = signal<readonly StudentGuardianLinkDto[]>([]);
  readonly summonActionMode = signal<SummonActionMode>('schedule');
  readonly summonSaving = signal(false);
  readonly summonReconciling = signal(false);
  readonly summonConflict = signal(false);
  readonly noShowDialogVisible = signal(false);
  readonly noShowNotes = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(2000)] });
  readonly scheduleForm = new FormGroup({
    appointmentAt: new FormControl<Date | null>(null, { validators: [Validators.required] }),
    location: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(500)] }),
    instructions: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2000)] }),
    guardianProfileId: new FormControl<number | null>(null, { validators: [Validators.required] })
  });
  readonly transitionNarrative = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(4000)] });
  readonly observationForm = new FormGroup({
    goals: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(2000)] }),
    startDate: new FormControl<Date | null>(null, { validators: [Validators.required] }),
    reviewDate: new FormControl<Date | null>(null, { validators: [Validators.required] }),
    endDate: new FormControl<Date | null>(null),
    indicators: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(4000)] }),
    notes: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(4000)] })
  });
  readonly verificationDetails = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(4000)] });

  get canManageReferrals(): boolean { return this.auth.hasRole('SocialWorker') && this.auth.hasPermission('Referral.Manage'); }
  get canSchedule(): boolean { return this.auth.hasRole('SocialWorker') && this.auth.hasPermission('Summon.Schedule'); }
  get canAttend(): boolean { return this.auth.hasRole('SocialWorker') && this.auth.hasPermission('Summon.MarkAttended'); }
  get canObserve(): boolean { return this.auth.hasRole('SocialWorker') && this.auth.hasPermission('Summon.StartObservation'); }
  get canImprove(): boolean { return this.auth.hasRole('SocialWorker') && this.auth.hasPermission('Summon.MarkImproved'); }
  get canCreateSummon(): boolean { return this.auth.hasRole('SocialWorker') && this.auth.hasPermission('Summon.Create'); }
  get canMessageGuardian(): boolean { return this.auth.hasRole('SocialWorker') && this.auth.hasPermission('Messaging.Send'); }
  referralBusy(): boolean { return this.referralSaving() || this.referralReconciling(); }
  summonBusy(): boolean { return this.summonSaving() || this.summonReconciling(); }

  ngOnInit(): void {
    this.section.set(this.route.snapshot.data['crmView'] === 'summons' ? 'summons' : 'cases');
    this.loadDashboard();
    this.load();
    merge(this.search.valueChanges, this.statusFilter.valueChanges, this.priorityFilter.valueChanges)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => { this.page.set(1); this.load(); });
  }

  load(): void {
    this.loading.set(true);
    this.errorMessage.set('');
    const search = this.search.value.trim() || undefined;
    if (this.section() === 'cases') {
      this.api.listReferrals({
        pageNumber: this.page(), pageSize: this.pageSize, search, sortDirection: 'desc',
        status: (this.statusFilter.value as StudentReferralStatus | null) ?? undefined,
        priority: this.priorityFilter.value ?? undefined
      })
        .pipe(finalize(() => this.loading.set(false)))
        .subscribe({
          next: response => {
            if (!response.isSuccess || !response.data) { this.errorMessage.set(response.errors[0] ?? response.message ?? 'تعذر تحميل بيانات المتابعة.'); return; }
            this.referrals.set(response.data.items);
            this.totalCount.set(response.data.totalCount);
          },
          error: error => this.errorMessage.set(this.httpMessage(error, 'تعذر تحميل بيانات المتابعة.'))
        });
      return;
    }
    this.api.listSummons({
      pageNumber: this.page(), pageSize: this.pageSize, search, sortDirection: 'desc',
      status: (this.statusFilter.value as GuardianSummonStatus | null) ?? undefined,
      priority: this.priorityFilter.value ?? undefined
    })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: response => {
          if (!response.isSuccess || !response.data) { this.errorMessage.set(response.errors[0] ?? response.message ?? 'تعذر تحميل بيانات المتابعة.'); return; }
          this.summons.set(response.data.items);
          this.totalCount.set(response.data.totalCount);
        },
        error: error => this.errorMessage.set(this.httpMessage(error, 'تعذر تحميل بيانات المتابعة.'))
      });
  }

  changePage(delta: number): void {
    const next = this.page() + delta;
    if (next < 1 || (delta > 0 && next > Math.ceil(this.totalCount() / this.pageSize))) return;
    this.page.set(next);
    this.load();
  }
  totalPages(): number { return Math.max(1, Math.ceil(this.totalCount() / this.pageSize)); }

  dashboardMetricIcon(code: string): string {
    return ({
      ActiveCases: 'pi-briefcase',
      Pending: 'pi-calendar-clock',
      Attended: 'pi-user-plus',
      UnderObservation: 'pi-eye',
      Improved: 'pi-check-circle'
    } as Record<string, string>)[code] ?? 'pi-chart-bar';
  }

  dashboardMetricTone(code: string): string {
    return ({
      ActiveCases: 'danger',
      Pending: 'warning',
      Attended: 'info',
      UnderObservation: 'purple',
      Improved: 'success'
    } as Record<string, string>)[code] ?? 'info';
  }

  displayActionDescription(action: StudentCaseActionDto): string {
    const value = action.description.trim();
    if (value.startsWith('Referral assigned to social worker')) return 'تم إسناد الإحالة إلى الموجه الطلابي.';
    if (value === 'Referral accepted and moved to in-progress') return 'تم قبول الإحالة وبدء المتابعة.';
    if (value.startsWith('Referral resolved:')) return 'تم حل الإحالة بعد اكتمال خطة المتابعة.';
    if (value.startsWith('Referral reopened:')) return 'أُعيد فتح الإحالة لاستكمال المتابعة.';
    if (this.hasEncodingDamage(value)) return 'إجراء متابعة مسجل — تعذر عرض نص بيانات الاختبار القديمة.';
    return value;
  }

  displayActionResult(value: string): string {
    return this.hasEncodingDamage(value) ? 'نتيجة متابعة مسجلة.' : value;
  }

  referralsFor(status: StudentReferralStatus): readonly ReferralDto[] { return this.referrals().filter(item => item.status === status); }
  summonsFor(status: GuardianSummonStatus): readonly SummonDto[] { return this.summons().filter(item => item.status === status); }

  openReferral(item: ReferralDto, mode: ReferralActionMode): void {
    if (!this.canManageReferrals) return;
    this.referralActionMode.set(mode);
    this.selectedReferral.set(item);
    this.referralConflict.set(false);
    this.referralActionForm.reset({ actionType: 'CounselingSession', description: '', actionAt: null, result: '', narrative: '' });
    this.referralDialogVisible.set(true);
    this.api.getReferral(item.id).subscribe({ next: response => { if (response.isSuccess && response.data) this.selectedReferral.set(response.data); } });
  }

  openEngagement(item: ReferralDto, mode: EngagementMode): void {
    if ((mode === 'summon' && !this.canCreateSummon) || (mode === 'message' && !this.canMessageGuardian)) return;
    this.selectedReferral.set(item);
    this.engagementMode.set(mode);
    this.engagementIdempotencyKey.set(this.api.createIdempotencyKey());
    this.activeGuardians.set([]);
    this.engagementForm.reset({
      guardianProfileId: null,
      priority: item.priority,
      reason: `متابعة الإحالة #${item.id}`,
      subject: `متابعة حالة الطالب ${item.student.displayName}`,
      body: ''
    });
    this.engagementDialogVisible.set(true);
    this.api.getStudentGuardians(item.student.id).subscribe({
      next: response => {
        if (response.isSuccess && response.data) {
          const guardians = response.data.filter(link => link.isActive);
          this.activeGuardians.set(guardians);
          this.engagementForm.controls.guardianProfileId.setValue(guardians.find(link => link.guardian.isPrimary)?.guardian.id ?? guardians[0]?.guardian.id ?? null);
        }
      }
    });
  }

  submitEngagement(): void {
    const referral = this.selectedReferral();
    const value = this.engagementForm.getRawValue();
    if (!referral || this.engagementSaving() || value.guardianProfileId === null) return;
    this.engagementForm.markAllAsTouched();
    this.engagementSaving.set(true);
    const idempotencyKey = this.engagementIdempotencyKey() ?? this.api.createIdempotencyKey();
    this.engagementIdempotencyKey.set(idempotencyKey);
    if (this.engagementMode() === 'summon') {
      if (!value.reason.trim()) { this.engagementSaving.set(false); return; }
      this.api.createSummon({
        studentId: referral.student.id,
        referralId: referral.id,
        reason: value.reason.trim(),
        priority: value.priority,
        guardianProfileId: value.guardianProfileId
      }, idempotencyKey).pipe(finalize(() => this.engagementSaving.set(false))).subscribe({
        next: response => {
          if (!response.isSuccess || !response.data) { this.toast.warn('لم يتم إنشاء الاستدعاء', response.errors[0] ?? response.message); return; }
          this.engagementDialogVisible.set(false);
          this.engagementIdempotencyKey.set(null);
          this.toast.success('تم إنشاء الاستدعاء', 'يمكن جدولة الموعد من شاشة الاستدعاءات.');
        },
        error: error => this.toast.error('تعذر إنشاء الاستدعاء', this.httpMessage(error, 'حاول مرة أخرى.'))
      });
      return;
    }
    if (!value.subject.trim() || !value.body.trim()) { this.engagementSaving.set(false); return; }
    this.api.createConversation({
      studentId: referral.student.id,
      threadType: 'GuardianSocialWorker',
      targetInstructorProfileId: null,
      targetStaffRole: null,
      targetStaffUserId: null,
      subject: value.subject.trim(),
      initialBody: value.body.trim(),
      idempotencyKey,
      referralId: referral.id,
      targetGuardianProfileId: value.guardianProfileId
    }).pipe(finalize(() => this.engagementSaving.set(false))).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) { this.toast.warn('لم تُفتح المحادثة', response.errors[0] ?? response.message); return; }
        this.engagementDialogVisible.set(false);
        this.engagementIdempotencyKey.set(null);
        this.toast.success('تم فتح محادثة الحالة', 'المحادثة مقصورة على ولي الأمر والأخصائي المكلف.');
      },
      error: error => this.toast.error('تعذر فتح المحادثة', this.httpMessage(error, 'حاول مرة أخرى.'))
    });
  }

  submitReferralAction(): void {
    const item = this.selectedReferral();
    const mode = this.referralActionMode();
    if (!item || this.referralBusy()) return;
    if (mode === 'addAction') {
      this.referralActionForm.controls.description.markAsTouched();
      if (!this.referralActionForm.controls.description.value.trim()) return;
    } else if (mode !== 'accept') {
      this.referralActionForm.controls.narrative.markAsTouched();
      if (!this.referralActionForm.controls.narrative.value.trim()) return;
    }
    const values = this.referralActionForm.getRawValue();
    let request$: Observable<ApiResponse<ReferralDto>>;
    if (mode === 'accept') request$ = this.api.acceptReferral(item.id, { rowVersion: item.rowVersion });
    else if (mode === 'addAction') request$ = this.api.addReferralAction(item.id, {
      actionType: values.actionType,
      description: values.description.trim(),
      actionAt: values.actionAt?.toISOString() ?? null,
      result: values.result.trim() || null,
      rowVersion: item.rowVersion
    });
    else if (mode === 'resolve') request$ = this.api.resolveReferral(item.id, { resolutionNote: values.narrative.trim(), rowVersion: item.rowVersion });
    else request$ = this.api.reopenReferral(item.id, { reason: values.narrative.trim(), rowVersion: item.rowVersion });
    this.referralSaving.set(true);
    request$.pipe(finalize(() => this.referralSaving.set(false))).subscribe({
      next: response => this.handleReferralResponse(response),
      error: (error: HttpErrorResponse) => this.handleReferralError(item.id, error)
    });
  }

  openSummon(item: SummonDto, mode: SummonActionMode): void {
    if (!this.isSummonActionAllowed(item, mode)) return;
    this.summonActionMode.set(mode);
    this.selectedSummon.set(item);
    this.summonHistory.set(null);
    this.summonConflict.set(false);
    this.transitionNarrative.reset('');
    this.verificationDetails.reset('');
    this.observationForm.reset({
      goals: item.observationGoals ?? '',
      startDate: item.observationStartDate ? new Date(`${item.observationStartDate}T00:00:00`) : new Date(),
      reviewDate: item.observationReviewDate ? new Date(`${item.observationReviewDate}T00:00:00`) : null,
      endDate: item.observationEndDate ? new Date(`${item.observationEndDate}T00:00:00`) : null,
      indicators: item.observationIndicators?.join('\n') ?? '',
      notes: item.observationNotes ?? ''
    });
    this.scheduleForm.reset({ appointmentAt: item.scheduledAt ? new Date(item.scheduledAt) : null, location: item.location ?? '', instructions: item.instructions ?? '', guardianProfileId: item.guardian.id });
    this.summonDialogVisible.set(true);
    this.reloadSummonContext(item.id, item.student.id);
  }

  submitSummonAction(): void {
    const item = this.selectedSummon();
    if (!item || this.summonBusy() || !this.isSummonActionAllowed(item, this.summonActionMode())) return;
    let request$: Observable<ApiResponse<SummonDto>>;
    if (this.summonActionMode() === 'schedule') {
      this.scheduleForm.markAllAsTouched();
      const value = this.scheduleForm.getRawValue();
      if (this.scheduleForm.invalid || !value.appointmentAt || value.appointmentAt.getTime() <= Date.now() || value.guardianProfileId === null) return;
      request$ = this.api.scheduleSummon(item.id, {
        appointmentAt: value.appointmentAt.toISOString(),
        location: value.location.trim(),
        instructions: value.instructions.trim() || null,
        guardianProfileId: value.guardianProfileId,
        rowVersion: item.rowVersion
      });
    } else if (this.summonActionMode() === 'observe') {
      this.observationForm.markAllAsTouched();
      const value = this.observationForm.getRawValue();
      const indicators = value.indicators.split(/\r?\n|,/).map(indicator => indicator.trim()).filter(Boolean);
      if (this.observationForm.invalid || !value.startDate || !value.reviewDate || indicators.length === 0) return;
      request$ = this.api.startObservation(item.id, {
        goals: value.goals.trim(),
        startDate: this.dateOnly(value.startDate),
        reviewDate: this.dateOnly(value.reviewDate),
        endDate: value.endDate ? this.dateOnly(value.endDate) : null,
        responsibleStaffUserId: this.auth.currentUser()?.userId ?? '',
        measurableIndicators: indicators,
        notes: value.notes.trim(),
        rowVersion: item.rowVersion
      });
    } else {
      this.transitionNarrative.markAsTouched();
      const narrative = this.transitionNarrative.value.trim();
      if (!narrative) return;
      if (this.summonActionMode() === 'attend') request$ = this.api.attendSummon(item.id, { attendanceNotes: narrative, rowVersion: item.rowVersion });
      else {
        this.verificationDetails.markAsTouched();
        if (this.verificationDetails.invalid) return;
        request$ = this.api.markImproved(item.id, {
          outcomeEvidence: narrative,
          verificationDetails: this.verificationDetails.value.trim(),
          rowVersion: item.rowVersion
        });
      }
    }
    this.summonSaving.set(true);
    request$.pipe(finalize(() => this.summonSaving.set(false))).subscribe({
      next: response => this.handleSummonResponse(response),
      error: (error: HttpErrorResponse) => this.handleSummonError(item, error)
    });
  }

  isSummonActionAllowed(item: SummonDto, mode: SummonActionMode): boolean {
    if (mode === 'schedule') return this.canSchedule && item.status === 'Pending';
    if (mode === 'attend') return this.canAttend && item.status === 'Pending' && item.scheduledAt !== null;
    if (mode === 'observe') return this.canObserve && item.status === 'Attended';
    return this.canImprove && item.status === 'UnderObservation';
  }

  canMarkNoShow(item: SummonDto): boolean {
    return this.canSchedule && item.status === 'Pending' && item.scheduledAt !== null
      && new Date(item.scheduledAt).getTime() <= Date.now();
  }

  markNoShow(item: SummonDto): void {
    if (!this.canMarkNoShow(item) || this.summonBusy()) return;
    this.selectedSummon.set(item);
    this.noShowNotes.reset('');
    this.noShowDialogVisible.set(true);
  }

  submitNoShow(): void {
    const item = this.selectedSummon();
    const notes = this.noShowNotes.value.trim();
    this.noShowNotes.markAsTouched();
    if (!item || !this.canMarkNoShow(item) || !notes || this.summonBusy()) return;
    this.summonSaving.set(true);
    this.api.markSummonNoShow(item.id, { notes, rowVersion: item.rowVersion })
      .pipe(finalize(() => this.summonSaving.set(false)))
      .subscribe({
        next: response => { this.noShowDialogVisible.set(false); this.handleSummonResponse(response); },
        error: (error: HttpErrorResponse) => this.handleSummonError(item, error)
      });
  }

  referralStatusLabel(status: StudentReferralStatus): string {
    return ({ Open: 'مفتوحة', Assigned: 'مسندة', InProgress: 'قيد المتابعة', Resolved: 'تم الحل', Closed: 'مغلقة' })[status];
  }
  summonStatusLabel(item: SummonDto): string {
    if (item.status === 'Pending') return item.scheduledAt ? 'موعد محدد — بانتظار الحضور' : 'بانتظار تحديد موعد';
    return ({ Attended: 'تم الحضور', UnderObservation: 'تحت الملاحظة', Improved: 'تحسّن' })[item.status];
  }
  priorityLabel(priority: ReferralDto['priority']): string { return ({ Normal: 'عادية', High: 'عالية', Critical: 'حرجة' })[priority]; }
  prioritySeverity(priority: ReferralDto['priority']): 'info' | 'warning' | 'danger' { return priority === 'Critical' ? 'danger' : priority === 'High' ? 'warning' : 'info'; }
  sourceLabel(source: ReferralDto['sourceSnapshot']['sourceType']): string {
    return ({ MorningDelay: 'تأخر صباحي', SessionDelay: 'تأخر عن الحصة', AcademicConcern: 'قلق أكاديمي', Behavior: 'سلوك', Absence: 'غياب', RepeatedEntryPermit: 'تكرار تصريح دخول', Manual: 'إحالة يدوية' })[source];
  }
  referralDialogTitle(): string {
    return ({ accept: 'بدء متابعة الحالة', addAction: 'إضافة إجراء للحالة', resolve: 'حل الحالة', reopen: 'إعادة فتح الحالة' })[this.referralActionMode()];
  }
  summonDialogTitle(): string {
    return ({ schedule: 'تحديد موعد', attend: 'تسجيل الحضور', observe: 'وضع تحت الملاحظة', improve: 'تحسن الحالة' })[this.summonActionMode()];
  }
  summonNarrativeLabel(): string {
    return ({ schedule: '', attend: 'ملخص الاجتماع', observe: 'خطة الملاحظة والمؤشرات القابلة للقياس', improve: 'أدلة تحسن الحالة' })[this.summonActionMode()];
  }
  transitionLabel(from: string | null, to: string): string {
    if (from === 'Pending' && to === 'Pending') return 'تم تحديد/تعديل الموعد';
    return `من ${from ? this.rawSummonStatusLabel(from) : 'الإنشاء'} إلى ${this.rawSummonStatusLabel(to)}`;
  }
  formatDateTime(value: string | null): string {
    if (!value) return '—';
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('ar-SA', { dateStyle: 'medium', timeStyle: 'short' }).format(date);
  }

  private handleReferralResponse(response: ApiResponse<ReferralDto>): void {
    if (!response.isSuccess || !response.data) {
      if (this.isConflictMessage(response.message, response.errors)) this.refreshReferralAfterConflict();
      else this.toast.warn('لم يُحفظ الإجراء', response.errors[0] ?? response.message);
      return;
    }
    this.replaceReferral(response.data);
    this.selectedReferral.set(response.data);
    this.referralDialogVisible.set(false);
    this.toast.success('تم تحديث الحالة', 'تم اعتماد أحدث إصدار للحالة.');
  }
  private handleReferralError(id: number, error: HttpErrorResponse): void {
    if (error.status === 409) { this.refreshReferralAfterConflict(id); return; }
    if (error.status === 0 || error.status >= 500) { this.refreshReferralAfterConflict(id, true); return; }
    this.toast.error('تعذر حفظ الإجراء', this.httpMessage(error, 'حاول مرة أخرى.'));
  }
  private refreshReferralAfterConflict(id = this.selectedReferral()?.id, uncertainWrite = false): void {
    if (!id) return;
    this.referralReconciling.set(true);
    this.api.getReferral(id).pipe(finalize(() => this.referralReconciling.set(false))).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) {
          this.referralConflict.set(true);
          this.toast.warn('تعذر تأكيد النتيجة', 'احتفظنا بمسودتك. حدّث الحالة يدويًا قبل إعادة المحاولة.');
          return;
        }
        const original = this.selectedReferral();
        const achieved = original ? this.referralOutcomeAchieved(original, response.data) : false;
        this.selectedReferral.set(response.data);
        this.replaceReferral(response.data);
        if (achieved) {
          this.referralConflict.set(false);
          this.referralDialogVisible.set(false);
          this.toast.success('تم تأكيد حفظ الإجراء', 'أكدت قراءة الخادم أن النتيجة المطلوبة تحققت؛ لم نكرر الطلب.');
          return;
        }
        this.referralConflict.set(true);
        this.toast.warn(
          uncertainWrite ? 'تعذر تأكيد تنفيذ الإجراء' : 'عدّل مستخدم آخر هذه الحالة',
          'احتفظنا بمسودتك وجلبنا الحالة الأحدث. راجعها ثم أعد المحاولة صراحةً؛ لن نكرر الإجراء تلقائيًا.');
      },
      error: error => {
        this.referralConflict.set(true);
        this.toast.error('تعذر تحديث الحالة', this.httpMessage(error, 'احتفظنا بمسودتك؛ حدّث الصفحة قبل إعادة المحاولة.'));
      }
    });
  }
  private handleSummonResponse(response: ApiResponse<SummonDto>): void {
    if (!response.isSuccess || !response.data) {
      if (this.isConflictMessage(response.message, response.errors)) this.refreshSummonAfterConflict();
      else this.toast.warn('لم يُحفظ الانتقال', response.errors[0] ?? response.message);
      return;
    }
    this.replaceSummon(response.data);
    this.selectedSummon.set(response.data);
    this.summonDialogVisible.set(false);
    this.toast.success('تم تحديث الاستدعاء', this.summonStatusLabel(response.data));
  }
  private handleSummonError(item: SummonDto, error: HttpErrorResponse): void {
    if (error.status === 409) { this.refreshSummonAfterConflict(item.id, item.student.id); return; }
    if (error.status === 0 || error.status >= 500) { this.refreshSummonAfterConflict(item.id, item.student.id, true); return; }
    this.toast.error('تعذر حفظ الانتقال', this.httpMessage(error, 'حاول مرة أخرى.'));
  }
  private refreshSummonAfterConflict(id = this.selectedSummon()?.id, studentId = this.selectedSummon()?.student.id, uncertainWrite = false): void {
    if (!id || !studentId) return;
    this.summonReconciling.set(true);
    forkJoin({ detail: this.api.getSummon(id), history: this.api.getSummonHistory(id), guardians: this.api.getStudentGuardians(studentId) })
      .pipe(finalize(() => this.summonReconciling.set(false)))
      .subscribe({
        next: ({ detail, history, guardians }) => {
          const original = this.selectedSummon();
          if (history.isSuccess && history.data) this.summonHistory.set(history.data);
          if (guardians.isSuccess && guardians.data) this.activeGuardians.set(guardians.data.filter(link => link.isActive));
          if (!detail.isSuccess || !detail.data) {
            this.summonConflict.set(true);
            this.toast.warn('تعذر تأكيد النتيجة', 'احتفظنا بمدخلاتك. حدّث الاستدعاء يدويًا قبل إعادة المحاولة.');
            return;
          }
          const achieved = original ? this.summonOutcomeAchieved(original, detail.data, history.data ?? null) : false;
          this.selectedSummon.set(detail.data);
          this.replaceSummon(detail.data);
          if (achieved) {
            this.summonConflict.set(false);
            this.summonDialogVisible.set(false);
            this.noShowDialogVisible.set(false);
            this.toast.success('تم تأكيد حفظ الانتقال', 'أكدت قراءة الخادم أن النتيجة المطلوبة تحققت؛ لم نكرر الطلب.');
            return;
          }
          this.summonConflict.set(true);
          this.toast.warn(
            uncertainWrite ? 'تعذر تأكيد تنفيذ الانتقال' : 'سبق تعديل الاستدعاء',
            'احتفظنا بمدخلاتك وجلبنا الانتقال الفائز. راجع النسخة الأحدث ثم أعد المحاولة صراحةً.');
        },
        error: error => {
          this.summonConflict.set(true);
          this.toast.error('تعذر تحديث الاستدعاء', this.httpMessage(error, 'احتفظنا بمدخلاتك؛ حدّث الصفحة قبل إعادة المحاولة.'));
        }
      });
  }

  private referralOutcomeAchieved(original: ReferralDto, latest: ReferralDto): boolean {
    const mode = this.referralActionMode();
    if (mode === 'accept') return latest.status !== 'Assigned' && latest.status !== 'Open';
    if (mode === 'resolve') return latest.status === 'Resolved' || latest.status === 'Closed';
    if (mode === 'reopen') return latest.status === 'InProgress';
    const draft = this.referralActionForm.getRawValue();
    return latest.actions.length > original.actions.length && latest.actions.some(action =>
      action.actionType === draft.actionType && action.description === draft.description.trim());
  }

  private summonOutcomeAchieved(original: SummonDto, latest: SummonDto, history: SummonHistoryDto | null): boolean {
    if (this.noShowDialogVisible()) {
      const notes = this.noShowNotes.value.trim();
      return history?.appointments?.some(item => item.action === 'NoShow' && item.notes === notes) ?? false;
    }
    const mode = this.summonActionMode();
    if (mode === 'attend') return latest.status !== 'Pending';
    if (mode === 'observe') return latest.status === 'UnderObservation' || latest.status === 'Improved';
    if (mode === 'improve') return latest.status === 'Improved';
    const draft = this.scheduleForm.getRawValue();
    return latest.status === 'Pending'
      && !!latest.scheduledAt
      && !!draft.appointmentAt
      && new Date(latest.scheduledAt).getTime() === draft.appointmentAt.getTime()
      && latest.location === draft.location.trim()
      && latest.guardian.id === draft.guardianProfileId
      && original.rowVersion !== latest.rowVersion;
  }
  private reloadSummonContext(id: number, studentId: number): void {
    forkJoin({ detail: this.api.getSummon(id), history: this.api.getSummonHistory(id), guardians: this.api.getStudentGuardians(studentId) }).subscribe({
      next: ({ detail, history, guardians }) => {
        if (detail.isSuccess && detail.data) { this.selectedSummon.set(detail.data); this.replaceSummon(detail.data); }
        if (history.isSuccess && history.data) this.summonHistory.set(history.data);
        if (guardians.isSuccess && guardians.data) this.activeGuardians.set(guardians.data.filter(link => link.isActive));
      },
      error: error => this.toast.error('تعذر تحديث تفاصيل الاستدعاء', this.httpMessage(error, 'حاول تحديث الصفحة.'))
    });
  }
  private replaceReferral(updated: ReferralDto): void { this.referrals.update(items => items.map(item => item.id === updated.id ? updated : item)); }
  private replaceSummon(updated: SummonDto): void { this.summons.update(items => items.map(item => item.id === updated.id ? updated : item)); }
  private loadDashboard(): void {
    this.api.getSocialWorkerDashboard().subscribe({
      next: response => {
        if (response.isSuccess && response.data) {
          this.dashboardCounts.set([...response.data.cases, ...response.data.summons]);
        }
      }
    });
  }
  private dateOnly(value: Date): string {
    const year = value.getFullYear();
    const month = `${value.getMonth() + 1}`.padStart(2, '0');
    const day = `${value.getDate()}`.padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
  private rawSummonStatusLabel(status: string): string { return ({ Pending: 'بانتظار الموعد/الحضور', Attended: 'تم الحضور', UnderObservation: 'تحت الملاحظة', Improved: 'تحسّن' } as Record<string, string>)[status] ?? status; }
  private isConflictMessage(message: string, errors: readonly string[]): boolean {
    const value = `${message} ${errors.join(' ')}`.toLowerCase();
    return value.includes('rowversion') || value.includes('row version') || value.includes('concurrency') || value.includes('مستخدم آخر');
  }
  private httpMessage(error: unknown, fallback: string): string { return extractHttpErrorMessage(error) ?? fallback; }
  private hasEncodingDamage(value: string): boolean { return /\?{3,}/.test(value); }
}
