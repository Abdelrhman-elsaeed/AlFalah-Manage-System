import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { GatePassStatus, ManagerGatePassAuditItemDto, TransitionDto, gatePassStatusLabel } from '../../../core/models/gate-pass.models';
import { GatePassService } from '../../../core/services/gate-pass.service';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-manager-gate-pass-audit',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './manager-gate-pass-audit.component.html',
  styleUrls: ['../manager-workspace.css', './manager-gate-pass-audit.component.css']
})
export class ManagerGatePassAuditComponent {
  private readonly api = inject(GatePassService);
  private readonly toast = inject(ToastService);

  readonly items = signal<readonly ManagerGatePassAuditItemDto[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly errorMessage = signal('');
  readonly page = signal(1);
  readonly total = signal(0);
  readonly selected = signal<ManagerGatePassAuditItemDto | null>(null);
  readonly action = signal<'cancel' | 'false-exit'>('cancel');
  readonly history = signal<readonly TransitionDto[]>([]);
  readonly status = new FormControl<GatePassStatus | ''>('', { nonNullable: true });
  readonly search = new FormControl('', { nonNullable: true });
  readonly reason = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(1000)] });
  readonly statuses: readonly GatePassStatus[] = ['Requested', 'Approved', 'SecurityAcknowledged', 'Exited', 'Cancelled', 'Rejected', 'Expired'];
  readonly pageSize = 12;
  readonly activeCount = computed(() => this.items().filter(item => this.canCancel(item.status)).length);
  readonly overdueCount = computed(() => this.items().filter(item => item.isOverdue).length);
  readonly exitedCount = computed(() => this.items().filter(item => item.status === 'Exited').length);

  constructor() { this.load(); }

  pages(): number { return Math.max(1, Math.ceil(this.total() / this.pageSize)); }
  label = gatePassStatusLabel;
  canCancel(status: GatePassStatus): boolean { return ['Requested', 'Approved', 'SecurityAcknowledged'].includes(status); }
  resetAndLoad(): void { this.page.set(1); this.selected.set(null); this.history.set([]); this.load(); }
  clearFilters(): void { this.status.setValue(''); this.search.setValue(''); this.resetAndLoad(); }
  move(delta: number): void { this.page.update(value => value + delta); this.load(); }

  load(): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.api.managerAudit({
      status: this.status.value || undefined,
      search: this.search.value.trim() || undefined,
      pageNumber: this.page(),
      pageSize: this.pageSize,
      sortBy: 'requestedExitAt',
      sortDirection: 'desc'
    }).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: response => {
        if (response.isSuccess && response.data) { this.items.set(response.data.items); this.total.set(response.data.totalCount); }
        else this.errorMessage.set(response.errors[0] ?? response.message);
      },
      error: (error: HttpErrorResponse) => this.errorMessage.set(error.status === 403 ? 'لا تملك صلاحية تدقيق الاستئذانات.' : 'تعذر تحميل سجل الاستئذانات. حاول مرة أخرى.')
    });
  }

  begin(item: ManagerGatePassAuditItemDto, action: 'cancel' | 'false-exit'): void {
    this.selected.set(item);
    this.action.set(action);
    this.reason.setValue('');
    this.history.set([]);
  }

  closeAction(): void { this.selected.set(null); this.reason.setValue(''); }

  showHistory(item: ManagerGatePassAuditItemDto): void {
    this.selected.set(null);
    this.api.history(item.id).subscribe({
      next: response => this.history.set(response.data?.transitions ?? []),
      error: () => this.toast.error('تعذر تحميل السجل', 'أعد المحاولة لاحقًا.')
    });
  }

  submit(): void {
    const item = this.selected();
    this.reason.markAsTouched();
    if (!item || this.reason.invalid || this.saving() || !confirm('هل تريد حفظ هذا الإجراء الاستثنائي في سجل التدقيق؟')) return;
    this.saving.set(true);
    const request = { reason: this.reason.value.trim(), rowVersion: item.rowVersion };
    const call = this.action() === 'cancel' ? this.api.cancel(item.id, request) : this.api.recordFalseExitIncident(item.id, request);
    call.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: response => {
        if (response.isSuccess) { this.toast.success('تم حفظ الإجراء', 'أضيف القرار إلى السجل المدقق.'); this.closeAction(); this.load(); }
        else this.toast.warn('لم يُحفظ الإجراء', response.errors[0] ?? response.message);
      },
      error: (error: HttpErrorResponse) => {
        if (error.status === 409) { this.toast.warn('تغير السجل', 'تم تحديث القائمة؛ راجع الحالة الجديدة.'); this.closeAction(); this.load(); }
        else this.toast.error('تعذر الحفظ', 'لم تتم إعادة الإرسال تلقائيًا.');
      }
    });
  }

  statusClass(status: GatePassStatus): string {
    if (status === 'Exited') return 'status-pill';
    if (status === 'Requested' || status === 'Approved' || status === 'SecurityAcknowledged') return 'status-pill status-pill--warning';
    if (status === 'Rejected' || status === 'Expired') return 'status-pill status-pill--danger';
    return 'status-pill status-pill--neutral';
  }
}
