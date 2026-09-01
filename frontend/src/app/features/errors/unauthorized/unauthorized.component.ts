import { Component, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-unauthorized',
  standalone: true,
  imports: [CommonModule, RouterLink, TranslateModule],
  template: `
    <div class="page" dir="rtl">
      <div class="content">
        <div class="icon"><i class="pi pi-lock" aria-hidden="true"></i></div>
        <h1>{{ 'ERRORS.UNAUTHORIZED' | translate }}</h1>
        <p>{{ message() }}</p>
        <a [routerLink]="returnLink()" class="btn-back">{{ returnLabelKey() | translate }}</a>
      </div>
    </div>
  `,
  styles: [`
    :host { display: block; }
    .page {
      display: grid;
      min-height: 100dvh;
      place-items: center;
      padding: var(--space-5);
      color: var(--text-strong);
      font-family: var(--font-app);
      background:
        radial-gradient(circle at 80% 10%, rgba(30, 142, 78, 0.08), transparent 38%),
        var(--bg-page);
    }
    .content {
      width: min(100%, 420px);
      padding: var(--space-6);
      text-align: center;
      background: var(--bg-surface);
      border: 1px solid var(--border);
      border-top: 3px solid var(--gold);
      border-radius: var(--radius-lg);
      box-shadow: var(--shadow-sm);
    }
    .icon {
      display: inline-grid;
      width: 54px;
      height: 54px;
      place-items: center;
      margin-bottom: var(--space-3);
      color: var(--brand-700);
      font-size: 1.35rem;
      background: var(--brand-50);
      border: 1px solid var(--brand-100);
      border-radius: 50%;
    }
    h1 { margin: 0; color: var(--text-strong); font-size: 1.35rem; font-weight: 800; }
    p { margin: var(--space-2) 0 var(--space-5); color: var(--text-muted); font-size: 0.86rem; }
    .btn-back {
      display: inline-flex;
      height: var(--control-height);
      align-items: center;
      justify-content: center;
      padding-inline: 1rem;
      color: #fff;
      font-size: 0.82rem;
      font-weight: 800;
      text-decoration: none;
      background: var(--brand-600);
      border: 1px solid var(--brand-600);
      border-radius: var(--radius-md);
      box-shadow: var(--shadow-xs);
      transition: background var(--duration-fast), transform var(--duration-fast), box-shadow var(--duration-normal);
    }
    .btn-back:hover {
      color: #fff;
      text-decoration: none;
      background: var(--brand-700);
      transform: translateY(-1px);
      box-shadow: var(--shadow-sm);
    }
  `]

})
export class UnauthorizedComponent {
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);

  readonly message = computed(() => this.route.snapshot.queryParamMap.get('reason') === 'active-school-required'
    ? 'لم يتم تحديد مدرسة نشطة.'
    : 'ليس لديك صلاحية للوصول إلى هذه الصفحة.');

  readonly returnLink = computed(() => {
    if (!this.auth.isAuthenticated()) return '/auth/school-login';
    if (this.auth.hasAnyRole(['MainManager', 'SuperAdmin'])) return '/main-manager/dashboard';
    if (this.auth.hasRole('SchoolManager')) return '/school-manager/dashboard';
    if (this.auth.hasRole('Moderator')) return '/moderator/dashboard';
    if (this.auth.hasRole('Instructor')) return '/instructor/dashboard';
    if (this.auth.hasRole('StudentAffairsOfficer')) return '/student-affairs/settings';
    return '/dashboard';
  });

  readonly returnLabelKey = computed(() => this.auth.isAuthenticated()
    ? 'ERRORS.BACK_TO_DASHBOARD'
    : 'AUTH.BACK_TO_SCHOOL_LOGIN');
}
