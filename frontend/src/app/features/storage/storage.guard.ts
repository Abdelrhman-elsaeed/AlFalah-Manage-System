import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of, switchMap } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { StorageApiService } from './storage-api.service';
import { VisitArchiveApiService } from './visit-archive-api.service';

// Delegation is live server state, so role names/old token permissions cannot decide access.
export const storageGuard: CanActivateFn = (route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const archive = inject(VisitArchiveApiService);
  if (!auth.isAuthenticated()) return router.createUrlTree(['/auth/school-login']);
  const url = new URL(state.url, 'http://localhost');
  const review = route.data['view'] === 'review' || (!route.data['own'] && url.pathname === '/school-manager/storage' &&
    (url.searchParams.get('view') === 'review' || url.searchParams.has('requirement') && !url.searchParams.has('file')));
  return inject(StorageApiService).accessInfo(route.data['own'] === true).pipe(
    switchMap(access => route.data['manage'] === true && !access.canManage
      ? of(router.createUrlTree(['/unauthorized']))
      : review && !access.canReviewEvidence
        ? of(router.createUrlTree(['/unauthorized']))
      : route.data['archive'] === true ? archive.operationsStatus().pipe(map(() => true)) : of(true)),
    catchError(error => of(error.status === 401 || error.status === 403
      ? router.createUrlTree(['/unauthorized']) : true))
  );
};

// The visit workspace keeps its existing role access; storage delegates enter only
// when the live archive policy also grants Visit.View for their school.
export const visitWorkspaceGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (!auth.isAuthenticated()) return router.createUrlTree(['/auth/school-login'], {queryParams:{returnUrl:state.url}});
  if (auth.hasAnyRole(['SchoolManager', 'Moderator', 'MainManager', 'SuperAdmin', 'Instructor'])) return true;
  return inject(VisitArchiveApiService).operationsStatus().pipe(
    map(() => true), catchError(() => of(router.createUrlTree(['/unauthorized']))));
};
