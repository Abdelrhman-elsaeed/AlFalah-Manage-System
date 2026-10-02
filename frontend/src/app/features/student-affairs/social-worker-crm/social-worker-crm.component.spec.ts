import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ApiResponse, PagedResult } from '../../../core/models/api-response.model';
import { ReferralDto, SummonDto } from '../../../core/models/phase5.models';
import { AuthService } from '../../../core/services/auth.service';
import { Phase5Service } from '../../../core/services/phase5.service';
import { ToastService } from '../../../core/services/toast.service';
import { SocialWorkerCrmComponent } from './social-worker-crm.component';

describe('SocialWorkerCrmComponent', () => {
  let api: jasmine.SpyObj<Phase5Service>;
  let toast: jasmine.SpyObj<ToastService>;

  beforeEach(async () => {
    api = jasmine.createSpyObj<Phase5Service>('Phase5Service', [
      'getSocialWorkerDashboard', 'listReferrals', 'listSummons', 'getReferral',
      'getStudentGuardians', 'createSummon', 'createConversation', 'createIdempotencyKey',
      'getSummon', 'getSummonHistory', 'startObservation', 'markSummonNoShow', 'acceptReferral'
    ]);
    api.getSocialWorkerDashboard.and.returnValue(of(success({ cases: [], summons: [] })));
    api.listReferrals.and.returnValue(of(success(page([referral()]))));
    api.listSummons.and.returnValue(of(success(page([summon()]))));
    api.getStudentGuardians.and.returnValue(of(success([{
      id: 3, guardian: summon().guardian, canSubmitExcuses: true, canRequestGatePass: true,
      validFrom: '2026-01-01', validTo: null, isActive: true, rowVersion: 'AQID'
    }] as const)));
    api.getSummon.and.returnValue(of(success(summon())));
    api.getSummonHistory.and.returnValue(of(success({ transitions: [], appointments: [] })));
    api.createIdempotencyKey.and.returnValue('stable-w8-key');

    const auth = jasmine.createSpyObj<AuthService>('AuthService', ['hasRole', 'hasPermission', 'currentUser']);
    auth.hasRole.and.callFake(role => role === 'SocialWorker');
    auth.hasPermission.and.returnValue(true);
    auth.currentUser.and.returnValue({ userId: 'worker-1' } as never);
    toast = jasmine.createSpyObj('ToastService', ['success', 'error', 'warn', 'info']);

    await TestBed.configureTestingModule({
      imports: [SocialWorkerCrmComponent, NoopAnimationsModule],
      providers: [
        { provide: Phase5Service, useValue: api },
        { provide: AuthService, useValue: auth },
        { provide: ActivatedRoute, useValue: { snapshot: { data: { crmView: 'cases' } } } },
        { provide: ToastService, useValue: toast }
      ]
    }).compileComponents();
  });

  it('loads the assigned case queue with server paging', () => {
    const fixture = TestBed.createComponent(SocialWorkerCrmComponent);
    fixture.detectChanges();

    expect(api.listReferrals).toHaveBeenCalledWith(jasmine.objectContaining({ pageNumber: 1, pageSize: 20 }));
    expect(fixture.componentInstance.referrals().length).toBe(1);
  });

  it('localizes system actions and hides damaged legacy fixture text', () => {
    const component = TestBed.createComponent(SocialWorkerCrmComponent).componentInstance;
    const baseAction = {
      id: 1,
      actionType: 'Other' as const,
      description: '',
      actor: { userId: 'worker-1', displayName: 'Worker', roleSnapshot: 'SocialWorker' },
      actionAt: '2026-10-01T08:00:00Z',
      result: null
    };

    expect(component.displayActionDescription({
      ...baseAction,
      description: 'Referral accepted and moved to in-progress'
    })).toBe('تم قبول الإحالة وبدء المتابعة.');
    expect(component.displayActionDescription({
      ...baseAction,
      description: '???? ?????? ??????'
    })).not.toContain('???');
  });

  it('preserves one idempotency key across a retryable summons create failure', () => {
    api.createSummon.and.returnValue(throwError(() => new Error('network')));
    const component = TestBed.createComponent(SocialWorkerCrmComponent).componentInstance;
    component.openEngagement(referral(), 'summon');
    component.engagementForm.controls.reason.setValue('Guardian follow-up');

    component.submitEngagement();
    component.submitEngagement();

    expect(api.createSummon).toHaveBeenCalledTimes(2);
    expect(api.createSummon.calls.allArgs()[0][1]).toBe('stable-w8-key');
    expect(api.createSummon.calls.allArgs()[1][1]).toBe('stable-w8-key');
    expect(api.createIdempotencyKey).toHaveBeenCalledTimes(1);
  });

  it('sends a structured observation plan with measurable indicators', () => {
    api.startObservation.and.returnValue(of(success({ ...summon(), status: 'UnderObservation' as const })));
    const component = TestBed.createComponent(SocialWorkerCrmComponent).componentInstance;
    const attended = { ...summon(), status: 'Attended' as const };
    api.getSummon.and.returnValue(of(success(attended)));
    component.openSummon(attended, 'observe');
    component.observationForm.setValue({
      goals: 'Improve attendance', startDate: new Date(2026, 8, 29),
      reviewDate: new Date(2026, 9, 6), endDate: null,
      indicators: 'Attendance rate\nLate arrivals', notes: 'Review weekly'
    });

    component.submitSummonAction();

    expect(api.startObservation).toHaveBeenCalledWith(attended.id, jasmine.objectContaining({
      goals: 'Improve attendance', responsibleStaffUserId: 'worker-1',
      measurableIndicators: ['Attendance rate', 'Late arrivals']
    }));
  });

  it('reconciles an achieved referral transition after a network timeout without resending it', () => {
    const assigned = { ...referral(), status: 'Assigned' as const };
    const achieved = { ...assigned, status: 'InProgress' as const, rowVersion: 'BAUG' };
    api.acceptReferral.and.returnValue(throwError(() => new HttpErrorResponse({ status: 0 })));
    api.getReferral.and.returnValue(of(success(achieved)));
    const component = TestBed.createComponent(SocialWorkerCrmComponent).componentInstance;
    component.openReferral(assigned, 'accept');

    component.submitReferralAction();

    expect(api.acceptReferral).toHaveBeenCalledTimes(1);
    expect(api.getReferral).toHaveBeenCalledWith(assigned.id);
    expect(component.referralDialogVisible()).toBeFalse();
    expect(component.selectedReferral()?.rowVersion).toBe('BAUG');
    expect(toast.success).toHaveBeenCalled();
  });
});

