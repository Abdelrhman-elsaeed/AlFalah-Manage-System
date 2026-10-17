import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

/** Preserve previous register/coverage links after moving the ledger into standby. */
export const legacyCoverageRegisterGuard: CanActivateFn = route => {
  if (route.queryParamMap.get('register') !== 'true' && route.queryParamMap.get('tab') !== 'coverage') return true;
  return inject(Router).createUrlTree(['/intelligent-timetable/standby'], {
    queryParams: { ...route.queryParams, tab: 'daily' }
  });
};
