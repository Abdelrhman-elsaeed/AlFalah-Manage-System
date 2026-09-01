import { FormBuilder } from '@angular/forms';
import { of } from 'rxjs';
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
