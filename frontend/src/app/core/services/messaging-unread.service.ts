import { Injectable } from '@angular/core';
import { InboxSyncService } from './inbox-sync.service';

@Injectable({ providedIn: 'root' })
export class MessagingUnreadService {
  readonly count = this.inbox.unreadMessages;
  constructor(private readonly inbox: InboxSyncService) {}

  refresh(): void {
    this.inbox.refresh();
  }

  markThreadRead(unreadCount: number): void {
    if (unreadCount > 0) this.inbox.refresh();
  }

  reset(): void {
    this.inbox.reset();
  }
}
