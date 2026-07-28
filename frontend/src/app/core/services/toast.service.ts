import { Injectable, Injector, inject } from '@angular/core';
import { MessageService } from 'primeng/api';
import { TranslateService } from '@ngx-translate/core';

export type ToastSeverity = 'success' | 'info' | 'warn' | 'error';

/**
 * Thin wrapper around PrimeNG's MessageService.
 * Centralizes toast invocation so components/services don't import primeng/api directly.
 *
 * D-33: dedupes identical (severity, summary, detail) tuples fired within a
 * short window so a cascade of failed HTTP requests (e.g. backend down) does
 * not stack 4-6 identical "تعذر الاتصال" toasts on top of each other.
 *
 * Callers are inconsistent about whether they hand over an already-translated
 * string or an i18n key, and a key that slipped through was rendered verbatim —
 * users saw "VISITS.PDF_DOWNLOAD_SUCCESS_TITLE" in the toast. Resolution now
 * happens here so neither calling style can leak a raw key to the screen.
 */
@Injectable({ providedIn: 'root' })
export class ToastService {
  private readonly messenger = inject(MessageService);
  // Must stay lazy. ErrorInterceptor injects ToastService eagerly, so an eager
  // TranslateService here closes the loop
  //   TranslateService (ctor loads ar.json) → HTTP_INTERCEPTORS → ErrorInterceptor
  //   → ToastService → TranslateService
  // which Angular reports as NG0200 and which kills the APP_INITIALIZER
  // translation load — leaving every | translate showing its raw key.
  private readonly injector = inject(Injector);
  private get translate(): TranslateService { return this.injector.get(TranslateService); }
  private static readonly DEDUPE_WINDOW_MS = 1500;

  /**
   * Shape of an i18n key: SCREAMING_SNAKE segments joined by dots. Arabic and
   * English display text can never match this, so the test is safe.
   */
  private static readonly KEY_PATTERN = /^[A-Z][A-Z0-9_]*(?:\.[A-Z0-9_]+)+$/;

  private readonly recent = new Map<string, number>();

  show(severity: ToastSeverity, summary: string, detail?: string): void {
    const resolvedSummary = this.resolve(summary);
    const resolvedDetail = this.resolve(detail);

    const key = `${severity}|${resolvedSummary}|${resolvedDetail ?? ''}`;
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
      summary: resolvedSummary,
      detail: resolvedDetail,
      life: severity === 'error' ? 6000 : 3500
    });
  }

  /**
   * Translates the text when it looks like an i18n key and a translation
   * exists. Anything else — display text, a server message, an unknown key —
   * is returned unchanged rather than blanked out.
   */
  private resolve(text: string | undefined): string | undefined {
    if (!text || !ToastService.KEY_PATTERN.test(text)) return text;
    const translated = this.translate.instant(text);
    return typeof translated === 'string' && translated !== text ? translated : text;
  }

  success(summary: string, detail?: string): void { this.show('success', summary, detail); }
  info(summary: string, detail?: string): void    { this.show('info', summary, detail); }
  warn(summary: string, detail?: string): void    { this.show('warn', summary, detail); }
  error(summary: string, detail?: string): void   { this.show('error', summary, detail); }
}
