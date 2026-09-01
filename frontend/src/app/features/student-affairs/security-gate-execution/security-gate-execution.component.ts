import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { PaginatorModule } from 'primeng/paginator';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TagModule } from 'primeng/tag';
import { Subject, catchError, filter, finalize, fromEvent, map, merge, of, switchMap, timer } from 'rxjs';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import {
  GatePassDto,
  PickupVerificationMethod,
  SecurityGatePassQueueItemDto,
  gatePassStatusLabel
} from '../../../core/models/gate-pass.models';
import { GatePassService } from '../../../core/services/gate-pass.service';
import { ToastService } from '../../../core/services/toast.service';

type QueueLoadResult =
  | { readonly ok: true; readonly items: readonly SecurityGatePassQueueItemDto[]; readonly total: number; readonly page: number; readonly pageSize: number }
  | { readonly ok: false; readonly error: string };

@Component({
  selector: 'app-security-gate-execution',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    ButtonModule,
    DialogModule,
    DropdownModule,
    InputTextareaModule,
    PaginatorModule,
    ProgressSpinnerModule,
    TagModule
  ],
  templateUrl: './security-gate-execution.component.html',
  styleUrl: './security-gate-execution.component.css'
})
export class SecurityGateExecutionComponent implements OnInit {
  private readonly api = inject(GatePassService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly manualRefresh = new Subject<void>();
  private readonly syncedAt = new Map<number, number>();

  readonly queue = signal<readonly SecurityGatePassQueueItemDto[]>([]);
  readonly totalRecords = signal(0);
  readonly pageNumber = signal(1);
  readonly pageSize = signal(12);
  readonly loading = signal(true);
  readonly errorMessage = signal('');
  readonly mutatingId = signal<number | null>(null);
  readonly exitTarget = signal<SecurityGatePassQueueItemDto | null>(null);
  readonly exitDialogVisible = signal(false);
  readonly executing = signal(false);
  readonly receipt = signal<GatePassDto | null>(null);
  readonly now = signal(Date.now());

  readonly verificationMethods: readonly { label: string; value: PickupVerificationMethod }[] = [
    { label: 'تحقق بصري', value: 'Visual' },
    { label: 'تحقق يدوي', value: 'Manual' },
    { label: 'لقطة شاشة ولي الأمر', value: 'GuardianScreenshot' }
  ];
  readonly exitForm = new FormGroup({
    verificationMethod: new FormControl<PickupVerificationMethod | null>(null, { validators: [Validators.required] }),
    verificationNote: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(1000)] }),
    gateNote: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(1000)] })
  });

  ngOnInit(): void {
    const focus$ = typeof window === 'undefined' ? of(undefined) : fromEvent(window, 'focus');
    const visibility$ = typeof document === 'undefined' ? of(undefined) : fromEvent(document, 'visibilitychange');
    merge(timer(0, 30_000), focus$, visibility$, this.manualRefresh).pipe(
      filter(() => typeof document === 'undefined' || document.visibilityState === 'visible'),
      switchMap(() => {
        this.loading.set(true);
        this.errorMessage.set('');
        return this.loadQueueRequest();
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(result => {
      this.loading.set(false);
      if (!result.ok) {
        this.errorMessage.set(result.error);
        return;
      }
      this.applyQueue(result.items, result.total, result.page, result.pageSize);
    });

    timer(0, 1_000).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.now.set(Date.now()));
  }

  refresh(): void {
    this.manualRefresh.next();
  }

  onPage(event: { page?: number; rows?: number }): void {
    this.pageNumber.set((event.page ?? 0) + 1);
    this.pageSize.set(event.rows ?? this.pageSize());
    this.refresh();
  }

  acknowledge(item: SecurityGatePassQueueItemDto): void {
    if (item.status !== 'Approved' || !this.isWindowActive(item) || this.mutatingId() !== null) return;
    this.mutatingId.set(item.id);
    this.api.acknowledgeSecurity(item.id, { rowVersion: item.rowVersion }).pipe(
      finalize(() => this.mutatingId.set(null))
    ).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) {
          const message = response.errors[0] ?? response.message;
          if (this.isConcurrency(message)) this.resolveLatest(item.id, 'conflict');
          else this.toast.warn('لم تُسجل المطابقة', message);
          return;
        }
        if (response.data.status !== 'SecurityAcknowledged') {
          this.resolveLatest(item.id, 'state');
          return;
        }
        this.updateOperationalItem(item.id, response.data);
        this.toast.success('تمت مطابقة بيانات المستلم', 'الاستئذان الآن جاهز لتسجيل الخروج الفعلي.');
      },
      error: (error: HttpErrorResponse) => this.handleMutationError(item.id, error, false)
    });
  }

  openExit(item: SecurityGatePassQueueItemDto): void {
    if (item.status !== 'SecurityAcknowledged' || !this.isWindowActive(item)) return;
    this.exitTarget.set(item);
    this.exitForm.reset({ verificationMethod: null, verificationNote: '', gateNote: '' });
    this.exitDialogVisible.set(true);
  }

  executeExit(): void {
    const item = this.exitTarget();
    this.exitForm.markAllAsTouched();
    const values = this.exitForm.getRawValue();
    if (!item || !values.verificationMethod || !values.verificationNote.trim() || this.executing()) return;
    if (!this.isWindowActive(item)) {
      this.toast.warn('انتهت نافذة الخروج', 'حدّث القائمة ولا تسجل حدث خروج خارج النافذة المعتمدة.');
      this.refresh();
      return;
    }

    const age = Date.now() - (this.syncedAt.get(item.id) ?? 0);
    if (age > 30_000) {
      this.refreshBeforeCommit(item.id);
      return;
    }

    this.executing.set(true);
    this.api.execute(item.id, {
      exitedAt: null,
      verificationMethod: values.verificationMethod,
      verificationNote: values.verificationNote.trim(),
      gateNote: values.gateNote.trim() || null,
      rowVersion: item.rowVersion
    }).pipe(finalize(() => this.executing.set(false))).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) {
          const message = response.errors[0] ?? response.message;
          if (this.isConcurrency(message)) this.resolveLatest(item.id, 'conflict');
          else this.toast.warn('لم يُسجل الخروج', message);
          return;
        }
        this.completeExit(response.data);
      },
      error: (error: HttpErrorResponse) => this.handleMutationError(item.id, error, true)
    });
  }

  isWindowActive(item: SecurityGatePassQueueItemDto): boolean {
    const current = this.now();
    return current >= new Date(item.approvedWindowStartsAt).getTime()
      && current <= new Date(item.approvedWindowEndsAt).getTime();
  }

  windowState(item: SecurityGatePassQueueItemDto): string {
    const current = this.now();
    const start = new Date(item.approvedWindowStartsAt).getTime();
    const end = new Date(item.approvedWindowEndsAt).getTime();
    if (current < start) return 'لم تبدأ النافذة بعد';
    if (current > end) return 'انتهت نافذة الخروج';
    return 'النافذة نشطة الآن';
  }

  windowSeverity(item: SecurityGatePassQueueItemDto): 'success' | 'warning' | 'danger' {
    const current = this.now();
    if (current > new Date(item.approvedWindowEndsAt).getTime()) return 'danger';
    return current < new Date(item.approvedWindowStartsAt).getTime() ? 'warning' : 'success';
  }

  countdown(item: SecurityGatePassQueueItemDto): string {
    const remaining = new Date(item.approvedWindowEndsAt).getTime() - this.now();
    if (remaining <= 0) return 'انتهى الوقت';
    const totalSeconds = Math.floor(remaining / 1000);
    const hours = Math.floor(totalSeconds / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    const seconds = totalSeconds % 60;
    return `${hours ? `${hours}:` : ''}${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
  }

  statusLabel(item: SecurityGatePassQueueItemDto): string {
    return gatePassStatusLabel(item.status);
  }

  formatDateTime(value: string | number | null): string {
    if (!value) return '—';
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? String(value) : new Intl.DateTimeFormat('ar-SA', {
      dateStyle: 'short',
      timeStyle: 'short'
    }).format(date);
  }

  initials(name: string): string {
    return name.split(/\s+/).filter(Boolean).slice(0, 2).map(part => part[0]).join('');
  }

  private loadQueueRequest() {
    return this.api.securityQueue({
      date: this.todayValue(),
      pageNumber: this.pageNumber(),
      pageSize: this.pageSize(),
      sortBy: 'approvedWindowStartsAt',
      sortDirection: 'asc'
    }).pipe(
      map(response => response.isSuccess && response.data
        ? ({ ok: true, items: response.data.items, total: response.data.totalCount, page: response.data.page, pageSize: response.data.pageSize } as const)
        : ({ ok: false, error: response.errors[0] ?? response.message ?? 'تعذر تحميل قائمة البوابة.' } as const)),
      catchError((error: HttpErrorResponse) => of({ ok: false, error: this.httpMessage(error, 'تعذر تحميل قائمة البوابة.') } as const))
    );
  }

  private applyQueue(items: readonly SecurityGatePassQueueItemDto[], total: number, page: number, pageSize: number): void {
    this.queue.set(items);
    this.totalRecords.set(total);
    this.pageNumber.set(page);
    this.pageSize.set(pageSize);
    const synced = Date.now();
    items.forEach(item => this.syncedAt.set(item.id, synced));
    const openTarget = this.exitTarget();
    if (openTarget) {
      const refreshedTarget = items.find(item => item.id === openTarget.id);
      if (refreshedTarget) this.exitTarget.set(refreshedTarget);
    }
  }

  private updateOperationalItem(id: number, detail: GatePassDto): void {
    if (detail.status !== 'Approved' && detail.status !== 'SecurityAcknowledged') {
      this.removeItem(id);
      return;
    }
    const operationalStatus: SecurityGatePassQueueItemDto['status'] = detail.status;
    this.queue.update(items => items.map(item => item.id === id
      ? { ...item, status: operationalStatus, rowVersion: detail.rowVersion }
      : item));
    const updated = this.queue().find(item => item.id === id) ?? null;
    this.exitTarget.set(updated);
    this.syncedAt.set(id, Date.now());
  }

  private refreshBeforeCommit(id: number): void {
    this.executing.set(true);
    this.api.getById(id).pipe(finalize(() => this.executing.set(false))).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) {
          this.toast.error('تعذر التحقق قبل الخروج', response.errors[0] ?? response.message);
          return;
        }
        if (response.data.status === 'Exited') {
          this.completeExit(response.data);
          return;
        }
        this.updateOperationalItem(id, response.data);
        if (response.data.status === 'SecurityAcknowledged') {
          this.toast.info('تم تحديث الاستئذان', 'راجع البيانات ثم اضغط «تم الخروج» مرة أخرى للتأكيد. لم نسجل خروجًا تلقائيًا.');
        } else {
          this.toast.warn('تغيرت حالة الاستئذان', `الحالة الحالية: ${gatePassStatusLabel(response.data.status)}.`);
        }
      },
      error: (error: HttpErrorResponse) => this.toast.error('تعذر التحقق قبل الخروج', this.httpMessage(error, 'حدّث القائمة وحاول مرة أخرى.'))
    });
  }

  private handleMutationError(id: number, error: HttpErrorResponse, physicalExit: boolean): void {
    if (error.status === 409) {
      this.resolveLatest(id, 'conflict');
      return;
    }
    if (error.status === 404) {
      this.removeItem(id);
      this.exitDialogVisible.set(false);
      this.toast.warn('لم يعد الاستئذان في القائمة', 'أزيل السجل القديم بعد التحديث.');
      return;
    }
    if (error.status === 0 && physicalExit) {
      this.toast.warn('تعذر تأكيد نتيجة تسجيل الخروج', 'سنتحقق من الخادم أولًا. لا تضغط تنفيذ مرة أخرى قبل ظهور الحالة.');
      this.resolveLatest(id, 'timeout');
      return;
    }
    this.toast.error(physicalExit ? 'تعذر تسجيل الخروج' : 'تعذر تسجيل المطابقة', this.httpMessage(error, 'حاول تحديث القائمة.'));
  }

  private resolveLatest(id: number, reason: 'conflict' | 'timeout' | 'state'): void {
    if (reason === 'conflict') this.toast.warn('تم تعديل الاستئذان من جهاز آخر', 'جارٍ جلب الحالة الأحدث. لن نكرر الإجراء تلقائيًا.');
    this.api.getById(id).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) return;
        if (response.data.status === 'Exited') {
          this.completeExit(response.data);
          return;
        }
        this.updateOperationalItem(id, response.data);
        this.toast.info('تم تحديث حالة الاستئذان', `الحالة الحالية: ${gatePassStatusLabel(response.data.status)}.`);
      },
      error: (error: HttpErrorResponse) => this.toast.error('تعذر جلب الحالة الأحدث', this.httpMessage(error, 'استخدم زر تحديث القائمة.'))
    });
  }

  private completeExit(detail: GatePassDto): void {
    if (detail.status !== 'Exited') {
      this.toast.warn('لم يكتمل تسجيل الخروج', `الحالة الحالية: ${gatePassStatusLabel(detail.status)}.`);
      return;
    }
    this.receipt.set(detail);
    this.exitDialogVisible.set(false);
    this.removeItem(detail.id);
    this.toast.success('تم تسجيل خروج الطالب ✅', `وقت الخروج المسجل من الخادم: ${this.formatDateTime(detail.exitedAt)}.`);
    timer(5_000).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      if (this.receipt()?.id === detail.id) this.receipt.set(null);
    });
  }

  private removeItem(id: number): void {
    this.queue.update(items => items.filter(item => item.id !== id));
    this.totalRecords.update(total => Math.max(0, total - 1));
    this.syncedAt.delete(id);
  }

  private todayValue(): string {
    const today = new Date();
    return `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`;
  }

  private isConcurrency(message: string): boolean {
    const normalized = message.toLocaleLowerCase('en');
    return normalized.includes('row version') || normalized.includes('rowversion') || normalized.includes('concurrency') || normalized.includes('modified by another');
  }

  private httpMessage(error: unknown, fallback: string): string {
    return extractHttpErrorMessage(error) ?? fallback;
  }
}
