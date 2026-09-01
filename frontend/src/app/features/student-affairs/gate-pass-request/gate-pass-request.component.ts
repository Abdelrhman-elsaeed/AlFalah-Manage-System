import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CalendarModule } from 'primeng/calendar';
import { CardModule } from 'primeng/card';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { InputTextModule } from 'primeng/inputtext';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { finalize, forkJoin } from 'rxjs';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import {
  ACTIVE_GATE_PASS_STATUSES,
  CreateGatePassRequestDto,
  GatePassDto,
  GatePassStatus,
  gatePassStatusLabel
} from '../../../core/models/gate-pass.models';
import { GuardianStudentDto } from '../../../core/models/student-affairs-dashboard.models';
import { AuthService } from '../../../core/services/auth.service';
import { GatePassService } from '../../../core/services/gate-pass.service';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-gate-pass-request',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    ButtonModule,
    CalendarModule,
    CardModule,
    DialogModule,
    DropdownModule,
    InputTextareaModule,
    InputTextModule,
    ProgressSpinnerModule,
    TableModule,
    TagModule
  ],
  templateUrl: './gate-pass-request.component.html',
  styleUrl: './gate-pass-request.component.css'
})
export class GatePassRequestComponent implements OnInit {
  private readonly api = inject(GatePassService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);
  private pendingIdempotencyKey: string | null = null;

  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly students = signal<readonly GuardianStudentDto[]>([]);
  readonly requests = signal<readonly GatePassDto[]>([]);
  readonly advisoryRequests = signal<readonly GatePassDto[]>([]);
  readonly totalRecords = signal(0);
  readonly pageSize = signal(10);
  readonly pageNumber = signal(1);
  readonly overlapWarning = signal<GatePassDto | null>(null);
  readonly cancelTarget = signal<GatePassDto | null>(null);
  readonly cancelVisible = signal(false);
  readonly cancelling = signal(false);
  readonly minExitDate = new Date();

