import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';
import { authInterceptor } from '../interceptors/auth.interceptor';
import { ToastService } from './toast.service';
import { TranslateService } from '@ngx-translate/core';

describe('AuthService session persistence', () => {
  const keys = ['alfalah_access_token', 'alfalah_refresh_token', 'alfalah_user'];
  const user = { userId: 'session-test', username: 'guardian', fullName: 'Guardian', preferredLanguage: 'ar', activeSchoolId: 1, roles: ['Guardian'], permissions: [] };
  const token = (expiry: number) => `e30.${btoa(JSON.stringify({ sub: user.userId, exp: expiry }))}.fixture`;
  const access = () => token(Math.floor(Date.now() / 1000) + 3600);
  const response = (accessToken = access()) => ({ isSuccess: true, errors: [], data: { accessToken, refreshToken: 'rotated-refresh', user } });
  let http: HttpTestingController;
  const router = { url: '/', navigate: jasmine.createSpy().and.resolveTo(true) };

  beforeEach(() => {
    for (const key of keys) { localStorage.removeItem(key); sessionStorage.removeItem(key); }
    TestBed.configureTestingModule({ providers: [
      provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(),
      { provide: Router, useValue: router },
      { provide: ToastService, useValue: { error: () => undefined, warn: () => undefined } },
      { provide: TranslateService, useValue: { instant: (key: string) => key } }
    ] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => {
    http.verify();
    for (const key of keys) { localStorage.removeItem(key); sessionStorage.removeItem(key); }
  });
  function store(storage: Storage, accessToken = access()) {
    storage.setItem(keys[0], accessToken);
    storage.setItem(keys[1], 'saved-refresh');
    storage.setItem(keys[2], JSON.stringify(user));
  }
  function verifyIdentity() { http.expectOne(r => r.url.endsWith('/auth/me')).flush({ isSuccess: true, data: user }); }

  it('restores a closed app session after all tab storage is lost', () => {
    store(localStorage);
    const auth = TestBed.inject(AuthService);
    auth.bootstrapSession().subscribe();
    verifyIdentity();
    expect(auth.isAuthenticated()).toBeTrue();
    expect(auth.currentUser()?.userId).toBe(user.userId);
  });
  it('saves a successful login durably without storing a password', () => {
    const auth = TestBed.inject(AuthService);
    auth.schoolLogin({ schoolId: 1, username: 'guardian', password: 'test-password' }).subscribe();
    http.expectOne(r => r.url.endsWith('/school-login')).flush(response());
    verifyIdentity();
    expect(localStorage.getItem(keys[1])).toBe('rotated-refresh');
    expect(localStorage.getItem(keys[2])).not.toContain('test-password');
    expect(sessionStorage.getItem(keys[1])).toBeNull();
  });
  it('migrates an existing tab session so an update does not force another login', () => {
    store(sessionStorage);
    const auth = TestBed.inject(AuthService);
    auth.bootstrapSession().subscribe();
    verifyIdentity();
    expect(localStorage.getItem(keys[1])).toBe('saved-refresh');
    expect(sessionStorage.getItem(keys[1])).toBeNull();
  });
  it('refreshes an expired saved access token and verifies the authoritative identity', () => {
    store(localStorage, token(1));
    const auth = TestBed.inject(AuthService);
    auth.bootstrapSession().subscribe();
    const refresh = http.expectOne(r => r.url.endsWith('/auth/refresh'));
    expect(refresh.request.body).toEqual({ refreshToken: 'saved-refresh' });
    refresh.flush(response());
    verifyIdentity();
    expect(auth.isAuthenticated()).toBeTrue();
    expect(localStorage.getItem(keys[1])).toBe('rotated-refresh');
  });
  it('clears saved credentials when the server rejects the session', () => {
    store(localStorage);
    const auth = TestBed.inject(AuthService);
    auth.bootstrapSession().subscribe();
    http.expectOne(r => r.url.endsWith('/auth/me')).flush({}, { status: 401, statusText: 'Unauthorized' });
    http.expectOne(r => r.url.endsWith('/auth/refresh')).flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(auth.isAuthenticated()).toBeFalse();
    for (const key of keys) expect(localStorage.getItem(key)).toBeNull();
  });
  for (const status of [0, 503]) {
    it(`keeps credentials for retry after a temporary startup failure (${status})`, () => {
      store(localStorage);
      const auth = TestBed.inject(AuthService);
      auth.bootstrapSession().subscribe();
      http.expectOne(r => r.url.endsWith('/auth/me')).flush(null, { status, statusText: 'Unavailable' });
      expect(auth.isAuthenticated()).toBeFalse();
      expect(localStorage.getItem(keys[1])).toBe('saved-refresh');
      auth.bootstrapSession().subscribe();
      verifyIdentity();
      expect(auth.isAuthenticated()).toBeTrue();
    });
  }
  it('keeps the refresh credential when automatic renewal meets a server outage', () => {
    store(localStorage);
    const auth = TestBed.inject(AuthService);
    auth.bootstrapSession().subscribe();
    http.expectOne(r => r.url.endsWith('/auth/me')).flush({}, { status: 401, statusText: 'Unauthorized' });
    http.expectOne(r => r.url.endsWith('/auth/refresh')).flush(null, { status: 503, statusText: 'Unavailable' });
    expect(auth.isAuthenticated()).toBeFalse();
    expect(localStorage.getItem(keys[1])).toBe('saved-refresh');
  });
  it('does not combine legacy tab credentials with another saved account', () => {
    store(localStorage);
    store(sessionStorage, token(1));
    sessionStorage.setItem(keys[1], 'old-account-refresh');
    const auth = TestBed.inject(AuthService);
    auth.bootstrapSession().subscribe();
    verifyIdentity();
    expect(auth.getRefreshToken()).toBe('saved-refresh');
    expect(sessionStorage.getItem(keys[1])).toBeNull();
  });
  it('drops the active identity when another tab logs out', () => {
    store(localStorage);
    const auth = TestBed.inject(AuthService);
    expect(auth.isAuthenticated()).toBeTrue();
    for (const key of keys) localStorage.removeItem(key);
    window.dispatchEvent(new StorageEvent('storage', { key: keys[2], storageArea: localStorage }));
    expect(auth.isAuthenticated()).toBeFalse();
    expect(auth.getAccessToken()).toBeNull();
    expect(auth.getRefreshToken()).toBeNull();
  });
  it('picks up token rotation from another tab', () => {
    store(localStorage);
    const auth = TestBed.inject(AuthService);
    localStorage.setItem(keys[1], 'other-tab-rotated-refresh');
    window.dispatchEvent(new StorageEvent('storage', { key: keys[1], storageArea: localStorage }));
    expect(auth.getRefreshToken()).toBe('other-tab-rotated-refresh');
  });
  it('clears both storage locations on explicit logout after device cleanup', async () => {
    store(localStorage);
    const auth = TestBed.inject(AuthService);
    store(sessionStorage);
    const cleanup = jasmine.createSpy().and.resolveTo(undefined);
    auth.beforeLogout = cleanup;
    await auth.logout();
    expect(cleanup).toHaveBeenCalledTimes(1);
    http.expectOne(r => r.url.endsWith('/auth/logout')).flush({ isSuccess: true });
    for (const key of keys) {
      expect(localStorage.getItem(key)).toBeNull();
      expect(sessionStorage.getItem(key)).toBeNull();
    }
    expect(auth.isAuthenticated()).toBeFalse();
  });
});
