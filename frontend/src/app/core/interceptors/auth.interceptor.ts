import { Injector, inject } from '@angular/core';
import {
  HttpContextToken,
  HttpErrorResponse,
  HttpEvent,
  HttpHandlerFn,
  HttpInterceptorFn,
  HttpRequest
} from '@angular/common/http';
import { Router } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import { Observable, catchError, finalize, map, shareReplay, switchMap, throwError } from 'rxjs';
import { SKIP_AUTH_REFRESH, SUPPRESS_ERROR_TOAST, SUPPRESS_FORBIDDEN_REDIRECT } from '../http/http-context.tokens';
import { extractHttpErrorMessage, isUnreadBlobError } from '../http/http-error-message';
import { AuthService } from '../services/auth.service';
import { ToastService } from '../services/toast.service';

const RETRIED_AFTER_REFRESH = new HttpContextToken<boolean>(() => false);
const PUBLIC_AUTH_PATHS = ['/auth/school-login', '/auth/main-manager-login', '/auth/refresh', '/auth/logout'];
const SILENT_PATHS = [...PUBLIC_AUTH_PATHS, '/auth/forgot-password', '/auth/reset-password'];

let refreshInFlight$: Observable<string> | null = null;

/** Locale/JSON headers, Bearer auth, single-flight refresh, and global errors. */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const toast = inject(ToastService);
  const injector = inject(Injector);
  const prepared = prepareRequest(request, auth);

  return next(prepared).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse)) return throwError(() => error);

      if (error.status === 401) {
        return handleUnauthorized(prepared, next, error, auth, router, toast);
      }

      if (error.status === 403) {
        if (!prepared.context.get(SUPPRESS_FORBIDDEN_REDIRECT) && !isSilent(prepared.url)) {
          void router.navigate(['/unauthorized']);
        }
        return throwError(() => error);
      }

      if (!isSilent(prepared.url) &&
          !prepared.context.get(SUPPRESS_ERROR_TOAST) &&
          !isUnreadBlobError(error)) {
        const translate = injector.get(TranslateService);
        const summary = translate.instant(error.status === 0 ? 'ERRORS.NETWORK_ERROR' : 'COMMON.ERROR');
        const detail = extractHttpErrorMessage(error) ||
          translate.instant(error.status === 0 ? 'ERRORS.NETWORK_ERROR' : 'ERRORS.SERVER_ERROR');
        toast.error(summary, detail);
      }

      return throwError(() => error);
    })
  );
};

function prepareRequest(request: HttpRequest<unknown>, auth: AuthService): HttpRequest<unknown> {
  const setHeaders: Record<string, string> = {};
  if (!request.headers.has('Accept')) setHeaders['Accept'] = 'application/json';
  if (!request.headers.has('Accept-Language')) setHeaders['Accept-Language'] = auth.getAcceptLanguage();

  const isMultipart = typeof FormData !== 'undefined' && request.body instanceof FormData;
  if (request.body !== null && !isMultipart && !request.headers.has('Content-Type')) {
    setHeaders['Content-Type'] = 'application/json; charset=utf-8';
  }

  const token = auth.getAccessToken();
  if (token && !isPublicAuthRequest(request.url) && !request.headers.has('Authorization')) {
    setHeaders['Authorization'] = `Bearer ${token}`;
  }

  return Object.keys(setHeaders).length > 0 ? request.clone({ setHeaders }) : request;
}

function handleUnauthorized(
  request: HttpRequest<unknown>,
  next: HttpHandlerFn,
  originalError: HttpErrorResponse,
  auth: AuthService,
  router: Router,
  toast: ToastService
): Observable<HttpEvent<unknown>> {
  if (isPublicAuthRequest(request.url) ||
      request.context.get(RETRIED_AFTER_REFRESH) ||
      request.context.get(SKIP_AUTH_REFRESH) ||
      !auth.getRefreshToken()) {
    endSession(auth, router);
    return throwError(() => originalError);
  }

  if (!refreshInFlight$) {
    refreshInFlight$ = auth.refreshToken().pipe(
      map(response => {
        const token = response.isSuccess ? response.data?.accessToken : null;
        if (!token) throw new Error('Token refresh did not return an access token.');
        return token;
      }),
      catchError(error => {
        endSession(auth, router);
        return throwError(() => error);
      }),
      finalize(() => { refreshInFlight$ = null; }),
      shareReplay({ bufferSize: 1, refCount: false })
    );
  }

  return refreshInFlight$.pipe(
    switchMap(token => {
      if (!canReplayAutomatically(request)) {
        toast.warn('انتهت الجلسة مؤقتاً', 'تم تجديد الجلسة. راجع البيانات ثم أعد الإرسال.');
        return throwError(() => originalError);
      }

      return next(request.clone({
        context: request.context.set(RETRIED_AFTER_REFRESH, true),
        setHeaders: { Authorization: `Bearer ${token}` }
      }));
    })
  );
}

function canReplayAutomatically(request: HttpRequest<unknown>): boolean {
  const safeMethod = ['GET', 'HEAD', 'OPTIONS'].includes(request.method.toUpperCase());
  const isMultipart = typeof FormData !== 'undefined' && request.body instanceof FormData;
  return safeMethod || (!isMultipart && request.headers.has('Idempotency-Key'));
}

function endSession(auth: AuthService, router: Router): void {
  const returnUrl = router.url.startsWith('/auth/') ? undefined : router.url;
  auth.clearAuthData();
  void router.navigate(['/auth/school-login'], {
    queryParams: returnUrl ? { returnUrl } : undefined
  });
}

function isPublicAuthRequest(url: string): boolean {
  return PUBLIC_AUTH_PATHS.some(path => url.includes(path));
}

function isSilent(url: string): boolean {
  return SILENT_PATHS.some(path => url.includes(path));
}
