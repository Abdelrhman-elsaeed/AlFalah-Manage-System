import { inject } from '@angular/core';
import { CanActivateFn, CanDeactivateFn, Router } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import { AuthService } from '../services/auth.service';

export const schoolDutyAccessGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.activeSchoolId() && auth.hasAnyPermission(['Duty.View', 'Duty.Manage'])
    ? true : inject(Router).createUrlTree(['/unauthorized']);
};

export const schoolDutyManagerGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.activeSchoolId() && auth.hasAnyPermission(['Duty.Manage'])
    ? true : inject(Router).createUrlTree(['/unauthorized']);
};
export const schoolDutySectionGuard: CanDeactivateFn<{ canLeave(): boolean }> = component => component.canLeave();
export const schoolDutyDelegationGuard:CanActivateFn=()=>{
 const auth=inject(AuthService);return auth.activeSchoolId()&&auth.hasRole('SchoolManager')&&auth.hasAnyPermission(['Duty.Manage'])?true:inject(Router).createUrlTree(['/unauthorized']);
};

/** Old signed-document/execution bookmarks now enter their dedicated page. */
export const schoolDutyEntryGuard: CanActivateFn = route => route.queryParamMap.has('execution')
  ? inject(Router).createUrlTree(['/school-duty/execution'], { queryParams: route.queryParams, fragment: route.fragment ?? undefined }) : true;

/** Preserve retained notifications, bookmarks and signature-return context. */
export const legacySchoolDutyGuard: CanActivateFn = route => {
  const auth=inject(AuthService);
  if (route.queryParamMap.get('tab') !== 'duty' || !(auth.hasAnyRole(['SchoolManager', 'Instructor'])||auth.hasAnyPermission(['Duty.Manage']))) return true;
  const { tab, ...queryParams } = route.queryParams;
  return inject(Router).createUrlTree([queryParams['execution'] ? '/school-duty/execution' : '/school-duty'], { queryParams, fragment: route.fragment ?? undefined });
};

export const schoolDutyUnsavedGuard: CanDeactivateFn<{ hasDraft(): boolean }> = (component, _route, _state, next) =>
  next?.url?.startsWith('/auth/') || next?.url === '/unauthorized' || !component.hasDraft()
    || window.confirm(inject(TranslateService).instant('DUTY.LEAVE_DRAFT'));
