import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { debounceTime, distinctUntilChanged, finalize } from 'rxjs';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import { DispatchFactDto, PendingDispatchDto, isBehaviorFact } from '../../../core/models/phase5.models';
import { AuthService } from '../../../core/services/auth.service';
import { Phase5Service } from '../../../core/services/phase5.service';
import { ToastService } from '../../../core/services/toast.service';

type Decision = 'approve' | 'suppress';

@Component({
  selector: 'app-notification-approval-queue',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, ButtonModule, DialogModule, InputTextModule, InputTextareaModule, ProgressSpinnerModule, TableModule, TagModule],
  templateUrl: './notification-approval-queue.component.html',
  styleUrl: './notification-approval-queue.component.css'
})
export class NotificationApprovalQueueComponent {
  private readonly api = inject(Phase5Service);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);

  readonly items = signal<readonly PendingDispatchDto[]>([]);
  readonly totalRecords = signal(0);
  readonly pageNumber = signal(1);
  readonly pageSize = signal(10);
  readonly loading = signal(true);
  readonly errorMessage = signal('');
  readonly search = new FormControl('', { nonNullable: true });
  readonly dialogVisible = signal(false);
  readonly selected = signal<PendingDispatchDto | null>(null);
  readonly fact = signal<DispatchFactDto | null>(null);
  readonly factLoading = signal(false);
  readonly decision = signal<Decision>('approve');
  readonly deciding = signal(false);
  readonly conflict = signal(false);
  readonly suppressReason = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(2000)] });

  get canApprove(): boolean { return this.auth.hasRole('StudentAffairsOfficer') && this.auth.hasPermission('Notification.ApproveDispatch'); }
  get canSuppress(): boolean { return this.auth.hasRole('StudentAffairsOfficer') && this.auth.hasPermission('Notification.SuppressDispatch'); }

  constructor() {
    this.search.valueChanges.pipe(debounceTime(350), distinctUntilChanged()).subscribe(() => this.load(1));
    this.load();
  }

  load(page = this.pageNumber()): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.api.listPendingDispatch(page, this.pageSize(), this.search.value).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) {
          this.errorMessage.set(response.errors[0] ?? response.message ?? 'تعذر تحميل طابور الإشعارات.');
          return;
        }
        this.items.set(response.data.items.filter(item => this.isAllowedFactType(item.factType)));
        this.totalRecords.set(response.data.totalCount);
        this.pageNumber.set(response.data.page);
        this.pageSize.set(response.data.pageSize);
      },
      error: error => this.errorMessage.set(this.httpMessage(error, 'تعذر تحميل طابور الإشعارات.'))
    });
  }

  onPage(event: { first: number; rows: number }): void {
    this.pageSize.set(event.rows);
    this.load(Math.floor(event.first / event.rows) + 1);
  }

  openDecision(item: PendingDispatchDto, decision: Decision): void {
    if ((decision === 'approve' && !this.canApprove) || (decision === 'suppress' && !this.canSuppress)) return;
    this.selected.set(item);
    this.decision.set(decision);
    this.fact.set(null);
    this.conflict.set(false);
    this.suppressReason.reset('');
    this.dialogVisible.set(true);
    this.loadFact(item);
  }

  submit(): void {
    const item = this.selected();
    if (!item || this.deciding()) return;
    if (this.decision() === 'suppress') {
      this.suppressReason.markAsTouched();
      if (!this.suppressReason.value.trim()) return;
    }
    this.deciding.set(true);
    const request$ = this.decision() === 'approve'
      ? this.api.approveNotification(item.id, { rowVersion: item.rowVersion })
      : this.api.suppressNotification(item.id, { reason: this.suppressReason.value.trim(), rowVersion: item.rowVersion });
    request$.pipe(finalize(() => this.deciding.set(false))).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) {
          if (this.isConflict(`${response.message} ${response.errors.join(' ')}`)) this.resolveConflict(item);
          else this.toast.warn('لم يُحفظ القرار', response.errors[0] ?? response.message);
          return;
        }
        this.removeItem(response.data.id);
        this.dialogVisible.set(false);
        this.toast.success(this.decision() === 'approve' ? 'تم إرسال الإشعار لولي الأمر' : 'تم كتم الإشعار', 'بقيت الواقعة محفوظة في سجل الطالب.');
        this.load(this.pageNumber());
      },
      error: (error: HttpErrorResponse) => {
        if (error.status === 409 || error.status === 404) this.resolveConflict(item);
        else this.toast.error('تعذر حفظ القرار', this.httpMessage(error, 'حاول مرة أخرى.'));
      }
    });
  }

  factTypeLabel(type: string): string { return this.isBehaviorType(type) ? 'واقعة سلوكية' : 'قلق أكاديمي'; }
  factSeverity(item: PendingDispatchDto): 'warning' | 'info' { return this.isBehaviorType(item.factType) ? 'warning' : 'info'; }
  isBehavior(fact: DispatchFactDto): boolean { return isBehaviorFact(fact); }
  behaviorSeverity(fact: DispatchFactDto): string { return isBehaviorFact(fact) ? fact.severity : '—'; }
  waitingAge(value: string): string {
    const minutes = Math.max(0, Math.floor((Date.now() - new Date(value).getTime()) / 60_000));
    if (minutes < 1) return 'الآن';
    if (minutes < 60) return `منذ ${minutes} دقيقة`;
    const hours = Math.floor(minutes / 60);
    return hours < 24 ? `منذ ${hours} ساعة` : `منذ ${Math.floor(hours / 24)} يوم`;
  }
  formatDateTime(value: string): string {
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('ar-SA', { dateStyle: 'medium', timeStyle: 'short' }).format(date);
  }

  private loadFact(item: PendingDispatchDto): void {
    this.factLoading.set(true);
    const request$ = this.isBehaviorType(item.factType) ? this.api.getBehavior(item.factId) : this.api.getAcademicConcern(item.factId);
    request$.pipe(finalize(() => this.factLoading.set(false))).subscribe({
      next: response => {
        if (response.isSuccess && response.data) this.fact.set(response.data);
        else this.toast.warn('تعذر تحميل سياق الواقعة', response.errors[0] ?? response.message);
      },
      error: error => this.toast.error('تعذر تحميل سياق الواقعة', this.httpMessage(error, 'حاول تحديث الطابور.'))
    });
  }
  private resolveConflict(item: PendingDispatchDto): void {
    this.conflict.set(true);
    this.toast.warn('سبق حسم الإشعار أو تعديله', 'تم تحديث الطابور وسياق الواقعة لإظهار القرار الفائز.');
    this.loadFact(item);
    this.load(this.pageNumber());
  }
  private removeItem(id: number): void {
    this.items.update(items => items.filter(item => item.id !== id));
    this.totalRecords.update(total => Math.max(0, total - 1));
  }
  private isAllowedFactType(type: string): boolean { return this.isBehaviorType(type) || this.isAcademicType(type); }
  private isBehaviorType(type: string): boolean { return type.toLowerCase().includes('behavior'); }
  private isAcademicType(type: string): boolean { return type.toLowerCase().includes('academic'); }
  private isConflict(value: string): boolean { const text = value.toLowerCase(); return text.includes('rowversion') || text.includes('row version') || text.includes('concurrency') || text.includes('مستخدم آخر'); }
  private httpMessage(error: unknown, fallback: string): string { return extractHttpErrorMessage(error) ?? fallback; }
}
