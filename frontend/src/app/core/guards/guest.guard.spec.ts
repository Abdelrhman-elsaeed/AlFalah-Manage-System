import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of } from 'rxjs';
import { routes } from '../../app.routes';
import { AuthService } from '../services/auth.service';

@Component({ selector: 'test-session-login', standalone: true, template: 'login' })
class LoginPage {}
@Component({ selector: 'test-session-dashboard', standalone: true, template: 'dashboard' })
class DashboardPage {}

describe('Login route session routing', () => {
  for (const path of ['school-login', 'main-manager-login']) {
    it(`opens the existing session instead of the ${path} form`, async () => {
      const auth = { isAuthenticated: () => true, roles: () => ['Guardian'], getRefreshToken: () => null };
      const login = routes.find(r => r.path === 'auth')!.children!.find(r => r.path === path)!;
      TestBed.configureTestingModule({ providers: [
        { provide: AuthService, useValue: auth },
        provideRouter([
          { path: 'auth/' + path, component: LoginPage, canActivate: login.canActivate },
          { path: 'student-affairs/guardian/dashboard', component: DashboardPage }
        ])
      ] });
      const harness = await RouterTestingHarness.create();
      await harness.navigateByUrl('/auth/' + path);
      expect(harness.routeNativeElement?.textContent).toBe('dashboard');
    });
  }
  it('attempts refresh before showing the login form for an expired access token', async () => {
    let authenticated = false;
    const auth = {
      isAuthenticated: () => authenticated, roles: () => ['Guardian'], getRefreshToken: () => 'saved-refresh',
      bootstrapSession: jasmine.createSpy().and.callFake(() => { authenticated = true; return of(undefined); })
    };
    const login = routes.find(r => r.path === 'auth')!.children!.find(r => r.path === 'school-login')!;
    TestBed.configureTestingModule({ providers: [
      { provide: AuthService, useValue: auth },
      provideRouter([
        { path: 'auth/school-login', component: LoginPage, canActivate: login.canActivate },
        { path: 'student-affairs/guardian/dashboard', component: DashboardPage }
      ])
    ] });
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/auth/school-login');
    expect(auth.bootstrapSession).toHaveBeenCalledTimes(1);
    expect(harness.routeNativeElement?.textContent).toBe('dashboard');
  });
  it('keeps the login form available after logout', async () => {
    const auth = { isAuthenticated: () => false, getRefreshToken: () => null };
    const login = routes.find(r => r.path === 'auth')!.children!.find(r => r.path === 'school-login')!;
    TestBed.configureTestingModule({ providers: [
      { provide: AuthService, useValue: auth },
      provideRouter([{ path: 'auth/school-login', component: LoginPage, canActivate: login.canActivate }])
    ] });
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/auth/school-login');
    expect(harness.routeNativeElement?.textContent).toBe('login');
  });
});
