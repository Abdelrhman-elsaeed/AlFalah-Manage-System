import { Component, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../../../core/services/auth.service';
import { roleLandingFor } from '../../../core/utils/role-landing';
import { SignatureEditorComponent } from '../../../shared/components/signature-editor/signature-editor.component';

@Component({ selector: 'app-signature-onboarding', standalone: true, imports: [TranslateModule, SignatureEditorComponent],
  template: `<main class="onboarding" dir="rtl"><section class="welcome" aria-labelledby="signature-welcome"><div class="welcome-icon"><i class="pi pi-pen-to-square" aria-hidden="true"></i></div><span class="eyebrow">{{ 'ACCOUNT.ONBOARDING.EYEBROW' | translate }}</span><h1 id="signature-welcome">{{ 'ACCOUNT.ONBOARDING.TITLE' | translate }}</h1><p class="intro">{{ 'ACCOUNT.ONBOARDING.INTRO' | translate }}</p><div class="reassurance"><i class="pi pi-shield" aria-hidden="true"></i><p>{{ 'ACCOUNT.ONBOARDING.PRIVACY' | translate }}</p></div><app-signature-editor [onboarding]="true" (completed)="continue()"/><p class="later">{{ 'ACCOUNT.ONBOARDING.LATER' | translate }}</p><button type="button" class="logout" (click)="auth.logout()">{{ 'AUTH.LOGOUT' | translate }}</button></section></main>`,
  styles: [`.onboarding{min-height:100dvh;display:grid;place-items:center;padding:28px 16px;background:radial-gradient(ellipse at top, var(--brand-100),transparent 70%),var(--bg-page)}.welcome{width:min(100%,720px);padding:32px;border:1px solid var(--border);border-radius:26px;background:var(--bg-surface);box-shadow:var(--shadow-lg)}.welcome-icon{display:grid;place-items:center;width:56px;height:56px;border-radius:18px;background:var(--brand-50);color:var(--brand-700);font-size:25px;margin-bottom:16px}.eyebrow{color:var(--brand-700);font-size:13px;font-weight:700}h1{margin:8px 0;color:var(--text-strong);font-size:28px}.intro,.later{color:var(--text-muted);line-height:1.8;font-size:14px}.reassurance{display:flex;gap:12px;align-items:flex-start;padding:14px;background:var(--brand-50);border-radius:14px;margin:20px 0}.reassurance i{color:var(--brand-700);margin-top:5px}.reassurance p{margin:0;line-height:1.8;font-size:13px;color:var(--text)}.logout{border:0;background:none;color:var(--text-muted);cursor:pointer;font:inherit;text-decoration:underline}@media(max-width:500px){.welcome{padding:22px 16px;border-radius:20px}h1{font-size:24px}.onboarding{padding:16px 10px}}`]
})
export class SignatureOnboardingComponent {
  readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  continue(): void {
    const requested = this.route.snapshot.queryParamMap.get('returnUrl');
    const safe = requested?.startsWith('/') && !requested.startsWith('//') && !requested.startsWith('/auth');
    void this.router.navigateByUrl(safe ? requested! : roleLandingFor(this.auth.roles()) ?? '/dashboard');
  }
}
