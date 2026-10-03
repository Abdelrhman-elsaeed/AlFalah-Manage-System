import { HttpErrorResponse } from '@angular/common/http';
import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { ApiResponse } from '../../../core/models/api-response.model';
import { GuardianStudentAffairsDashboardDto } from '../../../core/models/student-affairs-dashboard.models';
import { AuthService } from '../../../core/services/auth.service';
import { StudentAffairsDashboardService } from '../../../core/services/student-affairs-dashboard.service';
import { GuardianDashboardComponent } from './guardian-dashboard.component';

describe('Guardian dashboard refresh', () => {
  const data: GuardianStudentAffairsDashboardDto = {
    students: [], actions: [], unreadNotifications: 3, unreadThreads: 2, generatedAt: '2026-10-03T10:00:00Z'
  };
  const response: ApiResponse<GuardianStudentAffairsDashboardDto> = { isSuccess: true, message: '', errors: [], data };
  let api: jasmine.SpyObj<StudentAffairsDashboardService>;

  beforeEach(() => {
    api = jasmine.createSpyObj('StudentAffairsDashboardService', ['getGuardianDashboard']);
    api.getGuardianDashboard.and.returnValue(of(response));
    TestBed.configureTestingModule({
      imports: [GuardianDashboardComponent],
      providers: [
        { provide: StudentAffairsDashboardService, useValue: api },
        { provide: AuthService, useValue: { hasAllPermissions: () => true, hasPermission: () => true } },
        { provide: ActivatedRoute, useValue: { snapshot: { data: { guardianView: 'overview' } } } }
      ]
    }).overrideComponent(GuardianDashboardComponent, { set: { template: '' } });
  });

  it('keeps the displayed data during refresh and blocks overlapping requests', () => {
    const fixture = TestBed.createComponent(GuardianDashboardComponent);
    const component = fixture.componentInstance;
    const refresh = new Subject<ApiResponse<GuardianStudentAffairsDashboardDto>>();
    api.getGuardianDashboard.and.returnValue(refresh);
    component.load();
    component.load();
    expect(component.loading()).toBeFalse();
    expect(component.refreshing()).toBeTrue();
    expect(component.dashboard()).toBe(data);
    expect(api.getGuardianDashboard).toHaveBeenCalledTimes(2);
    refresh.next({ ...response, data: { ...data, unreadThreads: 4 } });
    refresh.complete();
    expect(component.dashboard()?.unreadThreads).toBe(4);
    expect(component.refreshing()).toBeFalse();
    fixture.destroy();
  });

  it('preserves the last successful result after a failed refresh and allows retry', () => {
    const fixture = TestBed.createComponent(GuardianDashboardComponent);
    const component = fixture.componentInstance;
    api.getGuardianDashboard.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
    component.load();
    expect(component.dashboard()).toBe(data);
    expect(component.errorMessage()).not.toBe('');
    expect(component.refreshing()).toBeFalse();
    api.getGuardianDashboard.and.returnValue(of(response));
    component.load();
    expect(component.errorMessage()).toBe('');
    fixture.destroy();
  });

  it('refreshes every minute while visible and stops after leaving the page', fakeAsync(() => {
    spyOnProperty(document, 'hidden', 'get').and.returnValue(false);
    const fixture = TestBed.createComponent(GuardianDashboardComponent);
    tick(60_000);
    expect(api.getGuardianDashboard).toHaveBeenCalledTimes(2);
    fixture.destroy();
    tick(60_000);
    document.dispatchEvent(new Event('visibilitychange'));
    expect(api.getGuardianDashboard).toHaveBeenCalledTimes(2);
  }));

  it('pauses requests in a hidden tab and refreshes when returning', fakeAsync(() => {
    const hidden = spyOnProperty(document, 'hidden', 'get').and.returnValue(true);
    const fixture = TestBed.createComponent(GuardianDashboardComponent);
    tick(60_000);
    expect(api.getGuardianDashboard).toHaveBeenCalledTimes(1);
    hidden.and.returnValue(false);
    document.dispatchEvent(new Event('visibilitychange'));
    expect(api.getGuardianDashboard).toHaveBeenCalledTimes(2);
    fixture.destroy();
  }));

  it('finds names with extra spaces in school records and supports number and class searches', () => {
    const fixture = TestBed.createComponent(GuardianDashboardComponent);
    const component = fixture.componentInstance;
    component.dashboard.set({ ...data, students: [{
      context: {
        student: { id: 1, displayName: 'E2E  Student', studentNumber: 'ST-001', classroomId: 1, classLabel: 'First-A', isActive: true, photoUrl: null },
        activeTerm: null, classroom: null, primaryGuardian: null, metrics: []
      },
      attendance: { officialAbsences: 0, excusedAbsences: 0, pendingExcuses: 0, acceptedExcuses: 0, rejectedExcuses: 0 },
      canSubmitExcuses: true, canRequestGatePass: true, receivesNotifications: true,
      activeGatePasses: 0, activeEntryPermits: 0, pendingOrUpcomingSummons: 0, recentRecognitions: 0, unreadNotifications: 0, unreadThreads: 0
    }] });
    for (const query of [' E2E Student ', 'st-001', 'first-a']) {
      component.search.set(query);
      expect(component.filteredCards().length).withContext(query).toBe(1);
    }
    component.attentionOnly.set(true);
    expect(component.filteredCards().length).toBe(0);
    fixture.destroy();
  });
});
