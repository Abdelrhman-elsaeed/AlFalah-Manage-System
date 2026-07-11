import { Injectable, inject } from '@angular/core';
import { MessageService } from 'primeng/api';

export type ToastSeverity = 'success' | 'info' | 'warn' | 'error';

/**
 * Thin wrapper around PrimeNG's MessageService.
 * Centralizes toast invocation so components/services don't import primeng/api directly.
 *
 * D-33: dedupes identical (severity, summary, detail) tuples fired within a
 * short window so a cascade of failed HTTP requests (e.g. backend down) does
 * not stack 4-6 identical "تعذر الاتصال" toasts on top of each other.
 */
@Injectable({ providedIn: 'root' })
export class ToastService {
  private readonly messenger = inject(MessageService);
  private static readonly DEDUPE_WINDOW_MS = 1500;
  private readonly recent = new Map<string, number>();

  show(severity: ToastSeverity, summary: string, detail?: string): void {
    const key = `${severity}|${summary}|${detail ?? ''}`;
    const now = Date.now();
    const last = this.recent.get(key);
    if (last !== undefined && now - last < ToastService.DEDUPE_WINDOW_MS) {
      return;
    }
    this.recent.set(key, now);
    // GC stale entries
    for (const [k, t] of this.recent) {
      if (now - t > ToastService.DEDUPE_WINDOW_MS) this.recent.delete(k);
    }

    this.messenger.add({
      severity,
      summary,
      detail,
      life: severity === 'error' ? 6000 : 3500
    });
  }

  success(summary: string, detail?: string): void { this.show('success', summary, detail); }
  info(summary: string, detail?: string): void    { this.show('info', summary, detail); }
  warn(summary: string, detail?: string): void    { this.show('warn', summary, detail); }
  error(summary: string, detail?: string): void   { this.show('error', summary, detail); }
}