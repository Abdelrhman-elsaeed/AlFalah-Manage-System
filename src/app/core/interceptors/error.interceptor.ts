import { Injectable, inject, Injector } from '@angular/core';
import {
  HttpInterceptor,
  HttpRequest,
  HttpHandler,
  HttpEvent,
  HttpErrorResponse
} from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { Router } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import { AuthService } from '../services/auth.service';
import { ToastService } from '../services/toast.service';
import { ApiResponse } from '../models/api-response.model';
import { SUPPRESS_FORBIDDEN_REDIRECT } from '../http/http-context.tokens';

/**
 * Global HTTP error interceptor.
 *
 * Responsibilities:
 *  - 401 (unauthenticated): clear local auth state and redirect to login.
 *      The token-refresh-and-retry flow lives in AuthInterceptor — by the time
 *      a request reaches this interceptor, refresh has already failed.
 *  - 403 (forbidden): redirect to the Unauthorized page.
 *  - Other 4xx/5xx: surface the server's ApiResponse.message via a PrimeNG toast.
 *  - Network errors (status 0): show a translated network-error toast.
 *
 * Skipped endpoints:
 *  - login + forgot-password + reset-password: we don't want toasts on the
 *    auth screen itself; the components handle their own errors inline.
 *  - refresh: handled separately by AuthInterceptor.
 */
@Injectable()
export class ErrorInterceptor implements HttpInterceptor {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  // Lazy getter breaks the circular DI chain:
  // APP_INITIALIZER → TranslateService → HttpClient → HTTP_INTERCEPTORS → ErrorInterceptor → TranslateService
  // Using Injector.get() means TranslateService is only resolved on first HTTP error, not at bootstrap.
  private readonly injector = inject(Injector);
  private get translate(): TranslateService { return this.injector.get(TranslateService); }

  private static readonly SILENT_PATH_FRAGMENTS = [
    '/auth/login',
    '/auth/school-login',
    '/auth/main-manager-login',
    '/auth/forgot-password',
    '/auth/reset-password',
    '/auth/refresh'
  ];

  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    return next.handle(req).pipe(
      catchError((error: HttpErrorResponse) => {
        const silent = ErrorInterceptor.SILENT_PATH_FRAGMENTS.some(f => req.url.includes(f));

        // ── 401 ────────────────────────────────────────────────────────────────
        if (error.status === 401) {
          // AuthInterceptor has already attempted refresh and given up.
          if (!silent && this.authService.isAuthenticated()) {
            // Stale token on an authenticated session — wipe and bounce.
            this.authService.logout();
          } else if (!silent) {
            // No active session — just bounce to login (logout clears + redirects).
            this.authService.clearAuthData();
            this.router.navigate(['/auth/school-login']);
          }
          return throwError(() => error);
        }

        // ── 403 ────────────────────────────────────────────────────────────────
        if (error.status === 403) {
          if (!silent && !req.context.get(SUPPRESS_FORBIDDEN_REDIRECT)) {
            this.router.navigate(['/unauthorized']);
          }
          return throwError(() => error);
        }

        // ── All other errors ────────────────────────────────────────────────────
        if (!silent) {
          const message = this.extractMessage(error);
          const summaryKey = error.status === 0
            ? 'ERRORS.NETWORK_ERROR'
            : 'COMMON.ERROR';
          const summary = this.translate.instant(summaryKey);
          this.toast.error(summary, message);
        }

        return throwError(() => error);
      })
    );
  }

  private extractMessage(error: HttpErrorResponse): string {
    // Backend wraps errors in { isSuccess, message, errors, data? }
    const body = error.error as (ApiResponse & { message?: string }) | string | null;

    if (typeof body === 'string' && body.trim()) {
      return body;
    }

    if (body && typeof body === 'object') {
      if (body.message && typeof body.message === 'string') return body.message;
      if (Array.isArray(body.errors) && body.errors.length > 0) return body.errors.join(' — ');
    }

    if (error.status === 0) {
      return this.translate.instant('ERRORS.NETWORK_ERROR');
    }

    return this.translate.instant('ERRORS.SERVER_ERROR');
  }
}