function referral(): ReferralDto {
  return {
    id: 44, student: student(), sourceSnapshot: { sourceType: 'Absence', sourceEntityId: 7, countSnapshot: 4, thresholdSnapshot: 3 },
    currentMetric: null, priority: 'High', status: 'InProgress', assignedSocialWorker: null,
    actions: [], resolutionNotes: null, createdAt: '2026-09-29T08:00:00Z', rowVersion: 'AQID'
  };
}

function summon(): SummonDto {
  return {
    id: 12, student: student(), referralId: 44, createdReason: 'Follow-up', priority: 'High',
    sourceCountSnapshot: 4, thresholdSnapshot: 3, status: 'Pending', scheduledAt: null,
    location: null, instructions: null,
    guardian: { id: 9, displayName: 'Guardian', relationship: 'Father', isPrimary: true, receivesNotifications: true },
    assignedSocialWorker: null, requiresOfficerReview: false, officerReviewReason: null,
    guardianNotifiedAt: null, rowVersion: 'AQID'
  };
}

function student() {
  return { id: 17, studentNumber: 'S-17', displayName: 'Student', classroomId: 2, classLabel: '1/A', isActive: true, photoUrl: null };
}

function page<T>(items: readonly T[]): PagedResult<T> {
  return { items, totalCount: items.length, page: 1, pageSize: 20, totalPages: 1, hasNext: false, hasPrevious: false };
}

function success<T>(data: T): ApiResponse<T> {
  return { isSuccess: true, message: '', errors: [], data };
}
