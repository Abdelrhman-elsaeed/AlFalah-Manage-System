import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, ElementRef, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  ReactiveFormsModule,
  ValidationErrors,
  Validators
} from '@angular/forms';
import { AuthService } from '../../../core/services/auth.service';
import { SettingsService } from '../../../core/services/settings.service';
import {
  CreateStudentAffairsSettingsRequestDto,
  SchoolStudentAffairsSettingsDto,
  STUDENT_AFFAIRS_PERMISSIONS,
  STUDENT_AFFAIRS_ROLES,
  StudentAffairsSettingsHistoryDto,
  UpdateStudentAffairsSettingsRequestDto
} from '../../../core/models/student-affairs-settings.models';
import { NonNullableFormBuilder } from '@angular/forms';

interface SettingsDraft {
  readonly arrivalCutoffLocalTime: string;
  readonly arrivalGraceMinutes: number;
  readonly morningDelayThresholdPerTerm: number;
  readonly behaviorIncidentMultiplePerTerm: number;
  readonly academicConcernThresholdPerTerm: number;
  readonly classroomEntryPermitThresholdPerTerm: number;
  readonly behaviorCountabilityPolicy: string;
  readonly absenceVisualAlertThresholdPerTerm: number;
  readonly absenceReferralThresholdPerTerm: number;
  readonly absenceChildRightsThresholdPerTerm: number;
  readonly auditReason: string;
}

interface PolicyOption {
  readonly value: string;
  readonly label: string;
}

const POSITIVE_INTEGER = [Validators.required, Validators.min(1), Validators.pattern(/^\d+$/)];

function orderedAbsenceThresholds(control: AbstractControl): ValidationErrors | null {
  const visual = Number(control.get('visual')?.value);
  const referral = Number(control.get('referral')?.value);
  const childRights = Number(control.get('childRights')?.value);
  return visual < referral && referral < childRights ? null : { thresholdOrder: true };
}

@Component({
  selector: 'app-student-affairs-settings',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './settings.component.html',
  styleUrls: ['./settings.component.css']
})
export class SettingsComponent implements OnInit {
  @ViewChild('errorSummary') private errorSummary?: ElementRef<HTMLElement>;

  private readonly fb = inject(NonNullableFormBuilder);
  private readonly settingsService = inject(SettingsService);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  private applyingServerState = false;
  private saveIdempotencyKey: string | null = null;
  private saveFingerprint: string | null = null;
  private resetIdempotencyKey: string | null = null;
  private resetFingerprint: string | null = null;

  readonly policies: readonly PolicyOption[] = [
    { value: 'all-upheld', label: 'جميع المخالفات المثبتة' }
  ];

  readonly form = this.fb.group({
    arrival: this.fb.group({
      cutoff: ['07:00', Validators.required],
      graceMinutes: [0, [Validators.required, Validators.min(0), Validators.pattern(/^\d+$/)]],
      morningDelayThreshold: [10, POSITIVE_INTEGER]
    }),
    observation: this.fb.group({
      behaviorMultiple: [10, POSITIVE_INTEGER],
      academicConcern: [3, POSITIVE_INTEGER],
      classroomPermit: [5, POSITIVE_INTEGER],
      behaviorPolicy: ['all-upheld', [Validators.required, Validators.maxLength(100), Validators.pattern(/^[\x20-\x7E]+$/)]]
    }),
    absence: this.fb.group({
      visual: [3, POSITIVE_INTEGER],
      referral: [5, POSITIVE_INTEGER],
      childRights: [10, POSITIVE_INTEGER]
    }, { validators: orderedAbsenceThresholds }),
    auditReason: ['']
  });

  readonly resetForm = this.fb.group({
    reason: ['', [Validators.required, Validators.maxLength(500)]],
    confirmed: [false, Validators.requiredTrue]
  });

  readonly settings = signal<SchoolStudentAffairsSettingsDto | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly resetting = signal(false);
  readonly resetDialogOpen = signal(false);
  readonly activeTab = signal<'settings' | 'history'>('settings');
  readonly successMessage = signal('');
  readonly errors = signal<readonly string[]>([]);
  readonly conflictMessage = signal('');
  readonly staleDraft = signal<SettingsDraft | null>(null);
  readonly effectiveCutoff = signal('—');

  readonly history = signal<readonly StudentAffairsSettingsHistoryDto[]>([]);
  readonly historyLoading = signal(false);
  readonly historyPage = signal(1);
  readonly historyTotalPages = signal(0);
  readonly historyTotalCount = signal(0);
  readonly pageSize = 10;

