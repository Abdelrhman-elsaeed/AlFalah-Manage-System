import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }

  // Redirect to appropriate login based on intended route
  const url = state.url;
  if (url.startsWith('/notification-entry?')) {
    const target = new URL(url, window.location.origin).searchParams.get('target');
    if (target && /^[0-9a-f-]{36}$/i.test(target)) sessionStorage.setItem('alfalah_notification_target', target);
  }
  if (url.startsWith('/main-manager')) {
    router.navigate(['/auth/main-manager-login']);
  } else {
    router.navigate(['/auth/school-login']);
  }
  return false;
};
