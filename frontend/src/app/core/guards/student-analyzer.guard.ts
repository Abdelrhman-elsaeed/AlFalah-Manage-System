import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { StudentAnalyzerService } from '../services/student-analyzer.service';

export const studentAnalyzerGuard: CanActivateFn = () => {
  const service = inject(StudentAnalyzerService);
  const router = inject(Router);

  return service.capabilities().pipe(
    map(response => response.isSuccess && response.data?.canAccess
      ? true
      : router.createUrlTree(['/unauthorized'])),
    catchError(() => of(router.createUrlTree(['/unauthorized'])))
  );
};
