import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Permission-based route guard.
 *
 * Reads `route.data.permissions` (string[]) and grants access only if the
 * currently authenticated user has ALL of the listed permissions.
 *
 * Usage:
 *   {
 *     path: 'admin',
 *     canActivate: [authGuard, permissionGuard],
 *     data: { permissions: ['Schools.Create', 'Users.Manage'] },
 *     ...
 *   }
 *
 * Unauthenticated users are redirected to the appropriate login page.
 * Authenticated users missing the permissions are redirected to /unauthorized.
 */
export const permissionGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.isAuthenticated()) {
    const url = state.url;
    if (url.startsWith('/main-manager')) {
      router.navigate(['/auth/main-manager-login']);
    } else {
      router.navigate(['/auth/school-login']);
    }
    return false;
  }

  const requiredPermissions: string[] = route.data?.['permissions'] ?? [];
  if (requiredPermissions.length === 0) {
    // No permission requirements declared → allow (AuthGuard already passed).
    return true;
  }

  const hasAll = requiredPermissions.every(p => authService.hasPermission(p));
  if (!hasAll) {
    router.navigate(['/unauthorized']);
    return false;
  }

  return true;
};
