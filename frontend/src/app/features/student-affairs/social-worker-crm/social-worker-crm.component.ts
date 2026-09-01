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
import { Observable, finalize, forkJoin } from 'rxjs';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import {
  GuardianSummonStatus,
  REFERRAL_STATUSES,
  ReferralDto,
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

type ReferralActionMode = 'accept' | 'addAction' | 'resolve' | 'reopen';
type SummonActionMode = 'schedule' | 'attend' | 'observe' | 'improve';

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
  styleUrl: './social-worker-crm.component.css'
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
  readonly search = new FormControl('', { nonNullable: true });
  readonly listMode = signal<'kanban' | 'list'>('kanban');
  readonly referralStatuses = REFERRAL_STATUSES;
  readonly summonStatuses = SUMMON_STATUSES;

  readonly referralDialogVisible = signal(false);
  readonly selectedReferral = signal<ReferralDto | null>(null);
  readonly referralActionMode = signal<ReferralActionMode>('accept');
  readonly referralSaving = signal(false);
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
    { label: 'استدعاء ولي الأمر', value: 'GuardianSummon' },
    { label: 'توصية بحسم درجات', value: 'GradeDeductionRecommendation' },
    { label: 'توصية بإيقاف', value: 'SuspensionRecommendation' },
    { label: 'إحالة إلى لجنة حقوق الطفل', value: 'ChildRightsCommitteeReferral' },
    { label: 'إجراء آخر', value: 'Other' }
  ];

  readonly summonDialogVisible = signal(false);
  readonly selectedSummon = signal<SummonDto | null>(null);
  readonly summonHistory = signal<SummonHistoryDto | null>(null);
  readonly activeGuardians = signal<readonly StudentGuardianLinkDto[]>([]);
  readonly summonActionMode = signal<SummonActionMode>('schedule');
  readonly summonSaving = signal(false);
  readonly summonConflict = signal(false);
  readonly scheduleForm = new FormGroup({
    appointmentAt: new FormControl<Date | null>(null, { validators: [Validators.required] }),
    location: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(500)] }),
    instructions: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2000)] }),
    guardianProfileId: new FormControl<number | null>(null, { validators: [Validators.required] })
  });
  readonly transitionNarrative = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(4000)] });

  get canManageReferrals(): boolean { return this.auth.hasRole('SocialWorker') && this.auth.hasPermission('Referral.Manage'); }
  get canSchedule(): boolean { return this.auth.hasRole('SocialWorker') && this.auth.hasPermission('Summon.Schedule'); }
  get canAttend(): boolean { return this.auth.hasRole('SocialWorker') && this.auth.hasPermission('Summon.MarkAttended'); }
  get canObserve(): boolean { return this.auth.hasRole('SocialWorker') && this.auth.hasPermission('Summon.StartObservation'); }
  get canImprove(): boolean { return this.auth.hasRole('SocialWorker') && this.auth.hasPermission('Summon.MarkImproved'); }

  ngOnInit(): void {
    this.section.set(this.route.snapshot.data['crmView'] === 'summons' ? 'summons' : 'cases');
    this.load();
    this.search.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.load());
  }

  load(): void {
    this.loading.set(true);
    this.errorMessage.set('');
    const search = this.search.value.trim() || undefined;
    if (this.section() === 'cases') {
      this.api.listReferrals({ pageNumber: 1, pageSize: 100, search, sortDirection: 'desc' })
        .pipe(finalize(() => this.loading.set(false)))
        .subscribe({
          next: response => {
            if (!response.isSuccess || !response.data) { this.errorMessage.set(response.errors[0] ?? response.message ?? 'تعذر تحميل بيانات المتابعة.'); return; }
            this.referrals.set(response.data.items);
          },
          error: error => this.errorMessage.set(this.httpMessage(error, 'تعذر تحميل بيانات المتابعة.'))
        });
      return;
    }
    this.api.listSummons({ pageNumber: 1, pageSize: 100, search, sortDirection: 'desc' })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: response => {
          if (!response.isSuccess || !response.data) { this.errorMessage.set(response.errors[0] ?? response.message ?? 'تعذر تحميل بيانات المتابعة.'); return; }
          this.summons.set(response.data.items);
        },
        error: error => this.errorMessage.set(this.httpMessage(error, 'تعذر تحميل بيانات المتابعة.'))
      });
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

  submitReferralAction(): void {
    const item = this.selectedReferral();
    const mode = this.referralActionMode();
    if (!item || this.referralSaving()) return;
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
    this.scheduleForm.reset({ appointmentAt: item.scheduledAt ? new Date(item.scheduledAt) : null, location: item.location ?? '', instructions: item.instructions ?? '', guardianProfileId: item.guardian.id });
    this.summonDialogVisible.set(true);
    this.reloadSummonContext(item.id, item.student.id);
  }

  submitSummonAction(): void {
    const item = this.selectedSummon();
    if (!item || this.summonSaving() || !this.isSummonActionAllowed(item, this.summonActionMode())) return;
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
    } else {
      this.transitionNarrative.markAsTouched();
      const narrative = this.transitionNarrative.value.trim();
      if (!narrative) return;
      if (this.summonActionMode() === 'attend') request$ = this.api.attendSummon(item.id, { attendanceNotes: narrative, rowVersion: item.rowVersion });
      else if (this.summonActionMode() === 'observe') request$ = this.api.startObservation(item.id, { observationPlan: narrative, rowVersion: item.rowVersion });
      else request$ = this.api.markImproved(item.id, { outcomeEvidence: narrative, rowVersion: item.rowVersion });
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
    this.toast.error('تعذر حفظ الإجراء', this.httpMessage(error, 'حاول مرة أخرى.'));
  }
  private refreshReferralAfterConflict(id = this.selectedReferral()?.id): void {
    if (!id) return;
    this.referralConflict.set(true);
    this.toast.warn('عدّل مستخدم آخر هذه الحالة', 'احتفظنا بمسودتك وجلبنا الحالة الأحدث. راجعها ثم أكد أن المسودة ما زالت مناسبة؛ لن نكرر الإجراء تلقائيًا.');
    this.api.getReferral(id).subscribe({ next: response => { if (response.isSuccess && response.data) { this.selectedReferral.set(response.data); this.replaceReferral(response.data); } } });
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
    this.toast.error('تعذر حفظ الانتقال', this.httpMessage(error, 'حاول مرة أخرى.'));
  }
  private refreshSummonAfterConflict(id = this.selectedSummon()?.id, studentId = this.selectedSummon()?.student.id): void {
    if (!id || !studentId) return;
    this.summonConflict.set(true);
    this.toast.warn('سبق تعديل الاستدعاء', 'احتفظنا بالنص وجلبنا الانتقال الفائز. لن نعيد حالة قديمة أو نرسل الانتقال تلقائيًا.');
    this.reloadSummonContext(id, studentId);
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
  private rawSummonStatusLabel(status: string): string { return ({ Pending: 'بانتظار الموعد/الحضور', Attended: 'تم الحضور', UnderObservation: 'تحت الملاحظة', Improved: 'تحسّن' } as Record<string, string>)[status] ?? status; }
  private isConflictMessage(message: string, errors: readonly string[]): boolean {
    const value = `${message} ${errors.join(' ')}`.toLowerCase();
    return value.includes('rowversion') || value.includes('row version') || value.includes('concurrency') || value.includes('مستخدم آخر');
  }
  private httpMessage(error: unknown, fallback: string): string { return extractHttpErrorMessage(error) ?? fallback; }
}
