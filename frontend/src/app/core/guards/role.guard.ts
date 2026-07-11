import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Role-based guard. Pass required roles in route data: { roles: ['MainManager'] }
 */
export const roleGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.isAuthenticated()) {
    router.navigate(['/auth/school-login']);
    return false;
  }

  const requiredRoles: string[] = route.data?.['roles'] ?? [];
  const requiredPermissions: string[] = route.data?.['permissions'] ?? [];

  if (requiredRoles.length > 0 && !authService.hasAnyRole(requiredRoles)) {
    router.navigate(['/unauthorized']);
    return false;
  }

  if (requiredPermissions.length > 0) {
    const hasAll = requiredPermissions.every(p => authService.hasPermission(p));
    if (!hasAll) {
      router.navigate(['/unauthorized']);
      return false;
    }
  }

  return true;
};
