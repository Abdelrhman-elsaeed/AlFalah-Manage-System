import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'app-unauthorized',
  standalone: true,
  imports: [CommonModule, RouterLink, TranslateModule],
  template: `
    <div class="page" dir="rtl">
      <div class="content">
        <div class="icon">🔒</div>
        <h1>{{ 'ERRORS.UNAUTHORIZED' | translate }}</h1>
        <p>{{ 'ERRORS.FORBIDDEN' | translate }}</p>
        <a routerLink="/auth/school-login" class="btn-back">{{ 'AUTH.BACK_TO_SCHOOL_LOGIN' | translate }}</a>
      </div>
    </div>
  `,
  styles: [`
    :host { display: block; }
    .page{
      min-height:100vh;
      display:flex; align-items:center; justify-content:center;
      background: var(--bg-page);
      font-family: var(--font-app);
      color: var(--text-strong);
    }
    .content{ text-align:center; padding:2rem; max-width: 480px; }
    .icon{ font-size:4rem; margin-bottom:1rem; color: var(--brand-700); }
    h1{ font-size:1.75rem; margin-bottom:.75rem; color: var(--text-strong); font-weight: 800; }
    p{ color: var(--text-muted); margin-bottom:2rem; }
    .btn-back{
      display:inline-block; padding:.75rem 1.75rem;
      background: var(--brand-500);
      color: var(--text-on-brand);
      border-radius: var(--radius-md);
      text-decoration:none;
      font-weight: 700;
      transition: background .15s, transform .15s;
      box-shadow: var(--shadow-sm);
    }
    .btn-back:hover{
      background: var(--brand-700);
      transform: translateY(-1px);
      text-decoration: none;
    }
  `]
})
export class UnauthorizedComponent {}
