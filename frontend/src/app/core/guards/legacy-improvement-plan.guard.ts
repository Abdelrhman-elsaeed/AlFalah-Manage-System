import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { VisitsV2Service } from '../services/visits-v2.service';

/** Keeps legacy improvement-plan deep links available only while V2 is off. */
export const legacyImprovementPlanGuard: CanActivateFn = () => {
  const visits = inject(VisitsV2Service);
  const router = inject(Router);
  return visits.availability().pipe(
    map(response => response.data?.isEnabled ? router.createUrlTree(['/visits-v2']) : true),
    catchError(() => of(true))
  );
};

