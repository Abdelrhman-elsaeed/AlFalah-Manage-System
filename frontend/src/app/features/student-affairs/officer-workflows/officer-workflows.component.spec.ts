import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { DailyOperationsService } from '../../../core/services/daily-operations.service';
import { OfficerOperationsService } from '../../../core/services/officer-operations.service';
import { ReferralDto } from '../../../core/models/phase5.models';
import { OfficerWorkflowsComponent } from './officer-workflows.component';

describe('OfficerWorkflowsComponent', () => {
  let fixture: ComponentFixture<OfficerWorkflowsComponent>;
  let component: OfficerWorkflowsComponent;
  let api: jasmine.SpyObj<OfficerOperationsService>;
  let directoryApi: jasmine.SpyObj<DailyOperationsService>;

  beforeEach(async () => {
    api = jasmine.createSpyObj<OfficerOperationsService>('OfficerOperationsService', [
      'listReferrals', 'assignableWorkers', 'searchStudents', 'createReferral',
      'createIdempotencyKey', 'assignReferral'
    ]);
    api.listReferrals.and.returnValue(of({
      isSuccess: true, message: '', errors: [],
      data: { items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0, hasNext: false, hasPrevious: false }
    }));
    api.assignableWorkers.and.returnValue(of({
      isSuccess: true, message: '', errors: [], data: [{ userId: 'worker-1', displayName: 'أخصائي المدرسة' }]
    }));
    api.createIdempotencyKey.and.returnValue('referral-idempotency-key');
    api.createReferral.and.returnValue(of({ isSuccess: true, message: '', errors: [], data: {} as never }));
    api.assignReferral.and.returnValue(of({ isSuccess: true, message: '', errors: [], data: {} as never }));
    directoryApi = jasmine.createSpyObj<DailyOperationsService>('DailyOperationsService', ['getClassrooms', 'getStudentsStats']);
    directoryApi.getClassrooms.and.returnValue(of({
      isSuccess: true, message: '', errors: [],
      data: { items: [], totalCount: 0, page: 1, pageSize: 100, totalPages: 0, hasNext: false, hasPrevious: false }
    }));
    directoryApi.getStudentsStats.and.returnValue(of({
      isSuccess: true, message: '', errors: [],
      data: { items: [], totalCount: 0, page: 1, pageSize: 100, totalPages: 0, hasNext: false, hasPrevious: false, totalClassrooms: 1 }
    }));

    await TestBed.configureTestingModule({
      imports: [OfficerWorkflowsComponent],
      providers: [
        MessageService,
        { provide: OfficerOperationsService, useValue: api },
        { provide: DailyOperationsService, useValue: directoryApi },
        { provide: ActivatedRoute, useValue: { snapshot: { data: { workflowMode: 'referrals' } } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(OfficerWorkflowsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads the unassigned referral queue and same-school worker lookup', () => {
    expect(api.listReferrals).toHaveBeenCalledWith(1, 20);
    expect(api.assignableWorkers).toHaveBeenCalled();
    expect(component.workers()[0].userId).toBe('worker-1');
  });

  it('uses one stable idempotency key when creating a manual referral', () => {
    component.referralForm.setValue({
      studentId: 17,
      reason: 'متابعة الغياب',
      priority: 'High',
      workerId: '',
      assignmentReason: ''
    });

    component.createReferral();

    expect(api.createReferral).toHaveBeenCalledOnceWith(
      { studentId: 17, reason: 'متابعة الغياب', source: 'Manual', priority: 'High' },
      'referral-idempotency-key'
    );
  });

  it('can create and assign the referral in one workflow', () => {
    api.createReferral.and.returnValue(of({
      isSuccess: true, message: '', errors: [],
      data: { id: 41, rowVersion: 'referral-v1' } as ReferralDto
    }));
    component.referralForm.setValue({
      studentId: 17,
      reason: 'متابعة سلوكية متكررة',
      priority: 'Critical',
      workerId: 'worker-1',
      assignmentReason: 'بدء التقييم خلال اليوم'
    });

    component.createReferral();

    expect(api.assignReferral).toHaveBeenCalledOnceWith(
      41,
      'worker-1',
      'بدء التقييم خلال اليوم',
      'referral-v1'
    );
    expect(component.saving()).toBeFalse();
  });
});
