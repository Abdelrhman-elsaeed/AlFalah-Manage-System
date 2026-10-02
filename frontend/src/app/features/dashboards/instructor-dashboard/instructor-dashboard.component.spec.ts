import { TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { of } from 'rxjs';
import { ApiResponse } from '../../../core/models/api-response.model';
import { InstructorDashboard } from '../../../core/models/dashboard.models';
import { TeacherTopPriorityDto } from '../../../core/models/student-affairs-dashboard.models';
import { DashboardService } from '../../../core/services/dashboard.service';
import { StudentAffairsDashboardService } from '../../../core/services/student-affairs-dashboard.service';
import { InstructorDashboardComponent } from './instructor-dashboard.component';

describe('InstructorDashboardComponent', () => {
  it('uses the compact officer-style workspace without embedding the legacy dashboard', async () => {
    const studentAffairs = jasmine.createSpyObj<StudentAffairsDashboardService>('StudentAffairsDashboardService', ['getTeacherTopPriority']);
    const dashboards = jasmine.createSpyObj<DashboardService>('DashboardService', ['getInstructor']);
    studentAffairs.getTeacherTopPriority.and.returnValue(of(success(priority())));
    dashboards.getInstructor.and.returnValue(of(success(performance())));

    await TestBed.configureTestingModule({
      imports: [InstructorDashboardComponent, RouterTestingModule],
      providers: [
        { provide: StudentAffairsDashboardService, useValue: studentAffairs },
        { provide: DashboardService, useValue: dashboards }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(InstructorDashboardComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.workspace-hero h1').textContent).toContain('لوحة عمل المعلم');
    expect(fixture.nativeElement.querySelectorAll('.metric-card').length).toBe(4);
    expect(fixture.nativeElement.querySelectorAll('.action-card').length).toBe(6);
    expect(fixture.nativeElement.querySelector('app-dashboard-live')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('تاريخ اليوم غير مُعدّ كيوم دراسي');
    expect(fixture.nativeElement.textContent).not.toContain('NonStudyDay');
  });
});

function success<T>(data: T): ApiResponse<T> {
  return { isSuccess: true, message: '', errors: [], data };
}

function priority(): TeacherTopPriorityDto {
  return {
    context: {
      teacher: { userId: 'teacher', displayName: 'E2E Teacher', roleSnapshot: 'Instructor' },
      resolutionKind: 'NonStudyDay',
      resolutionReason: 'The school date is not configured as a study day',
      schoolLocalTime: '2026-10-02T08:00:00+03:00',
      schoolTimeZone: 'Africa/Cairo',
      timetableRevision: 10,
      currentPeriod: null,
      roster: [],
      permittedQuickActions: []
    },
    pendingGatePassAcknowledgements: 0,
    pendingEntryPermitAcknowledgements: 0,
    gatePassAcknowledgements: [],
    entryPermitAcknowledgements: [],
    alerts: []
  };
}

function performance(): InstructorDashboard {
  return {
    instructorUserId: 'teacher',
    instructorFullName: 'E2E Teacher',
    schoolId: 18,
    schoolName: 'Al-Falah E2E Test School',
    latestEvaluation: null,
    performanceTrend: [],
    strengths: [],
    improvementPoints: [],
    openImprovementPlansCount: 0,
    improvementPlansWithFollowUpsCount: 0,
    totalFollowUpsCount: 0,
    latestFollowUpsCount: 0,
    reportViewedCount: 0,
    firstReportViewedAt: null,
    lastReportViewedAt: null,
    approvedVisitsCount: 0,
    appliedFilters: {
      academicYear: null,
      semester: null,
      schoolId: 18,
      schoolName: 'Al-Falah E2E Test School',
      subject: null,
      stage: null,
      moderatorUserId: null,
      moderatorFullName: null
    }
  };
}
