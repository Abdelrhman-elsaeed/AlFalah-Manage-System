import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ConversationThreadStatus, ConversationThreadType, MessagingAuditThreadDto } from '../../../core/models/phase5.models';
import { Phase5Service } from '../../../core/services/phase5.service';

@Component({
  selector: 'app-manager-messaging-audit',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './manager-messaging-audit.component.html',
  styleUrls: ['../manager-workspace.css', './manager-messaging-audit.component.css']
})
export class ManagerMessagingAuditComponent {
  private readonly api = inject(Phase5Service);

  readonly rows = signal<readonly MessagingAuditThreadDto[]>([]);
  readonly loading = signal(true);
  readonly errorMessage = signal('');
  readonly page = signal(1);
  readonly total = signal(0);
  readonly pageSize = 20;
  readonly threadType = new FormControl<ConversationThreadType | ''>('', { nonNullable: true });
  readonly status = new FormControl<ConversationThreadStatus | ''>('', { nonNullable: true });
  readonly threadTypes: readonly ConversationThreadType[] = ['GuardianTeacher', 'GuardianStudentAffairs', 'GuardianSocialWorker'];
  readonly statuses: readonly ConversationThreadStatus[] = ['Open', 'Closed', 'Archived'];
  readonly messageCount = computed(() => this.rows().reduce((sum, row) => sum + row.messageCount, 0));
  readonly pendingCount = computed(() => this.rows().reduce((sum, row) => sum + row.pendingDeliveryCount, 0));
  readonly failedCount = computed(() => this.rows().reduce((sum, row) => sum + row.failedCount, 0));

  constructor() { this.load(); }

  pages(): number { return Math.max(1, Math.ceil(this.total() / this.pageSize)); }
  resetAndLoad(): void { this.page.set(1); this.load(); }
  clearFilters(): void { this.threadType.setValue(''); this.status.setValue(''); this.resetAndLoad(); }

  load(): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.api.getMessagingAudit({
      threadType: this.threadType.value || undefined,
      status: this.status.value || undefined,
      pageNumber: this.page(),
      pageSize: this.pageSize,
      sortDirection: 'desc'
    }).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: response => {
        if (response.isSuccess && response.data) { this.rows.set(response.data.items); this.total.set(response.data.totalCount); }
        else this.errorMessage.set(response.errors[0] ?? response.message);
      },
      error: (error: HttpErrorResponse) => this.errorMessage.set(error.status === 403 ? 'لا تملك صلاحية تدقيق المراسلات.' : 'تعذر تحميل سجل التدقيق. حاول مرة أخرى.')
    });
  }

  previous(): void { if (this.page() > 1) { this.page.update(value => value - 1); this.load(); } }
  next(): void { if (this.page() < this.pages()) { this.page.update(value => value + 1); this.load(); } }

  typeLabel(type: ConversationThreadType): string {
    return ({ GuardianTeacher: 'ولي أمر ومعلم', GuardianStudentAffairs: 'ولي أمر وشؤون الطلاب', GuardianSocialWorker: 'ولي أمر وأخصائي اجتماعي' })[type];
  }

  statusLabel(status: ConversationThreadStatus): string {
    return ({ Open: 'مفتوح', Closed: 'مغلق', Archived: 'مؤرشف' })[status];
  }

  statusClass(status: ConversationThreadStatus): string {
    if (status === 'Open') return 'status-pill';
    return status === 'Closed' ? 'status-pill status-pill--neutral' : 'status-pill status-pill--warning';
  }
}
