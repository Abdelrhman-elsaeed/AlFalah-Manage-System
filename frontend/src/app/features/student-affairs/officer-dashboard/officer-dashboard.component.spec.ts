import { TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { ApiResponse } from '../../../core/models/api-response.model';
import { OfficerStudentAffairsDashboardDto } from '../../../core/models/student-affairs-dashboard.models';
import { StudentAffairsDashboardService } from '../../../core/services/student-affairs-dashboard.service';
import { OfficerDashboardComponent } from './officer-dashboard.component';

describe('OfficerDashboardComponent operational states', () => {
  it('visually separates new, incomplete, pending and completed work', async () => {
    const dashboard: OfficerStudentAffairsDashboardDto = {
      queues: [
        { code: 'PendingExcuses', label: 'أعذار قيد المراجعة', count: 2, severity: 'warning' },
        { code: 'UnreadOfficerThreads', label: 'محادثات غير مقروءة', count: 3, severity: 'warning' },
        { code: 'UnassignedReferrals', label: 'إحالات غير مسندة', count: 1, severity: 'warning' },
        { code: 'AutomationReviews', label: 'مراجعات الأتمتة', count: 0, severity: 'info' }
      ],
      thresholdAlerts: []
    };
    const api = jasmine.createSpyObj<StudentAffairsDashboardService>('StudentAffairsDashboardService', ['getOfficerDashboard', 'getSchoolOversightDashboard']);
    api.getOfficerDashboard.and.returnValue(of(success(dashboard)));

    await TestBed.configureTestingModule({
      imports: [OfficerDashboardComponent, NoopAnimationsModule],
      providers: [
        { provide: StudentAffairsDashboardService, useValue: api },
        { provide: ActivatedRoute, useValue: { snapshot: { data: { dashboardKind: 'officer' } } } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(OfficerDashboardComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('[data-state="new"]').length).toBe(1);
    expect(fixture.nativeElement.querySelectorAll('[data-state="incomplete"]').length).toBe(1);
    expect(fixture.nativeElement.querySelectorAll('[data-state="attention"]').length).toBe(1);
    expect(fixture.nativeElement.querySelectorAll('[data-state="complete"]').length).toBe(1);
  });
});

function success<T>(data: T): ApiResponse<T> {
  return { isSuccess: true, message: '', errors: [], data };
}
