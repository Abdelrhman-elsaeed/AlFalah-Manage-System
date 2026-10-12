import { inject } from '@angular/core';
import { CanActivateChildFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { SignatureEnrollmentService } from '../services/signature-enrollment.service';

export const signatureEnrollmentGuard: CanActivateChildFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.hasRole('SchoolManager')) return true;
  const target = () => router.createUrlTree(['/auth/signature'], { queryParams: { returnUrl: state.url } });
  return inject(SignatureEnrollmentService).requirement().pipe(
    map(response => response.isSuccess && response.data && !response.data.required ? true : target()),
    catchError(() => of(target()))
  );
};