  readonly canManage = computed(() =>
    this.auth.hasRole(STUDENT_AFFAIRS_ROLES.officer) &&
    this.auth.hasPermission(STUDENT_AFFAIRS_PERMISSIONS.manageSettings));
  readonly isCustom = computed(() => {
    const current = this.settings();
    return current !== null && current.id !== null && !current.usesLockedDefaults;
  });

  ngOnInit(): void {
    this.form.controls.arrival.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.updateEffectiveCutoff());
    this.form.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        if (!this.applyingServerState && !this.saving()) {
          this.saveIdempotencyKey = null;
          this.saveFingerprint = null;
        }
      });
    this.loadSettings();
  }

  selectTab(tab: 'settings' | 'history'): void {
    this.activeTab.set(tab);
    if (tab === 'history' && this.history().length === 0) this.loadHistory(1);
  }

  loadSettings(): void {
    this.loading.set(true);
    this.settingsService.getSettings().subscribe({
      next: outcome => {
        if (outcome.kind === 'success') {
          this.applySettings(outcome.data);
        } else {
          this.setBusinessErrors(outcome.errors, outcome.message);
        }
        this.loading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.handleHttpError(error, 'تعذر تحميل إعدادات شؤون الطلاب.');
      }
    });
  }

  save(): void {
    if (!this.canManage() || this.saving()) return;
    this.configureAuditReasonValidator();
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.errors.set(['يرجى تصحيح الحقول الموضحة قبل الحفظ.']);
      this.focusErrors();
      return;
    }

    const current = this.settings();
    if (!current) return;
    const values = this.toSettingsValues();
    const request = this.isCustom()
      ? { ...values, auditReason: this.form.controls.auditReason.value.trim(), rowVersion: current.rowVersion }
      : values;
    const fingerprint = JSON.stringify(request);
    if (fingerprint !== this.saveFingerprint || !this.saveIdempotencyKey) {
      this.saveFingerprint = fingerprint;
      this.saveIdempotencyKey = this.settingsService.createIdempotencyKey();
    }

    this.clearFeedback();
    this.saving.set(true);
    const operation = this.isCustom()
      ? this.settingsService.updateSettings(request as UpdateStudentAffairsSettingsRequestDto, this.saveIdempotencyKey)
      : this.settingsService.createSettings(request as CreateStudentAffairsSettingsRequestDto, this.saveIdempotencyKey);

    operation.subscribe({
      next: outcome => {
        this.saving.set(false);
        if (outcome.kind === 'success') {
          this.applySettings(outcome.data);
          this.successMessage.set(outcome.message || 'تم حفظ إعدادات شؤون الطلاب بنجاح.');
          this.saveIdempotencyKey = null;
          this.saveFingerprint = null;
          this.staleDraft.set(null);
          return;
        }
        if (outcome.kind === 'conflict') {
          this.handleConflict();
          return;
        }
        this.setBusinessErrors(outcome.errors, outcome.message);
      },
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        if (error.status === 409) this.handleConflict();
        else this.handleHttpError(error, 'تعذر حفظ الإعدادات. احتفظنا بجميع القيم المدخلة.');
      }
    });
  }

  openResetDialog(): void {
    if (!this.canManage() || !this.isCustom()) return;
    this.resetForm.reset({ reason: '', confirmed: false });
    this.resetDialogOpen.set(true);
    this.errors.set([]);
  }

  closeResetDialog(): void {
    if (!this.resetting()) this.resetDialogOpen.set(false);
  }

  resetToDefaults(): void {
    const current = this.settings();
    if (!current || !this.canManage() || this.resetting()) return;
    if (this.resetForm.invalid) {
      this.resetForm.markAllAsTouched();
      return;
    }

    const reason = this.resetForm.controls.reason.value.trim();
    const fingerprint = JSON.stringify({ reason, rowVersion: current.rowVersion });
    if (fingerprint !== this.resetFingerprint || !this.resetIdempotencyKey) {
      this.resetFingerprint = fingerprint;
      this.resetIdempotencyKey = this.settingsService.createIdempotencyKey();
    }

    this.resetting.set(true);
    this.clearFeedback();
    this.settingsService.resetSettings(
      { reason, rowVersion: current.rowVersion },
      this.resetIdempotencyKey
    ).subscribe({
      next: outcome => {
        this.resetting.set(false);
        if (outcome.kind === 'success') {
          this.applySettings(outcome.data);
          this.successMessage.set(outcome.message || 'تمت استعادة الإعدادات الافتراضية المقفلة.');
          this.resetDialogOpen.set(false);
          this.resetIdempotencyKey = null;
          this.resetFingerprint = null;
          return;
        }
        if (outcome.kind === 'conflict') {
          this.handleConflict();
          return;
        }
        this.setBusinessErrors(outcome.errors, outcome.message);
      },
      error: (error: HttpErrorResponse) => {
        this.resetting.set(false);
        if (error.status === 409) this.handleConflict();
        else this.handleHttpError(error, 'تعذر استعادة الإعدادات الافتراضية.');
      }
    });
  }

  restoreDraft(): void {
    const draft = this.staleDraft();
    if (!draft) return;
    this.applyingServerState = true;
    this.form.patchValue({
      arrival: {
        cutoff: draft.arrivalCutoffLocalTime.slice(0, 5),
        graceMinutes: draft.arrivalGraceMinutes,
        morningDelayThreshold: draft.morningDelayThresholdPerTerm
      },
      observation: {
        behaviorMultiple: draft.behaviorIncidentMultiplePerTerm,
        academicConcern: draft.academicConcernThresholdPerTerm,
        classroomPermit: draft.classroomEntryPermitThresholdPerTerm,
        behaviorPolicy: draft.behaviorCountabilityPolicy
      },
      absence: {
        visual: draft.absenceVisualAlertThresholdPerTerm,
        referral: draft.absenceReferralThresholdPerTerm,
        childRights: draft.absenceChildRightsThresholdPerTerm
      },
      auditReason: draft.auditReason
    });
    this.applyingServerState = false;
    this.staleDraft.set(null);
    this.conflictMessage.set('تمت استعادة مسودتك فوق أحدث نسخة. راجع التغييرات ثم أرسلها صراحةً.');
  }

  loadHistory(page: number): void {
    if (page < 1 || (this.historyTotalPages() > 0 && page > this.historyTotalPages())) return;
    this.historyLoading.set(true);
    this.settingsService.getHistory({ pageNumber: page, pageSize: this.pageSize }).subscribe({
      next: outcome => {
        this.historyLoading.set(false);
        if (outcome.kind === 'success') {
          this.history.set([...outcome.data.items].sort((left, right) => right.version - left.version));
          this.historyPage.set(outcome.data.page);
          this.historyTotalPages.set(outcome.data.totalPages);
          this.historyTotalCount.set(outcome.data.totalCount);
        } else {
          this.setBusinessErrors(outcome.errors, outcome.message);
        }
      },
      error: (error: HttpErrorResponse) => {
        this.historyLoading.set(false);
        this.handleHttpError(error, 'تعذر تحميل سجل التغييرات.');
      }
    });
  }

  historyChanges(index: number): readonly string[] {
    const current = this.history()[index];
    const previous = this.history()[index + 1];
    if (!current) return [];
    if (!previous) return ['نسخة مرجعية؛ لا تتوفر النسخة السابقة في هذه الصفحة.'];

    const labels: Array<[keyof SchoolStudentAffairsSettingsDto, string]> = [
      ['arrivalCutoffLocalTime', 'وقت احتساب التأخر'],
      ['arrivalGraceMinutes', 'فترة السماح'],
      ['morningDelayThresholdPerTerm', 'حد التأخر الصباحي'],
      ['behaviorIncidentMultiplePerTerm', 'مضاعف المخالفات'],
      ['academicConcernThresholdPerTerm', 'حد الملاحظات الأكاديمية'],
      ['classroomEntryPermitThresholdPerTerm', 'حد تصاريح الدخول'],
      ['absenceVisualAlertThresholdPerTerm', 'التنبيه المرئي'],
      ['absenceReferralThresholdPerTerm', 'الإحالة والاستدعاء'],
      ['absenceChildRightsThresholdPerTerm', 'لجنة حقوق الطفل'],
      ['behaviorCountabilityPolicy', 'سياسة احتساب السلوك']
    ];
    return labels
      .filter(([key]) => current.settings[key] !== previous.settings[key])
      .map(([, label]) => label);
  }

  formatDate(value: string): string {
    const date = new Date(value);
    return Number.isNaN(date.getTime())
      ? value
      : new Intl.DateTimeFormat('ar-SA', { dateStyle: 'medium', timeStyle: 'short' }).format(date);
  }

  private applySettings(settings: SchoolStudentAffairsSettingsDto): void {
    this.settings.set(settings);
    this.applyingServerState = true;
    this.form.patchValue({
      arrival: {
        cutoff: settings.arrivalCutoffLocalTime.slice(0, 5),
        graceMinutes: settings.arrivalGraceMinutes,
        morningDelayThreshold: settings.morningDelayThresholdPerTerm
      },
      observation: {
        behaviorMultiple: settings.behaviorIncidentMultiplePerTerm,
        academicConcern: settings.academicConcernThresholdPerTerm,
        classroomPermit: settings.classroomEntryPermitThresholdPerTerm,
        behaviorPolicy: settings.behaviorCountabilityPolicy
      },
      absence: {
        visual: settings.absenceVisualAlertThresholdPerTerm,
        referral: settings.absenceReferralThresholdPerTerm,
        childRights: settings.absenceChildRightsThresholdPerTerm
      },
      auditReason: ''
    });
    this.configureAuditReasonValidator();
    this.applyingServerState = false;
    this.updateEffectiveCutoff();
  }

  private configureAuditReasonValidator(): void {
    const control = this.form.controls.auditReason;
    control.setValidators(this.isCustom() ? [Validators.required, Validators.maxLength(500)] : []);
    control.updateValueAndValidity({ emitEvent: false });
  }

  private toSettingsValues(): CreateStudentAffairsSettingsRequestDto {
    const value = this.form.getRawValue();
    return {
      arrivalCutoffLocalTime: this.toTimeOnly(value.arrival.cutoff),
      arrivalGraceMinutes: Number(value.arrival.graceMinutes),
      morningDelayThresholdPerTerm: Number(value.arrival.morningDelayThreshold),
      behaviorIncidentMultiplePerTerm: Number(value.observation.behaviorMultiple),
      academicConcernThresholdPerTerm: Number(value.observation.academicConcern),
      classroomEntryPermitThresholdPerTerm: Number(value.observation.classroomPermit),
      behaviorCountabilityPolicy: value.observation.behaviorPolicy,
      absenceVisualAlertThresholdPerTerm: Number(value.absence.visual),
      absenceReferralThresholdPerTerm: Number(value.absence.referral),
      absenceChildRightsThresholdPerTerm: Number(value.absence.childRights)
    };
  }

  private captureDraft(): SettingsDraft {
    const values = this.toSettingsValues();
    return { ...values, auditReason: this.form.controls.auditReason.value };
  }

  private handleConflict(): void {
    this.staleDraft.set(this.captureDraft());
    this.conflictMessage.set('تم تعديل السجل بواسطة مستخدم آخر. عُرضت أحدث نسخة مع الاحتفاظ بمسودتك.');
    this.loadSettings();
    this.focusErrors();
  }

  private setBusinessErrors(errors: readonly string[], message: string): void {
    this.errors.set(errors.length > 0 ? [...new Set(errors)] : [message || 'تعذر إتمام العملية.']);
    this.focusErrors();
  }

  private handleHttpError(error: HttpErrorResponse, fallback: string): void {
    const envelope = error.error as { errors?: unknown; message?: unknown } | null;
    const serverErrors = Array.isArray(envelope?.errors)
      ? envelope.errors.filter((item): item is string => typeof item === 'string')
      : [];
    const message = typeof envelope?.message === 'string' ? envelope.message : '';
    this.errors.set([...new Set(serverErrors.length > 0 ? serverErrors : [message || fallback])]);
    this.focusErrors();
  }

  private clearFeedback(): void {
    this.errors.set([]);
    this.successMessage.set('');
    this.conflictMessage.set('');
  }

  private focusErrors(): void {
    setTimeout(() => this.errorSummary?.nativeElement.focus());
  }

  private updateEffectiveCutoff(): void {
    const { cutoff, graceMinutes } = this.form.controls.arrival.getRawValue();
    const [hours, minutes] = cutoff.split(':').map(Number);
    if (!Number.isInteger(hours) || !Number.isInteger(minutes)) {
      this.effectiveCutoff.set('—');
      return;
    }
    const total = (hours * 60 + minutes + Number(graceMinutes)) % (24 * 60);
    this.effectiveCutoff.set(`${String(Math.floor(total / 60)).padStart(2, '0')}:${String(total % 60).padStart(2, '0')}`);
  }

  private toTimeOnly(value: string): string {
    return /^\d{2}:\d{2}:\d{2}$/.test(value) ? value : `${value}:00`;
  }
}
