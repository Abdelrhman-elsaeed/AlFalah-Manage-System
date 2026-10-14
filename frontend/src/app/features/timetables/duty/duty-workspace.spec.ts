import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, provideRouter } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import { AuthService } from '../../../core/services/auth.service';
import { schoolDutyEntryGuard, schoolDutyDelegationGuard, schoolDutyManagerGuard, schoolDutySectionGuard, schoolDutyUnsavedGuard } from '../../../core/guards/school-duty-navigation.guard';
import { DutyFeedbackService } from './duty-feedback.service';
import { signatureEnrollmentGuard } from '../../../core/guards/signature-enrollment.guard';

describe('Ordered duty workspace and explicit error acknowledgement', () => {
  let language: string;
  let role: string;
  let school: number | undefined;
  let manage: boolean;
  beforeEach(() => {
    language = 'ar'; role = 'SchoolManager'; school = 1; manage = true;
    TestBed.configureTestingModule({ providers: [provideRouter([]),
      { provide: TranslateService, useValue: { get currentLang() { return language; }, instant: () => 'Leave draft?' } },
      { provide: AuthService, useValue: { activeSchoolId: () => school, hasRole: (expected: string) => role === expected,
        hasAnyPermission: (permissions: string[]) => manage && permissions.includes('Duty.Manage') } }
    ] });
  });
  it('keeps every duty page reachable without forced signature enrollment', () => {
    role='Instructor';
    for(const url of ['/school-duty','/school-duty/execution?execution=1','/school-duty/documents?execution=1'])
      expect(TestBed.runInInjectionContext(()=>signatureEnrollmentGuard({} as any,{url} as any))).toBeTrue();
  });
  it('duty settings/statistics/history follow the school capability regardless of role label', () => {
    expect(TestBed.runInInjectionContext(() => schoolDutyManagerGuard({} as any, {} as any))).toBeTrue();
    role = 'Instructor';expect(TestBed.runInInjectionContext(() => schoolDutyManagerGuard({} as any, {} as any))).toBeTrue();
    manage=false; const denied = TestBed.runInInjectionContext(() => schoolDutyManagerGuard({} as any, {} as any));
    expect(TestBed.inject(Router).serializeUrl(denied as any)).toBe('/unauthorized');
  });
  it('does not grant management through a role alone or a missing school', () => {
    manage = false; expect(TestBed.runInInjectionContext(() => schoolDutyManagerGuard({} as any, {} as any))).not.toBeTrue();
    manage = true; school = undefined; expect(TestBed.runInInjectionContext(() => schoolDutyManagerGuard({} as any, {} as any))).not.toBeTrue();
  });
  it('lets only the school manager open delegation even when another account can manage duty',()=>{
    expect(TestBed.runInInjectionContext(()=>schoolDutyDelegationGuard({} as any,{} as any))).toBeTrue();
    role='Moderator';expect(TestBed.runInInjectionContext(()=>schoolDutyDelegationGuard({} as any,{} as any))).not.toBeTrue();
  });
  it('old execution links retain identity on the separate execution page', () => {
    const route = new ActivatedRouteSnapshot(); route.queryParams = { execution: '42', date: '2030-01-06', termId: '7' }; route.fragment = 'history';
    const target = TestBed.runInInjectionContext(() => schoolDutyEntryGuard(route, {} as any));
    expect(TestBed.inject(Router).serializeUrl(target as any)).toBe('/school-duty/execution?execution=42&date=2030-01-06&termId=7#history');
  });
  it('does not turn plain schedule links into execution reads', () => {
    expect(TestBed.runInInjectionContext(() => schoolDutyEntryGuard(new ActivatedRouteSnapshot(), {} as any))).toBeTrue();
  });
  it('prevents page disposal during an in-flight save or signing operation', () => {
    expect(schoolDutySectionGuard({ canLeave: () => false }, {} as any, {} as any, {} as any)).toBeFalse();
    expect(schoolDutySectionGuard({ canLeave: () => true }, {} as any, {} as any, {} as any)).toBeTrue();
  });
  it('never prevents logout or permission-denied navigation with a draft prompt', () => {
    const confirm = spyOn(window, 'confirm'); const dirty = { hasDraft: () => true };
    expect(TestBed.runInInjectionContext(() => schoolDutyUnsavedGuard(dirty, {} as any, {} as any, { url: '/auth/school-login' } as any))).toBeTrue();
    expect(TestBed.runInInjectionContext(() => schoolDutyUnsavedGuard(dirty, {} as any, {} as any, { url: '/unauthorized' } as any))).toBeTrue();
    expect(confirm).not.toHaveBeenCalled();
  });
  it('shows only the selected language from a bilingual administrative error', () => {
    const feedback = TestBed.inject(DutyFeedbackService);
    feedback.show('أكّد الفئات الخالية. / Confirm empty categories.'); expect(feedback.current()!.message).toBe('أكّد الفئات الخالية.');
    feedback.clear(); language = 'en'; feedback.show('أكّد الفئات الخالية. / Confirm empty categories.'); expect(feedback.current()!.message).toBe('Confirm empty categories.');
  });
  it('preserves ordinary slashes in names, dates and error text', () => {
    const feedback = TestBed.inject(DutyFeedbackService); feedback.show('2030/01/06 — Teacher / classroom');
    expect(feedback.current()!.message).toBe('2030/01/06 — Teacher / classroom');
  });
  it('updates one source without multiplying its errors and acknowledges once', () => {
    const feedback = TestBed.inject(DutyFeedbackService), source = {}; const dismissed = jasmine.createSpy();
    feedback.show('First', dismissed, source); feedback.show('Second', dismissed, source);
    expect(feedback.current()!.message).toBe('Second'); feedback.dismiss(); expect(dismissed).toHaveBeenCalledTimes(1); expect(feedback.current()).toBeNull();
  });
  it('retains independent errors until each is acknowledged', () => {
    const feedback = TestBed.inject(DutyFeedbackService); feedback.show('Save failed', () => {}, {}); feedback.show('Download failed', () => {}, {});
    feedback.dismiss(); expect(feedback.current()!.message).toBe('Download failed'); feedback.dismiss(); expect(feedback.current()).toBeNull();
  });
  it('does not keep errors from a disposed document/picker context', () => {
    const feedback = TestBed.inject(DutyFeedbackService), source = {}; feedback.show('First', () => {}, source); feedback.show('Second', () => {}, {});
    feedback.forget(source); expect(feedback.current()!.message).toBe('Second'); feedback.clear(); expect(feedback.current()).toBeNull();
  });
});
