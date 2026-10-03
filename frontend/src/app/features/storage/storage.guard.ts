import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { StorageApiService } from './storage-api.service';

// Delegation is live server state, so role names/old token permissions cannot decide access.
export const storageGuard: CanActivateFn = route => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (!auth.isAuthenticated()) return router.createUrlTree(['/auth/school-login']);
  return inject(StorageApiService).contextInfo(route.data['own'] === true).pipe(
    map(() => true),
    catchError(error => of(error.status === 401 || error.status === 403
      ? router.createUrlTree(['/unauthorized']) : true))
  );
};
