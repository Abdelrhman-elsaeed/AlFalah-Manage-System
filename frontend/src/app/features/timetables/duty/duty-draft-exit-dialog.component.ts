import { Component, DestroyRef, ElementRef, ViewChild, inject } from '@angular/core';
import { TranslateModule, TranslateService } from '@ngx-translate/core';

@Component({ selector: 'app-duty-draft-exit-dialog', standalone: true, imports: [TranslateModule],
  template: `<dialog #dialog class="draft-exit-dialog" [dir]="translate.currentLang==='en'?'ltr':'rtl'" aria-modal="true" aria-labelledby="draft-exit-title" aria-describedby="draft-exit-description" (cancel)="$event.preventDefault(); finish(false)" (keydown)="dialogKey($event)">
    <span class="draft-mark"><i class="pi pi-file-edit" aria-hidden="true"></i></span>
    <span class="draft-label">{{ 'DUTY_EXEC.EXIT_DRAFT_LABEL' | translate }}</span>
    <h2 id="draft-exit-title">{{ 'DUTY_EXEC.EXIT_DRAFT_TITLE' | translate }}</h2>
    <p id="draft-exit-description">{{ 'DUTY_EXEC.EXIT_DRAFT_COPY' | translate }}</p>
    <ol><li>{{ 'DUTY_EXEC.EXIT_DRAFT_STEP1' | translate }}</li><li>{{ 'DUTY_EXEC.EXIT_DRAFT_STEP2' | translate }}</li><li>{{ 'DUTY_EXEC.EXIT_DRAFT_STEP3' | translate }}</li></ol>
    <p class="exit-note">{{ 'DUTY_EXEC.EXIT_DRAFT_LATER' | translate }}</p>
    <div class="exit-actions"><button type="button" class="continue-report" autofocus (click)="finish(false)">{{ 'DUTY_EXEC.EXIT_DRAFT_CONTINUE' | translate }}</button><button type="button" class="leave-draft" (click)="finish(true)">{{ 'DUTY_PAGES.OK' | translate }}</button></div>
  </dialog>`,
  styleUrl: './duty-draft-exit-dialog.component.css' })
export class DutyDraftExitDialogComponent {
  readonly translate = inject(TranslateService);
  @ViewChild('dialog', { static: true }) dialog!: ElementRef<HTMLDialogElement>;
  private resolve?: (leave: boolean) => void;
  private origin: HTMLElement | null = null;
  constructor() { inject(DestroyRef).onDestroy(() => this.finish(false)); }
  ask(): Promise<boolean> {
    this.origin = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    return new Promise(resolve => {
      this.resolve = resolve;
      this.dialog.nativeElement.showModal();
      this.dialog.nativeElement.querySelector<HTMLButtonElement>('button')?.focus();
    });
  }
  finish(leave: boolean) {
    if (this.dialog?.nativeElement.open) this.dialog.nativeElement.close();
    const resolve = this.resolve; this.resolve = undefined; resolve?.(leave);
    if (!leave && this.origin?.isConnected) this.origin.focus();
  }
  dialogKey(event: KeyboardEvent) {
    if (event.key !== 'Tab') return;
    const buttons = Array.from(this.dialog.nativeElement.querySelectorAll<HTMLButtonElement>('button'));
    const current = buttons.indexOf(document.activeElement as HTMLButtonElement);
    event.preventDefault(); event.stopPropagation();
    buttons[(current + (event.shiftKey ? -1 : 1) + buttons.length) % buttons.length]?.focus();
  }
}
