import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { InboxSyncService } from '../../../core/services/inbox-sync.service';
import { ButtonModule } from 'primeng/button';
import { PaginatorModule } from 'primeng/paginator';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TagModule } from 'primeng/tag';
import { GuardianNotificationDto } from '../../../core/models/guardian-self-service.models';
import { GuardianSelfServiceService } from '../../../core/services/guardian-self-service.service';

@Component({
  selector: 'app-guardian-notifications',
  standalone: true,
  imports: [CommonModule, ButtonModule, PaginatorModule, ProgressSpinnerModule, TagModule],
  templateUrl: './guardian-notifications.component.html',
  styleUrl: './guardian-notifications.component.css'
})
export class GuardianNotificationsComponent {
  private readonly api = inject(GuardianSelfServiceService);
  private readonly inboxSync = inject(InboxSyncService);
  private readonly destroyRef = inject(DestroyRef);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly unreadOnly = signal(false);
  readonly unreadCount = this.inboxSync.unreadNotifications;
  readonly notifications = signal<readonly GuardianNotificationDto[]>([]);
  readonly pageSize = 10;
  readonly page = signal(1);
  readonly total = signal(0);
  private loadRequest = 0;

  constructor() {
    this.load();
    this.inboxSync.changes$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.load());
  }

  load(): void {
    const request = ++this.loadRequest;
    this.loading.set(true);
    this.error.set('');
    this.api.listNotifications(this.page(), this.pageSize, this.unreadOnly() ? false : undefined)
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: response => {
        if (request !== this.loadRequest) return;
        this.loading.set(false);
        if (!response.isSuccess) { this.error.set(response.errors[0] ?? 'تعذر تحميل الإشعارات.'); return; }
        this.notifications.set(response.data?.items ?? []);
        this.total.set(response.data?.totalCount ?? 0);
      },
      error: () => { this.loading.set(false); this.error.set('تعذر تحميل الإشعارات.'); }
    });
  }

  toggleUnread(): void { this.unreadOnly.update(value => !value); this.page.set(1); this.load(); }

  changePage(event: { first?: number }): void {
    this.page.set(Math.floor((event.first ?? 0) / this.pageSize) + 1);
    this.load();
  }

  markRead(item: GuardianNotificationDto): void {
    if (item.readAt || this.busy()) return;
    this.busy.set(true);
    this.api.markRead(item.id).subscribe({ next: () => { this.busy.set(false); this.load(); this.inboxSync.refresh(); }, error: () => this.busy.set(false) });
  }

  markAllRead(): void {
    if (this.busy() || this.unreadCount() === 0) return;
    this.busy.set(true);
    this.api.markAllRead().subscribe({ next: () => { this.busy.set(false); this.load(); this.inboxSync.refresh(); }, error: () => this.busy.set(false) });
  }

}
