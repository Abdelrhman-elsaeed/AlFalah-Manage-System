import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, computed, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, map, of, switchMap, tap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { SKIP_AUTH_REFRESH } from '../http/http-context.tokens';
import {
  AuthResponseDto,
  CurrentUserDto,
  MainManagerLoginRequest,
  SchoolLoginRequestDto,
  SchoolLookup
} from '../models/auth.models';

const ACCESS_TOKEN_KEY = 'alfalah_access_token';
const REFRESH_TOKEN_KEY = 'alfalah_refresh_token';
const USER_KEY = 'alfalah_user';
const ROLE_CLAIM = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

interface JwtClaims {
  readonly sub?: string;
  readonly unique_name?: string;
  readonly exp?: number;
  readonly preferred_language?: string;
  readonly active_school_id?: string;
  readonly permission?: string | readonly string[];
  readonly role?: string | readonly string[];
  readonly [ROLE_CLAIM]?: string | readonly string[];
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly apiUrl = `${environment.apiUrl}/api/v1/auth`;
  private accessToken: string | null = this.readSession(ACCESS_TOKEN_KEY);
  private refreshTokenValue: string | null = this.readSession(REFRESH_TOKEN_KEY);
  private readonly currentUserState = signal<CurrentUserDto | null>(this.restoreUser());

  readonly currentUser = this.currentUserState.asReadonly();
  readonly isAuthenticated = computed(() =>
    this.currentUserState() !== null && this.accessToken !== null && !this.isTokenExpired(this.accessToken));
  readonly roles = computed<readonly string[]>(() => this.currentUserState()?.roles ?? []);
  readonly permissions = computed<readonly string[]>(() => this.currentUserState()?.permissions ?? []);
  readonly activeSchoolId = computed(() => this.currentUserState()?.activeSchoolId);

  constructor(
    private readonly http: HttpClient,
    private readonly router: Router
  ) {}

  schoolLogin(request: SchoolLoginRequestDto): Observable<ApiResponse<AuthResponseDto>> {
    return this.authenticate(`${this.apiUrl}/school-login`, request);
  }

  mainManagerLogin(request: MainManagerLoginRequest): Observable<ApiResponse<AuthResponseDto>> {
    return this.authenticate(`${this.apiUrl}/main-manager-login`, request);
  }

  /** Restores and verifies a tab-scoped session before protected routes mount. */
  bootstrapSession(): Observable<void> {
    if (!this.accessToken && !this.refreshTokenValue) return of(undefined);

    const session$ = this.accessToken && !this.isTokenExpired(this.accessToken)
      ? this.loadCurrentIdentity().pipe(map(() => undefined))
      : this.refreshToken().pipe(map(() => undefined));

    return session$.pipe(
      catchError(() => {
        this.clearAuthData();
        return of(undefined);
      })
    );
  }

  /** Refreshes the rotated token pair, then re-verifies authoritative grants via /auth/me. */
  refreshToken(): Observable<ApiResponse<AuthResponseDto>> {
    const refreshToken = this.refreshTokenValue;
    if (!refreshToken) return throwError(() => new Error('No refresh token is available.'));

    return this.http.post<ApiResponse<AuthResponseDto>>(
      `${this.apiUrl}/refresh`,
      { refreshToken }
    ).pipe(
      tap(response => this.acceptAuthResponse(response)),
      switchMap(response => {
        if (!response.isSuccess || !response.data) return of(response);
        return this.loadCurrentIdentity(true).pipe(map(() => response));
      })
    );
  }

  loadCurrentIdentity(skipRefresh = false): Observable<CurrentUserDto> {
    const context = skipRefresh
      ? new HttpContext().set(SKIP_AUTH_REFRESH, true)
      : undefined;
    return this.http.get<ApiResponse<CurrentUserDto>>(`${this.apiUrl}/me`, { context }).pipe(
      map(response => {
        if (!response.isSuccess || !response.data) {
          throw new Error(response.errors[0] ?? response.message ?? 'Unable to verify the current identity.');
        }
        return response.data;
      }),
      tap(user => this.setVerifiedUser(user))
    );
  }

  logout(): void {
    const refreshToken = this.refreshTokenValue;
    if (refreshToken) {
      this.http.post(`${this.apiUrl}/logout`, { refreshToken }).subscribe({ error: () => undefined });
    }
    this.clearAuthData();
    void this.router.navigate(['/auth/school-login']);
  }

  getSchools(): Observable<ApiResponse<SchoolLookup[]>> {
    return this.http.get<ApiResponse<SchoolLookup[]>>(`${this.apiUrl}/schools`);
  }

  changePassword(currentPassword: string, newPassword: string): Observable<ApiResponse<void>> {
    return this.http.post<ApiResponse<void>>(`${this.apiUrl}/change-password`, {
      currentPassword,
      newPassword
    });
  }

