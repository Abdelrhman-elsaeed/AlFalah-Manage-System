import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../../../core/services/auth.service';

function makeDashboard(titleKey: string, emoji: string, description: string) {
  @Component({
    standalone: true,
    imports: [CommonModule, TranslateModule],
    template: `
      <div class="page" dir="rtl">
        <header class="page-header">
          <div class="page-header__titles">
            <h1 class="page-header__title">{{ '${titleKey}' | translate }}</h1>
            <p class="page-header__subtitle">مرحباً، {{ authService.currentUser()?.fullName }}</p>
          </div>
        </header>

        <section class="surface-card dashboard-card">
          <div class="dashboard-card__icon">${emoji}</div>
          <h2 class="dashboard-card__title">{{ '${titleKey}' | translate }}</h2>
          <p class="dashboard-card__desc">${description}</p>
        </section>
      </div>
    `,
    styles: [`
      :host { display: block; color: var(--text); font-family: var(--font-app); }
      .page { display: flex; flex-direction: column; gap: var(--space-5); }
      .page-header {
        display: flex; align-items: center; justify-content: space-between;
        gap: var(--space-4); padding-bottom: var(--space-3);
        border-bottom: 1px solid var(--border);
      }
      .page-header__titles { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
      .page-header__title { font-size: 1.5rem; font-weight: 800; color: var(--text-strong); margin: 0; }
      .page-header__subtitle { font-size: 0.85rem; color: var(--text-muted); margin: 0; }

      .surface-card,
      .dashboard-card {
        background-color: var(--bg-surface) !important;
        background-image: none !important;
        color: var(--text);
        border: 1px solid var(--border);
        border-radius: var(--radius-lg);
        padding: var(--space-5);
        box-shadow: var(--shadow-sm);
      }
      .dashboard-card { text-align: center; padding: 5rem var(--space-5); }
      .dashboard-card__icon { font-size: 4rem; margin-bottom: var(--space-4); color: var(--brand-500); }
      .dashboard-card__title { font-size: 1.5rem; font-weight: 800; color: var(--text-strong); margin: 0 0 var(--space-3); }
      .dashboard-card__desc { color: var(--text-muted); font-size: 0.95rem; max-width: 540px; margin: 0 auto; line-height: 1.8; }
    `]
  })
  class DashboardPlaceholder {
    authService = inject(AuthService);
  }
  return DashboardPlaceholder;
}

export const SchoolManagerDashboardComponent = makeDashboard(
  'DASHBOARD.SCHOOL_MANAGER', '🏫',
  'سيتم تطوير لوحة مدير المدرسة في المرحلة التاسعة'
);

export const ModeratorDashboardComponent = makeDashboard(
  'DASHBOARD.MODERATOR', '📋',
  'سيتم تطوير لوحة المشرف في المرحلة التاسعة'
);

export const InstructorDashboardComponent = makeDashboard(
  'DASHBOARD.INSTRUCTOR', '👩‍🏫',
  'سيتم تطوير لوحة المعلم في المرحلة التاسعة'
);