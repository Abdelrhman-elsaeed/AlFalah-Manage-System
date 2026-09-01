import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CalendarModule } from 'primeng/calendar';
import { DialogModule } from 'primeng/dialog';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { finalize, timer } from 'rxjs';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import { GatePassDto, gatePassStatusLabel } from '../../../core/models/gate-pass.models';
import { AuthService } from '../../../core/services/auth.service';
import { GatePassService } from '../../../core/services/gate-pass.service';
import { ToastService } from '../../../core/services/toast.service';

type DecisionMode = 'approve' | 'reject';

@Component({
  selector: 'app-officer-gate-pass-queue',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    ButtonModule,
    CalendarModule,
    DialogModule,
    InputTextareaModule,
    ProgressSpinnerModule,
    TableModule,
    TagModule
  ],
  templateUrl: './officer-gate-pass-queue.component.html',
  styleUrl: './officer-gate-pass-queue.component.css'
})
export class OfficerGatePassQueueComponent implements OnInit {
  private readonly api = inject(GatePassService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly queue = signal<readonly GatePassDto[]>([]);
  readonly totalRecords = signal(0);
  readonly pageNumber = signal(1);
  readonly pageSize = signal(10);
  readonly dateFilter = signal<Date | null>(new Date());
  readonly loading = signal(true);
  readonly refreshing = signal(false);
  readonly errorMessage = signal('');
  readonly selected = signal<GatePassDto | null>(null);
  readonly decisionMode = signal<DecisionMode>('approve');
  readonly dialogVisible = signal(false);
  readonly detailLoading = signal(false);
  readonly deciding = signal(false);
  readonly autoRefreshEnabled = signal(true);

  readonly approveForm = new FormGroup({
    windowStartsAt: new FormControl<Date | null>(null, { validators: [Validators.required] }),
    windowEndsAt: new FormControl<Date | null>(null, { validators: [Validators.required] }),
    approvalNote: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(1000)] })
  });
  readonly rejectReason = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(1000)] });

  get canApprove(): boolean {
    return this.auth.hasRole('StudentAffairsOfficer') && this.auth.hasPermission('GatePass.Approve');
  }

  get canReject(): boolean {
    return this.auth.hasRole('StudentAffairsOfficer') && this.auth.hasPermission('GatePass.Reject');
  }

  ngOnInit(): void {
    this.loadQueue(1);
    timer(60_000, 60_000).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      if (this.autoRefreshEnabled() && !this.dialogVisible() && (typeof document === 'undefined' || document.visibilityState === 'visible')) {
        this.loadQueue(this.pageNumber(), true);
      }
    });
  }

  loadQueue(page = this.pageNumber(), quiet = false): void {
    if (quiet) this.refreshing.set(true);
    else this.loading.set(true);
    this.errorMessage.set('');
    this.api.list({
      status: 'Requested',
      date: this.dateValue(this.dateFilter()),
      pageNumber: page,
      pageSize: this.pageSize(),
      sortBy: 'requestedExitAt',
      sortDirection: 'asc'
    }).pipe(
      finalize(() => {
        this.loading.set(false);
        this.refreshing.set(false);
      })
    ).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) {
          this.errorMessage.set(response.errors[0] ?? response.message ?? 'تعذر تحميل قائمة الاستئذانات.');
          return;
        }
        this.queue.set(response.data.items);
        this.totalRecords.set(response.data.totalCount);
        this.pageNumber.set(response.data.page);
        this.pageSize.set(response.data.pageSize);
      },
      error: error => this.errorMessage.set(this.httpMessage(error, 'تعذر تحميل قائمة الاستئذانات.'))
    });
  }

  applyDateFilter(value: Date | null): void {
    this.dateFilter.set(value);
    this.loadQueue(1);
  }

  clearDateFilter(): void {
    this.dateFilter.set(null);
    this.loadQueue(1);
  }

  onPage(event: { first: number; rows: number }): void {
    this.pageSize.set(event.rows);
    this.loadQueue(Math.floor(event.first / event.rows) + 1);
  }

  openDecision(row: GatePassDto, mode: DecisionMode): void {
    if ((mode === 'approve' && !this.canApprove) || (mode === 'reject' && !this.canReject)) return;
    this.decisionMode.set(mode);
    this.selected.set(row);
    this.dialogVisible.set(true);
    this.detailLoading.set(true);
    this.rejectReason.reset('');
    this.api.getById(row.id).pipe(finalize(() => this.detailLoading.set(false))).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) {
          this.toast.error('تعذر فتح المراجعة', response.errors[0] ?? response.message);
          return;
        }
        this.selected.set(response.data);
        if (response.data.status !== 'Requested') {
          this.removeFromQueue(response.data.id);
          this.toast.info('تغيرت حالة الاستئذان', `الحالة الحالية: ${gatePassStatusLabel(response.data.status)}.`);
          return;
        }
        this.initializeApprovalWindow(response.data);
      },
      error: (error: HttpErrorResponse) => this.handleDetailError(row.id, error)
    });
  }

  submitApprove(): void {
    this.approveForm.markAllAsTouched();
    if (this.approveForm.invalid || this.approvalValidationMessage() || this.deciding()) return;
    this.submitWithLatest('approve');
  }

  submitReject(): void {
    this.rejectReason.markAsTouched();
    if (!this.rejectReason.value.trim() || this.deciding()) return;
    this.submitWithLatest('reject');
  }

  approvalValidationMessage(): string {
    const detail = this.selected();
    const start = this.approveForm.controls.windowStartsAt.value;
    const end = this.approveForm.controls.windowEndsAt.value;
    if (!detail || !start || !end) return '';
    if (start.getTime() >= end.getTime()) return 'بداية النافذة يجب أن تسبق نهايتها.';
    if (end.getTime() <= Date.now()) return 'نهاية نافذة الخروج يجب أن تكون في المستقبل.';
    const requested = new Date(detail.requestedExitAt).getTime();
    if (requested < start.getTime() || requested > end.getTime()) return 'يجب أن يقع وقت الخروج المطلوب داخل النافذة المعتمدة.';
    return '';
  }

  urgencyLabel(item: GatePassDto): string {
    const difference = new Date(item.requestedExitAt).getTime() - Date.now();
    if (difference < 0) return 'متأخر';
    if (difference <= 30 * 60 * 1000) return 'عاجل';
    return 'قادم';
  }

  urgencySeverity(item: GatePassDto): 'info' | 'warning' | 'danger' {
    const label = this.urgencyLabel(item);
    return label === 'متأخر' ? 'danger' : label === 'عاجل' ? 'warning' : 'info';
  }

  requestedAge(item: GatePassDto): string {
    const minutes = Math.max(0, Math.floor((Date.now() - new Date(item.requestedAt).getTime()) / 60_000));
    if (minutes < 1) return 'الآن';
    if (minutes < 60) return `منذ ${minutes} د`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `منذ ${hours} س`;
    return `منذ ${Math.floor(hours / 24)} يوم`;
  }

  formatDateTime(value: string | null): string {
    if (!value) return '—';
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('ar-SA', {
      dateStyle: 'medium',
      timeStyle: 'short'
    }).format(date);
  }

  gatePassStatusLabel(status: GatePassDto['status']): string {
    return gatePassStatusLabel(status);
  }

  private submitWithLatest(mode: DecisionMode): void {
    const draftTarget = this.selected();
    if (!draftTarget) return;
    this.deciding.set(true);
    this.api.getById(draftTarget.id).subscribe({
      next: detailResponse => {
        if (!detailResponse.isSuccess || !detailResponse.data) {
          this.deciding.set(false);
          this.toast.error('تعذر تحديث الاستئذان قبل القرار', detailResponse.errors[0] ?? detailResponse.message);
          return;
        }
        const latest = detailResponse.data;
        this.selected.set(latest);
        if (latest.status !== 'Requested') {
          this.deciding.set(false);
          this.removeFromQueue(latest.id);
          this.toast.warn('سبق حسم الاستئذان', `الحالة الفائزة: ${gatePassStatusLabel(latest.status)}. راجعها قبل إغلاق النافذة.`);
          return;
        }
        if (mode === 'approve') this.sendApproval(latest);
        else this.sendRejection(latest);
      },
      error: (error: HttpErrorResponse) => {
        this.deciding.set(false);
        this.handleMutationError(draftTarget.id, error);
      }
    });
  }

  private sendApproval(latest: GatePassDto): void {
    const values = this.approveForm.getRawValue();
    if (!values.windowStartsAt || !values.windowEndsAt) {
      this.deciding.set(false);
      return;
    }
    this.api.approve(latest.id, {
      windowStartsAt: values.windowStartsAt.toISOString(),
      windowEndsAt: values.windowEndsAt.toISOString(),
      approvalNote: values.approvalNote.trim() || null,
      rowVersion: latest.rowVersion
    }).pipe(finalize(() => this.deciding.set(false))).subscribe({
      next: response => this.handleDecisionResponse(response, 'تم اعتماد الاستئذان'),
      error: (error: HttpErrorResponse) => this.handleMutationError(latest.id, error)
    });
  }

  private sendRejection(latest: GatePassDto): void {
    this.api.reject(latest.id, {
      reason: this.rejectReason.value.trim(),
      rowVersion: latest.rowVersion
    }).pipe(finalize(() => this.deciding.set(false))).subscribe({
      next: response => this.handleDecisionResponse(response, 'تم رفض الاستئذان'),
      error: (error: HttpErrorResponse) => this.handleMutationError(latest.id, error)
    });
  }

  private handleDecisionResponse(
    response: { readonly isSuccess: boolean; readonly data?: GatePassDto | null; readonly message: string; readonly errors: readonly string[] },
    successSummary: string
  ): void {
    if (!response.isSuccess || !response.data) {
      const message = response.errors[0] ?? response.message;
      if (this.isConcurrency(message)) {
        const id = this.selected()?.id;
        if (id) this.resolveConflict(id);
      } else {
        this.toast.warn('لم يُحفظ القرار', message);
      }
      return;
    }
    this.selected.set(response.data);
    this.removeFromQueue(response.data.id);
    this.dialogVisible.set(false);
    this.toast.success(successSummary, 'تم تحديث قائمة الطلبات المعلقة.');
  }

  private handleMutationError(id: number, error: HttpErrorResponse): void {
    if (error.status === 409) {
      this.resolveConflict(id);
      return;
    }
    if (error.status === 404) {
      this.removeFromQueue(id);
      this.toast.warn('لم يعد الاستئذان متاحًا', 'أزيل الصف القديم من القائمة.');
      return;
    }
    this.toast.error('تعذر حفظ القرار', this.httpMessage(error, 'حاول مرة أخرى.'));
  }

  private resolveConflict(id: number): void {
    this.toast.warn('عدّل موظف آخر هذا الاستئذان', 'احتفظنا بمسودتك. جارٍ جلب الحالة الفائزة؛ راجعها ولا يُعاد إرسال القرار تلقائيًا.');
    this.api.getById(id).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) return;
        this.selected.set(response.data);
        this.queue.update(items => response.data?.status === 'Requested'
          ? items.map(item => item.id === id ? response.data as GatePassDto : item)
          : items.filter(item => item.id !== id));
        this.toast.info('تم تحديث الحالة', `الحالة الحالية: ${gatePassStatusLabel(response.data.status)}.`);
      },
      error: (error: HttpErrorResponse) => this.handleDetailError(id, error)
    });
  }

  private handleDetailError(id: number, error: HttpErrorResponse): void {
    if (error.status === 404) {
      this.removeFromQueue(id);
      this.dialogVisible.set(false);
      this.toast.warn('لم يعد الاستئذان موجودًا', 'أزيل الصف القديم من القائمة.');
      return;
    }
    this.toast.error('تعذر تحديث تفاصيل الاستئذان', this.httpMessage(error, 'حاول تحديث القائمة.'));
  }

  private initializeApprovalWindow(item: GatePassDto): void {
    const requested = new Date(item.requestedExitAt);
    this.approveForm.reset({
      windowStartsAt: new Date(requested.getTime() - 15 * 60_000),
      windowEndsAt: new Date(requested.getTime() + 15 * 60_000),
      approvalNote: ''
    });
  }

  private removeFromQueue(id: number): void {
    this.queue.update(items => items.filter(item => item.id !== id));
    this.totalRecords.update(total => Math.max(0, total - 1));
  }

  private dateValue(value: Date | null): string | undefined {
    if (!value) return undefined;
    const year = value.getFullYear();
    const month = String(value.getMonth() + 1).padStart(2, '0');
    const day = String(value.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private isConcurrency(message: string): boolean {
    const normalized = message.toLocaleLowerCase('en');
    return normalized.includes('row version') || normalized.includes('rowversion') || normalized.includes('concurrency') || normalized.includes('modified by another');
  }

  private httpMessage(error: unknown, fallback: string): string {
    return extractHttpErrorMessage(error) ?? fallback;
  }
}