  getAccessToken(): string | null {
    return this.accessToken;
  }

  getRefreshToken(): string | null {
    return this.refreshTokenValue;
  }

  getAcceptLanguage(): 'ar-SA' | 'en-US' {
    return this.currentUserState()?.preferredLanguage?.toLowerCase().startsWith('en') ? 'en-US' : 'ar-SA';
  }

  hasRole(role: string): boolean {
    return this.roles().includes(role);
  }

  hasPermission(permission: string): boolean {
    return this.permissions().includes(permission);
  }

  hasAnyRole(roles: readonly string[]): boolean {
    return roles.some(role => this.hasRole(role));
  }

  hasAllPermissions(permissions: readonly string[]): boolean {
    return permissions.every(permission => this.hasPermission(permission));
  }

  hasAnyPermission(permissions: readonly string[]): boolean {
    return permissions.some(permission => this.hasPermission(permission));
  }

  /** Decodes claims for bootstrap/fallback display only; /auth/me remains authoritative. */
  decodeToken(token: string): JwtClaims | null {
    try {
      const payload = token.split('.')[1];
      if (!payload) return null;
      const normalized = payload.replace(/-/g, '+').replace(/_/g, '/');
      const padded = normalized.padEnd(Math.ceil(normalized.length / 4) * 4, '=');
      const bytes = Uint8Array.from(atob(padded), character => character.charCodeAt(0));
      return JSON.parse(new TextDecoder().decode(bytes)) as JwtClaims;
    } catch {
      return null;
    }
  }

  clearAuthData(): void {
    this.accessToken = null;
    this.refreshTokenValue = null;
    this.currentUserState.set(null);
    this.removeSession(ACCESS_TOKEN_KEY);
    this.removeSession(REFRESH_TOKEN_KEY);
    this.removeSession(USER_KEY);

    // Remove tokens written by the former persistent-storage implementation.
    if (typeof window !== 'undefined') {
      window.localStorage.removeItem(ACCESS_TOKEN_KEY);
      window.localStorage.removeItem(REFRESH_TOKEN_KEY);
      window.localStorage.removeItem(USER_KEY);
    }
  }

  private authenticate(
    url: string,
    request: SchoolLoginRequestDto | MainManagerLoginRequest
  ): Observable<ApiResponse<AuthResponseDto>> {
    return this.http.post<ApiResponse<AuthResponseDto>>(url, request).pipe(
      tap(response => this.acceptAuthResponse(response)),
      switchMap(response => {
        if (!response.isSuccess || !response.data) return of(response);
        return this.loadCurrentIdentity().pipe(map(() => response));
      })
    );
  }

  private acceptAuthResponse(response: ApiResponse<AuthResponseDto>): void {
    if (!response.isSuccess || !response.data) return;

    this.accessToken = response.data.accessToken;
    this.refreshTokenValue = response.data.refreshToken;
    this.currentUserState.set(response.data.user);
    this.writeSession(ACCESS_TOKEN_KEY, response.data.accessToken);
    this.writeSession(REFRESH_TOKEN_KEY, response.data.refreshToken);
    this.writeSession(USER_KEY, JSON.stringify(response.data.user));
  }

  private setVerifiedUser(user: CurrentUserDto): void {
    this.currentUserState.set(user);
    this.writeSession(USER_KEY, JSON.stringify(user));
  }

  private restoreUser(): CurrentUserDto | null {
    const stored = this.readSession(USER_KEY);
    if (stored) {
      try {
        return JSON.parse(stored) as CurrentUserDto;
      } catch {
        this.removeSession(USER_KEY);
      }
    }

    const claims = this.accessToken ? this.decodeToken(this.accessToken) : null;
    if (!claims) return null;

    return {
      userId: claims.sub ?? '',
      username: claims.unique_name ?? '',
      fullName: claims.unique_name ?? '',
      preferredLanguage: claims.preferred_language ?? 'ar',
      activeSchoolId: claims.active_school_id ? Number(claims.active_school_id) : undefined,
      roles: this.asStringArray(claims[ROLE_CLAIM] ?? claims.role),
      permissions: this.asStringArray(claims.permission)
    };
  }

  private isTokenExpired(token: string): boolean {
    const expiry = this.decodeToken(token)?.exp;
    return !expiry || expiry * 1000 <= Date.now() + 5_000;
  }

  private asStringArray(value: string | readonly string[] | undefined): string[] {
    if (!value) return [];
    return Array.isArray(value) ? [...value] : [value as string];
  }

  private readSession(key: string): string | null {
    if (typeof window === 'undefined') return null;
    return window.sessionStorage.getItem(key);
  }

  private writeSession(key: string, value: string): void {
    if (typeof window !== 'undefined') window.sessionStorage.setItem(key, value);
  }

  private removeSession(key: string): void {
    if (typeof window !== 'undefined') window.sessionStorage.removeItem(key);
  }
}
