import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { roleLandingFor } from '../utils/role-landing';

/** Reuse an existing session when entering the platform from the public homepage. */
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const destination = () => auth.isAuthenticated()
    ? router.createUrlTree([roleLandingFor(auth.roles())])
    : true;

  if (auth.isAuthenticated() || !auth.getRefreshToken()) return destination();
  return auth.bootstrapSession().pipe(map(destination));
};
