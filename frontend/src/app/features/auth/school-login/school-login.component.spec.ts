import { FormBuilder } from '@angular/forms';
import { of, Subject, throwError } from 'rxjs';
import { ApiResponse } from '../../../core/models/api-response.model';
import { AuthResponseDto } from '../../../core/models/auth.models';
import { SchoolLoginComponent } from './school-login.component';

describe('SchoolLoginComponent secretary routing', () => {
  it('opens the student-attendance sheet after a Secretary login', () => {
    const authResponse: ApiResponse<AuthResponseDto> = {
      isSuccess: true,
      message: '',
      errors: [],
      data: {
        accessToken: 'access-token',
        refreshToken: 'refresh-token',
        accessTokenExpiry: '2026-09-01T12:00:00Z',
        refreshTokenExpiry: '2026-10-01T12:00:00Z',
        user: {
          userId: 'secretary-test',
          username: 'secretary.test',
          fullName: 'Test Secretary',
          preferredLanguage: 'ar',
          activeSchoolId: 18,
          roles: ['Secretary'],
          permissions: ['Attendance.ViewStudents', 'Attendance.ManageStudents']
        }
      }
    };
    const auth = {
      schoolLogin: jasmine.createSpy().and.returnValue(of(authResponse))
    };
    const router = {
      navigate: jasmine.createSpy().and.resolveTo(true)
    };
    const translate = {
      instant: (key: string) => key
    };
    const component = new SchoolLoginComponent(
      new FormBuilder(),
      auth as never,
      router as never,
      translate as never
    );
    component.loginForm.setValue({
      schoolId: 18,
      username: 'secretary.test',
      password: 'Test@1234'
    });

    component.onSubmit();

    expect(router.navigate).toHaveBeenCalledOnceWith(['/student-affairs/attendance/sheet']);
  });
});

describe('SchoolLoginComponent landing separation regressions', () => {
  function setup() {
    const auth = { getSchools: jasmine.createSpy(), schoolLogin: jasmine.createSpy() };
    const router = { navigate: jasmine.createSpy().and.resolveTo(true) };
    const component = new SchoolLoginComponent(new FormBuilder(), auth as never, router as never, { instant: (key: string) => key } as never);
    return { component, auth, router };
  }

  it('keeps school loading and network errors inside the login page', () => {
    const { component, auth } = setup();
    const schools = new Subject();
    auth.getSchools.and.returnValue(schools);
    component.ngOnInit();
    expect(component.schoolsLoading()).toBeTrue();
    schools.next({ isSuccess: true, data: [{ id: 18, name: 'Al-Falah', city: 'Makkah', stage: 'Middle' }] });
    expect(component.schools().length).toBe(1);
    expect(component.schoolsLoading()).toBeFalse();
    schools.error(new Error('offline'));
    expect(component.errorMessage()).toBe('ERRORS.NETWORK_ERROR');
    expect(component.schoolsLoading()).toBeFalse();
  });

  it('does not submit invalid credentials and preserves the original minimum lengths', () => {
    const { component, auth } = setup();
    component.loginForm.setValue({ schoolId: '', username: 'ab', password: 'short' });
    component.onSubmit();
    expect(auth.schoolLogin).not.toHaveBeenCalled();
    expect(component.loginForm.touched).toBeTrue();
    expect(component.usernameControl?.hasError('minlength')).toBeTrue();
    expect(component.passwordControl?.hasError('minlength')).toBeTrue();
  });

  it('shows authentication failures and releases the submit state', () => {
    const { component, auth, router } = setup();
    component.loginForm.setValue({ schoolId: 18, username: 'teacher', password: 'secret123' });
    auth.schoolLogin.and.returnValue(throwError(() => ({ error: { message: 'Invalid credentials' } })));
    component.onSubmit();
    expect(component.loading()).toBeFalse();
    expect(component.errorMessage()).toBe('Invalid credentials');
    expect(router.navigate).not.toHaveBeenCalled();
  });

  for (const [role, destination] of [
    ['SuperAdmin', '/main-manager/dashboard'], ['MainManager', '/main-manager/dashboard'],
    ['SchoolManager', '/school-manager/dashboard'], ['Moderator', '/moderator/dashboard'],
    ['Instructor', '/instructor/dashboard'], ['StudentAffairsOfficer', '/student-affairs/settings'], ['Unknown', '/dashboard']
  ]) {
    it(`preserves the ${role} redirect`, () => {
      const { component, auth, router } = setup();
      auth.schoolLogin.and.returnValue(of({ isSuccess: true, data: { user: { roles: [role] } } }));
      component.loginForm.setValue({ schoolId: '18', username: 'teacher', password: 'secret123' });
      component.onSubmit();
      expect(auth.schoolLogin).toHaveBeenCalledWith({ schoolId: 18, username: 'teacher', password: 'secret123' });
      expect(router.navigate).toHaveBeenCalledOnceWith([destination]);
    });
  }
});
