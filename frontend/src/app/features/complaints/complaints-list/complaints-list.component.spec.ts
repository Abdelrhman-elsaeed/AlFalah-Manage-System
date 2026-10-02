import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { TranslateModule } from '@ngx-translate/core';
import { of } from 'rxjs';
import { ApiResponse } from '../../../core/models/api-response.model';
import { AuthService } from '../../../core/services/auth.service';
import { ComplaintsService } from '../../../core/services/complaints.service';
import { ToastService } from '../../../core/services/toast.service';
import { VisitsService } from '../../../core/services/visits.service';
import { VisitsV2Service } from '../../../core/services/visits-v2.service';
import { ComplaintsListComponent } from './complaints-list.component';

describe('ComplaintsListComponent instructor workspace', () => {
  let complaints: jasmine.SpyObj<ComplaintsService>;
  let visits: jasmine.SpyObj<VisitsService>;
  let visitsV2: jasmine.SpyObj<VisitsV2Service>;

  beforeEach(async () => {
    complaints = jasmine.createSpyObj<ComplaintsService>('ComplaintsService', [
      'list', 'create', 'updateStatus', 'reopenVisit', 'delete'
    ]);
    visits = jasmine.createSpyObj<VisitsService>('VisitsService', [
      'listMyApprovedReports', 'getInstructorReport'
    ]);
    visitsV2 = jasmine.createSpyObj<VisitsV2Service>('VisitsV2Service', ['list', 'get']);

    complaints.list.and.returnValue(of(success([])));
    complaints.create.and.returnValue(of(success({} as any)));
    visits.listMyApprovedReports.and.returnValue(of(success({
      items: [{
        id: 3025,
        visitCategoryLabelAr: 'زيارة صفية أو دورية',
        visitSequenceLabelAr: 'أولى',
        visitDate: '2026-09-21T08:00:00Z',
        statusLabelAr: 'معتمدة'
      }],
      page: 1,
      pageSize: 100,
      totalCount: 1,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false
    } as any)));
    visits.getInstructorReport.and.returnValue(of(success({ visitId: 3025 } as any)));
    visitsV2.list.and.returnValue(of(success({ page: { items: [], totalCount: 0 } } as any)));

    const auth = {
      roles: signal<readonly string[]>(['Instructor']),
      hasPermission: (permission: string) => ['Complaint.View', 'Complaint.Create'].includes(permission)
    };
    const toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error', 'warn']);

    await TestBed.configureTestingModule({
      imports: [ComplaintsListComponent, RouterTestingModule, TranslateModule.forRoot()],
      providers: [
        { provide: ComplaintsService, useValue: complaints },
        { provide: VisitsService, useValue: visits },
        { provide: VisitsV2Service, useValue: visitsV2 },
        { provide: AuthService, useValue: auth },
        { provide: ToastService, useValue: toast }
      ]
    }).compileComponents();
  });

  it('combines complaint submission and results under two tabs on one route', () => {
    const fixture = TestBed.createComponent(ComplaintsListComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('.complaint-tabs [role="tab"]').length).toBe(2);
    expect(fixture.nativeElement.querySelectorAll('.report-card').length).toBe(1);

    fixture.componentInstance.prepareComplaint(fixture.componentInstance.reports()[0]);
    fixture.detectChanges();

    expect(visits.getInstructorReport).toHaveBeenCalledOnceWith(3025);
    expect(fixture.nativeElement.querySelector('.complaint-compose')).not.toBeNull();

    fixture.componentInstance.complaintSubject = 'طلب مراجعة الدرجة';
    fixture.componentInstance.complaintBody = 'أرجو مراجعة تفاصيل التقرير المعتمد.';
    fixture.componentInstance.submitComplaint();
    fixture.detectChanges();

    expect(complaints.create).toHaveBeenCalledOnceWith(3025, {
      subject: 'طلب مراجعة الدرجة',
      body: 'أرجو مراجعة تفاصيل التقرير المعتمد.'
    });
    expect(fixture.componentInstance.activeTab()).toBe('results');
    expect(complaints.list).toHaveBeenCalledTimes(2);
  });
});

function success<T>(data: T): ApiResponse<T> {
  return { isSuccess: true, message: '', errors: [], data };
}
