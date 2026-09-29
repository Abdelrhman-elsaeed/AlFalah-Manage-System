import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
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
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly unreadOnly = signal(false);
  readonly unreadCount = signal(0);
  readonly notifications = signal<readonly GuardianNotificationDto[]>([]);
  readonly pageSize = 10;
  readonly page = signal(1);
  readonly total = signal(0);

  constructor() { this.load(); }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.api.listNotifications(this.page(), this.pageSize, this.unreadOnly() ? false : undefined).subscribe({
      next: response => {
        this.loading.set(false);
        if (!response.isSuccess) { this.error.set(response.errors[0] ?? 'تعذر تحميل الإشعارات.'); return; }
        this.notifications.set(response.data?.items ?? []);
        this.total.set(response.data?.totalCount ?? 0);
        this.refreshCount();
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
    this.api.markRead(item.id).subscribe({ next: () => { this.busy.set(false); this.load(); }, error: () => this.busy.set(false) });
  }

  markAllRead(): void {
    if (this.busy() || this.unreadCount() === 0) return;
    this.busy.set(true);
    this.api.markAllRead().subscribe({ next: () => { this.busy.set(false); this.load(); }, error: () => this.busy.set(false) });
  }

  private refreshCount(): void {
    this.api.unreadCount().subscribe(response => this.unreadCount.set(response.data ?? 0));
  }
}
