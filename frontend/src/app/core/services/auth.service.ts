import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap, catchError, throwError } from 'rxjs';
import {
  AuthResponse,
  CurrentUser,
  MainManagerLoginRequest,
  SchoolLoginRequest,
  SchoolLookup,
  UserTokenInfo
} from '../models/auth.models';
import { ApiResponse } from '../models/api-response.model';
import { environment } from '../../../environments/environment';

const ACCESS_TOKEN_KEY = 'alfalah_access_token';
const REFRESH_TOKEN_KEY = 'alfalah_refresh_token';
const USER_KEY = 'alfalah_user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly apiUrl = `${environment.apiUrl}/api/v1/auth`;

  // Reactive user state
  private _currentUser = signal<UserTokenInfo | null>(this.getUserFromStorage());
  readonly currentUser = this._currentUser.asReadonly();
  readonly isAuthenticated = computed(() => !!this._currentUser());
  readonly roles = computed(() => this._currentUser()?.roles ?? []);
  readonly permissions = computed(() => this._currentUser()?.permissions ?? []);
  readonly activeSchoolId = computed(() => this._currentUser()?.activeSchoolId);

  constructor(private http: HttpClient, private router: Router) {}

  // ─── School Login ─────────────────────────────────────────────────────────

  schoolLogin(request: SchoolLoginRequest): Observable<ApiResponse<AuthResponse>> {
    return this.http.post<ApiResponse<AuthResponse>>(
      `${this.apiUrl}/school-login`, request
    ).pipe(
      tap(response => {
        if (response.isSuccess && response.data) {
          this.saveAuthData(response.data);
        }
      }),
      catchError(err => throwError(() => err))
    );
  }

  // ─── Main Manager Login ───────────────────────────────────────────────────

  mainManagerLogin(request: MainManagerLoginRequest): Observable<ApiResponse<AuthResponse>> {
    return this.http.post<ApiResponse<AuthResponse>>(
      `${this.apiUrl}/main-manager-login`, request
    ).pipe(
      tap(response => {
        if (response.isSuccess && response.data) {
          this.saveAuthData(response.data);
        }
      }),
      catchError(err => throwError(() => err))
    );
  }

  // ─── Refresh Token ────────────────────────────────────────────────────────

  refreshToken(): Observable<ApiResponse<AuthResponse>> {
    const refreshToken = this.getRefreshToken();
    if (!refreshToken) return throwError(() => new Error('No refresh token'));

    return this.http.post<ApiResponse<AuthResponse>>(
      `${this.apiUrl}/refresh`, { refreshToken }
    ).pipe(
      tap(response => {
        if (response.isSuccess && response.data) {
          this.saveAuthData(response.data);
        }
      })
    );
  }

  // ─── Logout ───────────────────────────────────────────────────────────────

  logout(): void {
    const refreshToken = this.getRefreshToken();
    if (refreshToken) {
      this.http.post(`${this.apiUrl}/logout`, { refreshToken }).subscribe({
        error: () => {} // Swallow errors on logout
      });
    }
    this.clearAuthData();
    this.router.navigate(['/auth/school-login']);
  }

  // ─── Schools Lookup ───────────────────────────────────────────────────────

  getSchools(): Observable<ApiResponse<SchoolLookup[]>> {
    return this.http.get<ApiResponse<SchoolLookup[]>>(`${this.apiUrl}/schools`);
  }

  // ─── Token Accessors ──────────────────────────────────────────────────────

  getAccessToken(): string | null {
    return localStorage.getItem(ACCESS_TOKEN_KEY);
  }

  getRefreshToken(): string | null {
    return localStorage.getItem(REFRESH_TOKEN_KEY);
  }

  // ─── Role/Permission Checks ───────────────────────────────────────────────

  hasRole(role: string): boolean {
    return this.roles().includes(role);
  }

  hasPermission(permission: string): boolean {
    return this.permissions().includes(permission);
  }

  hasAnyRole(roles: string[]): boolean {
    return roles.some(r => this.roles().includes(r));
  }

  // ─── Private Helpers ──────────────────────────────────────────────────────

  private saveAuthData(authResponse: AuthResponse): void {
    localStorage.setItem(ACCESS_TOKEN_KEY, authResponse.accessToken);
    localStorage.setItem(REFRESH_TOKEN_KEY, authResponse.refreshToken);
    localStorage.setItem(USER_KEY, JSON.stringify(authResponse.user));
    this._currentUser.set(authResponse.user);
  }

  /**
   * Clears auth state from storage and the reactive signal.
   * Public so the ErrorInterceptor can wipe a stale session before redirecting
   * to login (logout() also navigates, which isn't always desired here).
   */
  clearAuthData(): void {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this._currentUser.set(null);
  }

  private getUserFromStorage(): UserTokenInfo | null {
    try {
      const stored = localStorage.getItem(USER_KEY);
      return stored ? JSON.parse(stored) : null;
    } catch {
      return null;
    }
  }
}
