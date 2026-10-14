import { CommonModule } from '@angular/common';
import { Component, DestroyRef, ElementRef, ViewChild, effect, inject } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { DutyFeedbackService } from './duty-feedback.service';

@Component({ selector: 'app-duty-error-dialog', standalone: true, imports: [CommonModule, TranslateModule],
  template: `<dialog #dialog class="duty-error-dialog" role="alertdialog" aria-modal="true" aria-labelledby="duty-error-title" aria-describedby="duty-error-message" (cancel)="$event.preventDefault()" (keydown)="dialogKey($event)">
    <div class="error-mark"><i class="pi pi-exclamation-circle" aria-hidden="true"></i></div>
    <h2 id="duty-error-title">{{ 'DUTY_PAGES.ERROR_TITLE' | translate }}</h2>
    <p id="duty-error-message" dir="auto">{{ feedback.current()?.message }}</p>
    <button type="button" autofocus (click)="acknowledge()">{{ 'DUTY_PAGES.OK' | translate }}</button>
  </dialog>`,
  styles: [`.duty-error-dialog{position:fixed;inset:0;margin:auto;border:1px solid var(--border);border-radius:22px;padding:30px;width:min(420px,calc(100vw - 32px));max-height:calc(100dvh - 48px);overflow:auto;text-align:center;color:var(--text-strong);background:var(--bg-surface);box-shadow:0 24px 90px #102f2540;font:inherit}.duty-error-dialog::backdrop{background:#142a266b;backdrop-filter:blur(4px)}.error-mark{display:grid;place-items:center;width:64px;height:64px;margin:0 auto 18px;border-radius:20px;background:#fff0ed;color:#a9362b;border:1px solid #f0c9c3;font-size:28px}h2{margin:0 0 12px;font-size:22px}p{white-space:pre-wrap;overflow-wrap:anywhere;font-size:15px;line-height:1.9;color:var(--text);margin:0 0 24px}button{font:inherit;font-weight:700;font-size:15px;width:100%;min-height:48px;border:0;border-radius:11px;color:white;background:var(--brand-800);cursor:pointer}button:focus-visible{outline:3px solid var(--gold-600);outline-offset:4px}@media(prefers-reduced-motion:reduce){*,*::backdrop{animation:none;transition:none}}`] })
export class DutyErrorDialogComponent {
  readonly feedback = inject(DutyFeedbackService);
  @ViewChild('dialog', { static: true }) dialog!: ElementRef<HTMLDialogElement>;
  private readonly destroy = inject(DestroyRef);
  private destroyed = false;
  constructor() {
    effect(() => {
      const notice = this.feedback.current();
      setTimeout(() => {
        if (this.destroyed) return;
        const dialog = this.dialog.nativeElement;
        if (notice && this.feedback.current()) { if (!dialog.open) dialog.showModal(); dialog.querySelector('button')?.focus(); }
        else if (!this.feedback.current() && dialog.open) dialog.close();
      });
    });
    this.destroy.onDestroy(() => { this.destroyed = true; this.feedback.clear(); });
  }
  acknowledge() {
    const origin = this.feedback.current()?.focus;
    this.dialog.nativeElement.close(); this.feedback.dismiss();
    if (!origin?.isConnected) setTimeout(() => document.querySelector<HTMLButtonElement>('.document-unavailable button,.duty-workspace .page-empty button')?.focus());
  }
  dialogKey(event: KeyboardEvent) {
    if (event.key === 'Tab') { event.preventDefault(); event.stopPropagation(); this.dialog.nativeElement.querySelector('button')?.focus(); }
    if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); }
  }
}
