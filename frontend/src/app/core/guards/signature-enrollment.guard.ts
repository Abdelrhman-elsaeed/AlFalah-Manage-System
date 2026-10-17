import { inject } from '@angular/core';
import { CanActivateChildFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { SignatureEnrollmentService } from '../services/signature-enrollment.service';

export const signatureEnrollmentGuard: CanActivateChildFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.hasRole('SchoolManager')) return true;
  if (state.url.split('?')[0] === '/account/signature') return true;
  const path = state.url.split(/[?#]/)[0];
  if (path === '/school-duty' || path.startsWith('/school-duty/')) return true;
  if (path === '/standby-tasks') return true;
  if (state.url.startsWith('/timetable') && new URLSearchParams(state.url.split('?')[1]).get('tab') === 'duty') return true;
  const target = () => router.createUrlTree(['/auth/signature'], { queryParams: { returnUrl: state.url } });
  return inject(SignatureEnrollmentService).requirement().pipe(
    map(response => response.isSuccess && response.data && !response.data.required ? true : target()),
    catchError(() => of(target()))
  );
};
