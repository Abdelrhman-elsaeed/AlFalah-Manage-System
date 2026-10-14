import { Injectable, inject, signal } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

interface DutyErrorNotice { source: object; message: string; dismissed: () => void; focus: HTMLElement | null; }
@Injectable({ providedIn: 'root' })
export class DutyFeedbackService {
  private readonly translate = inject(TranslateService);
  readonly current = signal<DutyErrorNotice | null>(null);
  private queue: DutyErrorNotice[] = [];
  show(message: string, dismissed: () => void = () => {}, source: object = {}) {
    if (!message.trim()) return;
    const parts = message.split(/\s+\/\s+/);
    const text = parts.length === 2 && /[\u0600-\u06ff]/.test(parts[0]) && !/[\u0600-\u06ff]/.test(parts[1])
      ? parts[this.translate.currentLang === 'en' ? 1 : 0] : message;
    const notice = { source, message: text, dismissed, focus: document.activeElement instanceof HTMLElement ? document.activeElement : null };
    if (this.current()?.source === source) { this.current.update(old => ({ ...notice, focus: old!.focus })); return; }
    if (this.queue.some(x => x.source === source)) { this.queue = this.queue.map(x => x.source === source ? notice : x); return; }
    if (this.current()) this.queue.push(notice); else this.current.set(notice);
  }
  dismiss() {
    const old = this.current(); old?.dismissed(); this.current.set(this.queue.shift() ?? null);
    if (!this.current() && old?.focus?.isConnected) setTimeout(() => old.focus?.focus());
  }
  forget(source: object) {
    this.queue = this.queue.filter(x => x.source !== source);
    if (this.current()?.source === source) this.current.set(this.queue.shift() ?? null);
  }
  clear() { this.queue = []; this.current.set(null); }
}
