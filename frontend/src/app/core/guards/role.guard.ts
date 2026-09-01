import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Role-based guard. Pass required roles in route data: { roles: ['MainManager'] }
 */
export const roleGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.isAuthenticated()) {
    return router.createUrlTree(['/auth/school-login'], { queryParams: { returnUrl: state.url } });
  }

  const requiredRoles = (route.data?.['roles'] ?? []) as readonly string[];
  const requiredPermissions = (route.data?.['permissions'] ?? []) as readonly string[];

  if (state.url.startsWith('/student-affairs/') && !authService.activeSchoolId()) {
    return router.createUrlTree(['/unauthorized'], { queryParams: { reason: 'active-school-required' } });
  }

  if (requiredRoles.length > 0 && !authService.hasAnyRole(requiredRoles)) {
    return router.createUrlTree(['/unauthorized']);
  }

  if (requiredPermissions.length > 0 && !authService.hasAllPermissions(requiredPermissions)) {
    return router.createUrlTree(['/unauthorized']);
  }

  return true;
};
