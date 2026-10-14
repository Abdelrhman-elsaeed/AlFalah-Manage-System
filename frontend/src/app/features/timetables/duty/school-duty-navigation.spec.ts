import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, provideRouter } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { SignatureEnrollmentService } from '../../../core/services/signature-enrollment.service';
import { signatureEnrollmentGuard } from '../../../core/guards/signature-enrollment.guard';
import { legacySchoolDutyGuard, schoolDutyAccessGuard, schoolDutyUnsavedGuard } from '../../../core/guards/school-duty-navigation.guard';

describe('Independent duty navigation', () => {
  let school: number | undefined;
  let permissions: string[];
  let roles: string[];
  let router: Router;
  const route = () => new ActivatedRouteSnapshot();
  beforeEach(() => {
    school = 1; permissions = ['Duty.View']; roles = ['Instructor'];
    TestBed.configureTestingModule({ providers: [provideRouter([]),
      { provide: AuthService, useValue: {
        activeSchoolId: () => school, hasAnyPermission: (required: string[]) => required.some(x => permissions.includes(x)),
        hasAnyRole: (required: string[]) => required.some(x => roles.includes(x)), hasRole: (role: string) => roles.includes(role)
      } },
      { provide: TranslateService, useValue: { instant: () => 'Leave unsaved changes?' } },
      { provide: SignatureEnrollmentService, useValue: { requirement: () => of({ isSuccess: true, data: { required: true } }) } }
    ] });
    router = TestBed.inject(Router);
  });
  it('allows own duty with Duty.View without requiring timetable administration', () => {
    expect(TestBed.runInInjectionContext(() => schoolDutyAccessGuard(route(), {} as any))).toBeTrue();
  });
  it('allows the duty manager through the existing duty capability', () => {
    roles = ['SchoolManager']; permissions = ['Duty.Manage'];
    expect(TestBed.runInInjectionContext(() => schoolDutyAccessGuard(route(), {} as any))).toBeTrue();
  });
  it('denies a session without an active school', () => {
    school = undefined;
    const denied = TestBed.runInInjectionContext(() => schoolDutyAccessGuard(route(), {} as any));
    expect(router.serializeUrl(denied as any)).toBe('/unauthorized');
  });
  it('does not use timetable permission as a duty grant', () => {
    permissions = ['Timetable.View'];
    const denied = TestBed.runInInjectionContext(() => schoolDutyAccessGuard(route(), {} as any));
    expect(router.serializeUrl(denied as any)).toBe('/unauthorized');
  });
  it('retains signed-document/date context and fragment when opening an old duty link', () => {
    const old = route(); old.queryParams = { tab: 'duty', date: '2026-10-14', execution: '42' }; old.fragment = 'documents';
    const target = TestBed.runInInjectionContext(() => legacySchoolDutyGuard(old, {} as any));
    expect(router.serializeUrl(target as any)).toBe('/school-duty/execution?date=2026-10-14&execution=42#documents');
  });
  it('keeps a secretary on the lesson timetable instead of granting duty access', () => {
    roles = ['Secretary']; const old = route(); old.queryParams = { tab: 'duty' };
    expect(TestBed.runInInjectionContext(() => legacySchoolDutyGuard(old, {} as any))).toBeTrue();
  });
  it('preserves unsaved duty work when leaving is declined', () => {
    spyOn(window, 'confirm').and.returnValue(false);
    expect(TestBed.runInInjectionContext(() => schoolDutyUnsavedGuard({ hasDraft: () => true }, route(), {} as any, {} as any))).toBeFalse();
  });
  it('leaves saved duty work without an unnecessary confirmation', () => {
    const confirm = spyOn(window, 'confirm');
    expect(TestBed.runInInjectionContext(() => schoolDutyUnsavedGuard({ hasDraft: () => false }, route(), {} as any, {} as any))).toBeTrue(); expect(confirm).not.toHaveBeenCalled();
  });
  it('lets a teacher reach own duty before saved-signature setup', () => {
    expect(TestBed.runInInjectionContext(() => signatureEnrollmentGuard(route(), { url: '/school-duty?execution=42' } as any))).toBeTrue();
  });
});