  readonly form = new FormGroup({
    studentId: new FormControl<number | null>(null, { validators: [Validators.required] }),
    desiredExitTime: new FormControl<Date | null>(null, { validators: [Validators.required] }),
    reason: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(1000)] }),
    pickupPersonName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(200)] }),
    pickupRelationship: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(100)] }),
    pickupIdentityHint: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(200)] })
  });
  readonly cancelReason = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(500)] });

  get canCancelOwn(): boolean {
    return this.auth.hasPermission('GatePass.CancelOwn');
  }

  ngOnInit(): void {
    this.form.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.pendingIdempotencyKey = null;
      this.updateOverlapWarning();
    });
    this.loadInitial();
  }

  loadInitial(): void {
    this.loading.set(true);
    forkJoin({
      students: this.api.getGuardianStudents(),
      passes: this.api.getMine(this.mineQuery(1, this.pageSize())),
      advisory: this.api.getMine(this.mineQuery(1, 100))
    }).pipe(
      finalize(() => this.loading.set(false)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: ({ students, passes, advisory }) => {
        if (!students.isSuccess || !students.data) {
          this.toast.error('تعذر تحميل الطلاب', students.errors[0] ?? students.message);
        } else {
          const eligible = students.data.filter(link => link.canRequestGatePass && link.student.isActive);
          this.students.set(eligible);
          if (eligible.length === 1) this.form.controls.studentId.setValue(eligible[0].student.id);
        }
        if (!passes.isSuccess || !passes.data) {
          this.toast.error('تعذر تحميل الاستئذانات', passes.errors[0] ?? passes.message);
        } else {
          this.applyPage(passes.data.items, passes.data.totalCount, passes.data.page, passes.data.pageSize);
        }
        if (advisory.isSuccess && advisory.data) {
          this.advisoryRequests.set(advisory.data.items.filter(item => ACTIVE_GATE_PASS_STATUSES.includes(item.status)));
          this.updateOverlapWarning();
        }
      },
      error: error => this.toast.error('تعذر تحميل صفحة الاستئذان', this.errorMessage(error))
    });
  }

  loadMine(page = this.pageNumber()): void {
    this.api.getMine(this.mineQuery(page, this.pageSize())).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) {
          this.toast.error('تعذر تحديث الاستئذانات', response.errors[0] ?? response.message);
          return;
        }
        this.applyPage(response.data.items, response.data.totalCount, response.data.page, response.data.pageSize);
        this.loadAdvisoryRequests();
      },
      error: error => this.toast.error('تعذر تحديث الاستئذانات', this.errorMessage(error))
    });
  }

  onPage(event: { first: number; rows: number }): void {
    this.pageSize.set(event.rows);
    this.loadMine(Math.floor(event.first / event.rows) + 1);
  }

  submit(): void {
    this.form.markAllAsTouched();
    const raw = this.form.getRawValue();
    if (this.form.invalid || !raw.studentId || !raw.desiredExitTime || this.submitting()) return;
    if (!raw.reason.trim() || !raw.pickupPersonName.trim()) {
      this.toast.warn('أكمل البيانات المطلوبة', 'سبب الاستئذان واسم الشخص المستلم لا يمكن أن يكونا فارغين.');
      return;
    }
    if (raw.desiredExitTime.getTime() <= Date.now()) {
      this.toast.warn('راجع وقت الخروج', 'يجب أن يكون وقت الخروج المطلوب في المستقبل.');
      return;
    }
    if (raw.desiredExitTime.getDay() === 5) {
      this.toast.warn('اليوم غير متاح', 'يوم الجمعة ليس يومًا دراسيًا. اختر وقتًا من السبت إلى الخميس.');
      return;
    }

    const request: CreateGatePassRequestDto = {
      studentId: raw.studentId,
      desiredExitTime: raw.desiredExitTime.toISOString(),
      reason: raw.reason.trim(),
      pickupPersonName: raw.pickupPersonName.trim(),
      pickupRelationship: raw.pickupRelationship.trim() || null,
      pickupIdentityHint: raw.pickupIdentityHint.trim() || null
    };
    this.pendingIdempotencyKey ??= this.api.createIdempotencyKey();
    this.submitting.set(true);
    this.api.create(request, this.pendingIdempotencyKey).pipe(
      finalize(() => this.submitting.set(false))
    ).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) {
          const message = response.errors[0] ?? response.message;
          this.pendingIdempotencyKey = null;
          this.showCreateFailure(message);
          this.loadMine(1);
          return;
        }
        const receipt = response.data;
        this.pendingIdempotencyKey = null;
        this.requests.update(items => [receipt, ...items.filter(item => item.id !== receipt.id)].slice(0, this.pageSize()));
        this.totalRecords.update(total => total + (this.requests().some(item => item.id === receipt.id) ? 0 : 1));
        this.toast.success('تم إرسال طلب الاستئذان', 'حالة الطلب الآن: بانتظار المراجعة.');
        this.form.patchValue({
          desiredExitTime: null,
          reason: '',
          pickupPersonName: '',
          pickupRelationship: '',
          pickupIdentityHint: ''
        });
        this.form.markAsPristine();
        this.loadMine(1);
      },
      error: (error: HttpErrorResponse) => {
        if (error.status === 0) {
          this.toast.warn('تعذر تأكيد الإرسال', 'سنتحقق من طلباتك قبل إعادة المحاولة. لا تنشئ طلبًا جديدًا الآن.');
          this.loadMine(1);
          return;
        }
        this.pendingIdempotencyKey = null;
        this.showCreateFailure(this.errorMessage(error));
        if (error.status === 400) this.loadMine(1);
      }
    });
  }

  openCancel(item: GatePassDto): void {
    if (item.status !== 'Requested' || !this.canCancelOwn) return;
    this.cancelTarget.set(item);
    this.cancelReason.reset('');
    this.cancelVisible.set(true);
  }

  cancel(): void {
    const target = this.cancelTarget();
    const reason = this.cancelReason.value.trim();
    if (!target || !reason || this.cancelling()) {
      this.cancelReason.markAsTouched();
      return;
    }
    this.cancelling.set(true);
    this.api.cancel(target.id, { reason, rowVersion: target.rowVersion }).pipe(
      finalize(() => this.cancelling.set(false))
    ).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) {
          this.toast.warn('لم يُلغَ الطلب', response.errors[0] ?? response.message);
          this.loadMine();
          return;
        }
        this.replaceRequest(response.data);
        this.cancelVisible.set(false);
        this.toast.success('تم إلغاء الاستئذان');
      },
      error: (error: HttpErrorResponse) => {
        if (error.status === 409) {
          this.toast.warn('تم تعديل الطلب', 'حدّثنا القائمة. راجع الحالة الجديدة قبل اتخاذ قرار آخر.');
          this.loadMine();
          return;
        }
        this.toast.error('تعذر إلغاء الاستئذان', this.errorMessage(error));
      }
    });
  }

  statusLabel(status: GatePassStatus): string {
    return gatePassStatusLabel(status);
  }

  statusSeverity(status: GatePassStatus): 'success' | 'info' | 'warning' | 'danger' | 'secondary' {
    if (status === 'Exited') return 'success';
    if (status === 'Approved' || status === 'SecurityAcknowledged') return 'info';
    if (status === 'Requested') return 'warning';
    if (status === 'Rejected' || status === 'Expired') return 'danger';
    return 'secondary';
  }

  formatDateTime(value: string | null): string {
    if (!value) return '—';
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('ar-SA', {
      dateStyle: 'medium',
      timeStyle: 'short'
    }).format(date);
  }

  private updateOverlapWarning(): void {
    const studentId = this.form.controls.studentId.value;
    const desired = this.form.controls.desiredExitTime.value?.getTime();
    if (!studentId || desired === undefined) {
      this.overlapWarning.set(null);
      return;
    }
    const overlap = this.advisoryRequests().find(item =>
      item.student.id === studentId
      && ACTIVE_GATE_PASS_STATUSES.includes(item.status)
      && Math.abs(new Date(item.requestedExitAt).getTime() - desired) <= 30 * 60 * 1000
    ) ?? null;
    this.overlapWarning.set(overlap);
  }

  private applyPage(items: readonly GatePassDto[], total: number, page: number, pageSize: number): void {
    this.requests.set(items);
    this.totalRecords.set(total);
    this.pageNumber.set(page);
    this.pageSize.set(pageSize);
    this.updateOverlapWarning();
  }

  private replaceRequest(updated: GatePassDto): void {
    this.requests.update(items => items.map(item => item.id === updated.id ? updated : item));
    this.advisoryRequests.update(items => ACTIVE_GATE_PASS_STATUSES.includes(updated.status)
      ? items.map(item => item.id === updated.id ? updated : item)
      : items.filter(item => item.id !== updated.id));
    this.cancelTarget.set(updated);
  }

  private loadAdvisoryRequests(): void {
    this.api.getMine(this.mineQuery(1, 100)).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) return;
        this.advisoryRequests.set(response.data.items.filter(item => ACTIVE_GATE_PASS_STATUSES.includes(item.status)));
        this.updateOverlapWarning();
      },
      error: () => undefined
    });
  }

  private mineQuery(pageNumber: number, pageSize: number) {
    return { pageNumber, pageSize, sortBy: 'requestedAt', sortDirection: 'desc' as const };
  }

  private showCreateFailure(message: string): void {
    if (this.isOverlapFailure(message)) {
      this.toast.warn('يوجد استئذان قريب من هذا الوقت', 'لا يمكن إنشاء استئذان نشط آخر للطالب خلال 30 دقيقة قبل الوقت أو بعده.');
      return;
    }
    this.toast.error('تعذر إرسال طلب الاستئذان', message || 'راجع البيانات وحاول مرة أخرى.');
  }

  private isOverlapFailure(message: string): boolean {
    const normalized = message.toLocaleLowerCase('en');
    return normalized.includes('overlap') || normalized.includes('active gate pass') || normalized.includes('تداخل');
  }

  private errorMessage(error: unknown): string {
    return extractHttpErrorMessage(error) ?? 'تعذر الاتصال بالخدمة. حاول مرة أخرى.';
  }
}
