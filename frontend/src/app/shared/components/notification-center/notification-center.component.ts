import { CommonModule } from '@angular/common';
import { Component, ElementRef, HostListener, Input, ViewChild, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { CenterItem, NOTIFICATION_CATEGORIES, NotificationCenterService } from '../../../core/services/notification-center.service';
import { InboxSyncService } from '../../../core/services/inbox-sync.service';
import { WebPushService } from '../../../core/services/web-push.service';

@Component({
  selector: 'app-notification-center', standalone: true, imports: [CommonModule, FormsModule, RouterLink],
  host: { '[attr.data-open]': 'open()' },
  templateUrl: './notification-center.component.html', styleUrl: './notification-center.component.css'
})
export class NotificationCenterComponent {
  @Input() history = false;
  @ViewChild('bell') bell?: ElementRef<HTMLButtonElement>;
  @ViewChild('panel') panel?: ElementRef<HTMLElement>;
  readonly center = inject(NotificationCenterService);
  readonly sync = inject(InboxSyncService);
  readonly push = inject(WebPushService);
  private readonly router = inject(Router);
  readonly open = signal(false);
  readonly loading = signal(false);
  readonly reading = signal(false);
  readonly error = signal('');
  readonly entries = signal<CenterItem[]>([]);
  readonly categories = NOTIFICATION_CATEGORIES;
  category = ''; unread = false;
  next: string | null = null;
  private request = 0;
  private contextVersion = -1;
  constructor() {
    effect(() => {
      const context = this.center.contextVersion();
      if (context !== this.contextVersion) {
        this.contextVersion = context; this.request++;
        this.entries.set([]); this.next = null;
      }
      this.center.revision();
      if (this.open() || this.history) void this.load(false, true);
      else { this.entries.set([]); this.next = null; }
    }, { allowSignalWrites: true });
  }
  async show(): Promise<void> {
    this.open.set(true);
    requestAnimationFrame(() => { if (this.open()) this.panel?.nativeElement.querySelector<HTMLButtonElement>('button')?.focus(); });
  }
  close(): void { this.open.set(false); this.bell?.nativeElement.focus(); }
  @HostListener('keydown', ['$event']) key(event: KeyboardEvent): void {
    if (!this.open()) return;
    if (event.key === 'Escape') { event.preventDefault(); this.close(); }
    if (event.key !== 'Tab') return;
    const nodes = this.panel?.nativeElement.querySelectorAll<HTMLElement>('button:not(:disabled), a[href], select');
    if (!nodes?.length) return;
    const first = nodes[0], last = nodes[nodes.length - 1];
    if (!this.panel?.nativeElement.contains(document.activeElement)) {
      event.preventDefault(); (event.shiftKey ? last : first).focus(); return;
    }
    if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
    else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
  }
  async load(more = false, preserve = false): Promise<void> {
    const request = ++this.request;
    this.loading.set(true); this.error.set('');
    if (!more && !preserve) { this.entries.set([]); this.next = null; }
    try {
      const page = await this.center.page(this.category, this.unread, more ? this.next : null);
      if (request !== this.request) return;
      this.entries.set(more ? [...this.entries(), ...page.items.filter(x => !this.entries().some(e => e.id === x.id))] : page.items);
      this.next = page.nextCursor;
    } catch { if (request === this.request) this.error.set('تعذر تحديث الإشعارات. تحقق من الاتصال وأعد المحاولة.'); }
    finally { if (request === this.request) this.loading.set(false); }
  }
  async mark(id?: string): Promise<void> {
    if (this.reading()) return;
    this.reading.set(true);
    try {
      await this.center.read(id); await this.load(false, true);
      setTimeout(() => {
        if (!this.open() && !this.history) return;
        const root = this.panel?.nativeElement;
        (root?.querySelector<HTMLButtonElement>(id ? `[data-entry-id="${id}"] .entry-open` : '.view-switch button.active')
          ?? root?.querySelector<HTMLButtonElement>('.view-switch button.active'))?.focus();
      });
    }
    catch { this.error.set('تعذر حفظ حالة القراءة. أعد المحاولة.'); }
    finally { this.reading.set(false); }
  }
  go(entry: CenterItem): void {
    this.close(); void this.router.navigate(['/notification-entry'], { queryParams: { target: entry.id } });
  }
  time(value: string): string {
    const minutes = Math.max(0, Math.floor((Date.now() - Date.parse(value)) / 60000));
    if (minutes < 1) return 'الآن';
    if (minutes < 60) return `منذ ${new Intl.NumberFormat('ar-SA').format(minutes)} دقيقة`;
    return new Intl.DateTimeFormat('ar-SA', { day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit' }).format(new Date(value));
  }
  status(state: string): string { return ({ pending: 'بانتظار الإجراء', resolved: 'تمت المعالجة', cancelled: 'ملغى', correction: 'قيد التصحيح' } as Record<string, string>)[state] ?? ''; }
}
