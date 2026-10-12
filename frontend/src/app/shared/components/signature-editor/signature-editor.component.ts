import { AfterViewInit, Component, DestroyRef, ElementRef, OnDestroy, ViewChild, inject, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import SignaturePad from 'signature_pad';
import { SignatureEnrollmentService } from '../../../core/services/signature-enrollment.service';

@Component({
  selector: 'app-signature-editor', standalone: true, imports: [CommonModule, TranslateModule, ButtonModule],
  template: `
    <div class="signature-editor" [attr.aria-busy]="loading() || saving()">
      <p class="error" role="alert" *ngIf="error()">{{ error() }}</p>
      <p class="success" role="status" *ngIf="saved()">{{ 'ACCOUNT.SIGNATURE.SAVE_SUCCESS' | translate }}</p>
      <p *ngIf="loading()">{{ 'COMMON.LOADING' | translate }}</p>
      <div class="pad-wrap"><canvas #canvas width="600" height="200" tabindex="0"
        [attr.aria-label]="'ACCOUNT.SIGNATURE.LABEL' | translate"></canvas></div>
      <p class="hint">{{ 'ACCOUNT.SIGNATURE.DRAW_HINT' | translate }}</p>
      <div class="actions">
        <button pButton type="button" icon="pi pi-eraser" severity="secondary" [outlined]="true"
          [label]="'ACCOUNT.SIGNATURE.CLEAR' | translate" [disabled]="loading() || saving()" (click)="clear()"></button>
        <button pButton type="button" icon="pi pi-save" [label]="(onboarding() ? 'ACCOUNT.ONBOARDING.CONTINUE' : 'ACCOUNT.SIGNATURE.SAVE') | translate"
          [loading]="saving()" [disabled]="loading() || saving()" (click)="save()"></button>
      </div>
    </div>`,
  styles: [`:host{display:block;min-width:0}.pad-wrap{padding:12px;border:2px dashed var(--border-strong);border-radius:16px;background:var(--bg-surface-2)}canvas{display:block;width:100%;height:200px;touch-action:none;background:white;border-radius:10px;cursor:crosshair}.actions{display:flex;gap:12px;flex-wrap:wrap;margin-top:18px}.hint{font-size:13px;color:var(--text-muted);margin:10px 0}.error{color:var(--danger)}.success{color:var(--brand-700)}@media(max-width:500px){.pad-wrap{padding:6px}.actions>button{flex:1;min-width:120px}}`]
})
export class SignatureEditorComponent implements AfterViewInit, OnDestroy {
  @ViewChild('canvas', { static: true }) canvas!: ElementRef<HTMLCanvasElement>;
  readonly onboarding = input(false);
  readonly completed = output<void>();
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly saved = signal(false);
  private readonly service = inject(SignatureEnrollmentService);
  private readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);
  private pad!: SignaturePad;
  private observer?: ResizeObserver;
  private restoredImage: string | null = null;
  private destroyed = false;

  ngAfterViewInit(): void {
    this.pad = new SignaturePad(this.canvas.nativeElement, { penColor: '#172D21' });
    this.pad.addEventListener('beginStroke', () => { this.restoredImage = null; this.error.set(''); this.saved.set(false); });
    this.observer = new ResizeObserver(() => this.resize());
    this.observer.observe(this.canvas.nativeElement);
    this.resize();
    this.service.load().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: response => {
        this.loading.set(false);
        if (!response.isSuccess) { this.error.set(this.translate.instant('ACCOUNT.SIGNATURE.LOAD_FAILED')); return; }
        const image = response.data?.signatureDrawnData;
        if (image) {
          this.restoredImage = image;
          void this.pad.fromDataURL(image).catch(() => this.error.set(this.translate.instant('ACCOUNT.SIGNATURE.LOAD_FAILED')));
        }
      },
      error: () => { this.loading.set(false); this.error.set(this.translate.instant('ACCOUNT.SIGNATURE.LOAD_FAILED')); }
    });
  }
  clear(): void { this.pad.clear(); this.restoredImage = null; this.saved.set(false); this.error.set(''); }
  save(): void {
    if (this.loading() || this.saving()) return;
    if (this.pad.isEmpty() && !this.restoredImage) { this.error.set(this.translate.instant('ACCOUNT.SIGNATURE.REQUIRED')); return; }
    const image = this.restoredImage ?? this.pad.toDataURL('image/png');
    this.saving.set(true);
    this.service.save(image).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: response => {
        this.saving.set(false);
        if (response.isSuccess) { this.saved.set(true); this.error.set(''); this.completed.emit(); }
        else this.error.set(this.translate.instant('ACCOUNT.SIGNATURE.SAVE_FAILED'));
      },
      error: () => { this.saving.set(false); this.error.set(this.translate.instant('ACCOUNT.SIGNATURE.SAVE_ERROR')); }
    });
  }
  private resize(): void {
    if (this.destroyed) return;
    const element = this.canvas.nativeElement;
    const points = this.pad.toData();
    const ratio = Math.max(window.devicePixelRatio || 1, 1);
    element.width = Math.max(1, Math.round(element.clientWidth * ratio));
    element.height = Math.round(200 * ratio);
    element.getContext('2d')?.scale(ratio, ratio);
    this.pad.clear();
    if (points.length) this.pad.fromData(points);
    else if (this.restoredImage) void this.pad.fromDataURL(this.restoredImage);
  }
  ngOnDestroy(): void { this.destroyed = true; this.observer?.disconnect(); this.pad?.off(); }
}
