import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of, switchMap, throwError } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { StorageApiService } from './storage-api.service';
import { VisitArchiveApiService } from './visit-archive-api.service';

// Delegation is live server state, so role names/old token permissions cannot decide access.
export const storageGuard: CanActivateFn = (route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const archive = inject(VisitArchiveApiService);
  const api = inject(StorageApiService);
  if (!auth.isAuthenticated()) return router.createUrlTree(['/auth/school-login']);
  const url = new URL(state.url, 'http://localhost');
  const review = route.data['view'] === 'review' || (!route.data['own'] && url.pathname === '/school-manager/storage' &&
    (url.searchParams.get('view') === 'review' || url.searchParams.has('requirement') && !url.searchParams.has('file')));
  // Old personal bookmarks remain useful after an administrator grant. Resolve
  // live school management first, without weakening the personal-folder policy.
  const scope = route.data['own'] === true
    ? api.accessInfo(false).pipe(
        catchError(error => error.status === 403 ? of(null) : throwError(() => error)),
        switchMap(school => school?.canManage
          ? of({ redirectToSchool: true, ...school })
          : api.accessInfo(true).pipe(map(own => ({ redirectToSchool: false, ...own })))))
    : api.accessInfo(false).pipe(map(access => ({ redirectToSchool: false, ...access })));
  return scope.pipe(
    switchMap(access => access.redirectToSchool
      ? of(router.createUrlTree(['/school-manager/storage']))
      : route.data['manage'] === true && !access.canManage
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
