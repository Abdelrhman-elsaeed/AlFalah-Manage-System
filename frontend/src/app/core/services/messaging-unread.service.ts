import { Injectable, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { Phase5Service } from './phase5.service';

@Injectable({ providedIn: 'root' })
export class MessagingUnreadService {
  readonly count = signal(0);
  private refreshing = false;

  constructor(private readonly api: Phase5Service) {}

  refresh(): void {
    if (this.refreshing) return;
    this.refreshing = true;
    this.api.listConversations({ pageNumber: 1, pageSize: 100, isUnread: true }).pipe(
      finalize(() => { this.refreshing = false; })
    ).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) return;
        this.count.set(response.data.items.reduce((total, conversation) => total + conversation.unreadCount, 0));
      },
      error: () => undefined
    });
  }

  markThreadRead(unreadCount: number): void {
    if (unreadCount <= 0) return;
    this.count.update(current => Math.max(0, current - unreadCount));
  }

  reset(): void {
    this.count.set(0);
  }
}
