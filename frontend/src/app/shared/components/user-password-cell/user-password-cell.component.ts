import { CommonModule } from '@angular/common';
import { Component, DestroyRef, OnChanges, OnDestroy, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslateModule } from '@ngx-translate/core';
import { UsersService } from '../../../core/services/users.service';

@Component({ selector: 'app-user-password-cell', standalone: true, imports: [CommonModule, TranslateModule],
  template: `<div class="password-cell"><span dir="ltr" class="secret" *ngIf="!unavailable()">{{ password() ?? '••••••••' }}</span><span class="unavailable" *ngIf="unavailable()">{{ 'USERS.PASSWORD_UNAVAILABLE' | translate }}</span><button type="button" class="reveal" [disabled]="loading()" [attr.aria-label]="(password() ? 'USERS.HIDE_PASSWORD' : 'USERS.SHOW_PASSWORD') | translate" [attr.title]="(password() ? 'USERS.HIDE_PASSWORD' : 'USERS.SHOW_PASSWORD') | translate" (click)="toggle()"><i class="pi" [ngClass]="loading() ? 'pi-spinner pi-spin' : password() ? 'pi-eye-slash' : 'pi-eye'" aria-hidden="true"></i></button></div><small *ngIf="unavailable()">{{ 'USERS.PASSWORD_LEGACY_HINT' | translate }}</small><small role="alert" *ngIf="failed()">{{ 'USERS.PASSWORD_REVEAL_FAILED' | translate }}</small>`,
  styles: [`.password-cell{display:flex;gap:8px;align-items:center;min-width:140px}.secret{font-family:monospace;overflow-wrap:anywhere;max-width:220px}.reveal{display:grid;place-items:center;width:32px;height:32px;flex-shrink:0;border:1px solid var(--border);border-radius:9px;background:var(--brand-50);color:var(--brand-700);cursor:pointer}.reveal:focus-visible{outline:2px solid var(--brand-600);outline-offset:2px}.unavailable,small{color:var(--text-muted);font-size:12px}small{display:block;max-width:220px;line-height:1.6;margin-top:4px}`] })
export class UserPasswordCellComponent implements OnChanges, OnDestroy {
  readonly userId = input.required<string>();
  readonly password = signal<string | null>(null);
  readonly loading = signal(false);
  readonly unavailable = signal(false);
  readonly failed = signal(false);
  private readonly users = inject(UsersService);
  private readonly destroyRef = inject(DestroyRef);
  private timer?: ReturnType<typeof setTimeout>;
  private requestVersion = 0;
  toggle(): void {
    if (this.password()) { this.hide(); return; }
    if (this.loading()) return;
    const version = ++this.requestVersion;
    this.loading.set(true); this.failed.set(false); this.unavailable.set(false);
    this.users.revealPassword(this.userId()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: response => {
        if (version !== this.requestVersion) return;
        this.loading.set(false);
        if (!response.isSuccess || !response.data) { this.failed.set(true); return; }
        this.unavailable.set(!response.data.available);
        this.password.set(response.data.password);
        if (response.data.password) this.timer = setTimeout(() => this.hide(), 20_000);
      },
      error: () => { if (version === this.requestVersion) { this.loading.set(false); this.failed.set(true); } }
    });
  }
  hide(): void { if (this.timer) clearTimeout(this.timer); this.password.set(null); }
  ngOnChanges(): void { this.hide(); this.requestVersion++; this.loading.set(false); this.unavailable.set(false); this.failed.set(false); }
  ngOnDestroy(): void { this.hide(); this.requestVersion++; }
}
