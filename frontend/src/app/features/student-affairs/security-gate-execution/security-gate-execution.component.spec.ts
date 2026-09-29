import { HttpErrorResponse } from '@angular/common/http';
import { TestBed, discardPeriodicTasks, fakeAsync, tick } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { Subject, of, throwError } from 'rxjs';
import { ApiResponse } from '../../../core/models/api-response.model';
import {
  SecurityGatePassDetailDto,
  SecurityGatePassPage,
  SecurityGatePassQueueItemDto
} from '../../../core/models/gate-pass.models';
import { AuthService } from '../../../core/services/auth.service';
import { GatePassService } from '../../../core/services/gate-pass.service';
import { ToastService } from '../../../core/services/toast.service';
import { SecurityGateExecutionComponent } from './security-gate-execution.component';

describe('SecurityGateExecutionComponent', () => {
  let api: jasmine.SpyObj<GatePassService>;
  let auth: jasmine.SpyObj<AuthService>;
  let toast: jasmine.SpyObj<ToastService>;

  beforeEach(async () => {
    api = jasmine.createSpyObj<GatePassService>('GatePassService', [
      'securityQueue', 'securityDetail', 'acknowledgeSecurity', 'execute'
    ]);
    api.securityQueue.and.returnValue(of(success(page([]))));
    auth = jasmine.createSpyObj<AuthService>('AuthService', ['hasRole', 'hasPermission']);
    auth.hasRole.and.callFake(role => role === 'SecurityGuard');
    auth.hasPermission.and.returnValue(true);
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'info', 'warn', 'error']);

    await TestBed.configureTestingModule({
      imports: [SecurityGateExecutionComponent, NoopAnimationsModule],
      providers: [
        { provide: GatePassService, useValue: api },
        { provide: AuthService, useValue: auth },
        { provide: ToastService, useValue: toast }
      ]
    }).compileComponents();
  });

  it('loads the server-paged queue and uses the server clock', fakeAsync(() => {
    const serverNow = '2026-09-29T08:00:00.000Z';
    api.securityQueue.and.returnValue(of(success(page([queueItem()], serverNow))));
    const fixture = TestBed.createComponent(SecurityGateExecutionComponent);

    fixture.detectChanges();
    tick(0);
    fixture.detectChanges();

    expect(api.securityQueue).toHaveBeenCalledOnceWith({ pageNumber: 1, pageSize: 12 });
    expect(fixture.componentInstance.queue().map(item => item.id)).toEqual([7]);
    expect(Math.abs(fixture.componentInstance.now() - new Date(serverNow).getTime())).toBeLessThan(50);
    fixture.destroy();
    discardPeriodicTasks();
  }));

  it('renders empty and scoped error states without fabricating rows', fakeAsync(() => {
    const emptyFixture = TestBed.createComponent(SecurityGateExecutionComponent);
    emptyFixture.detectChanges();
    tick(0);
    emptyFixture.detectChanges();
    expect(emptyFixture.nativeElement.textContent).toContain('لا توجد استئذانات جاهزة');
    emptyFixture.destroy();

    api.securityQueue.and.returnValue(throwError(() => new HttpErrorResponse({ status: 403 })));
    const deniedFixture = TestBed.createComponent(SecurityGateExecutionComponent);
    deniedFixture.detectChanges();
    tick(0);
    deniedFixture.detectChanges();
    expect(deniedFixture.componentInstance.queue()).toEqual([]);
    expect(deniedFixture.nativeElement.querySelector('[role="alert"]')).not.toBeNull();
    expect(deniedFixture.nativeElement.textContent).toContain('لا تملك دور الأمن');
    deniedFixture.destroy();
    discardPeriodicTasks();
  }));

  it('treats the execution window as half-open', () => {
    const component = createComponent();
    const item = queueItem();

    component.now.set(new Date(item.approvedWindowStartsAt).getTime());
    expect(component.isWindowActive(item)).toBeTrue();
    component.now.set(new Date(item.approvedWindowEndsAt).getTime());
    expect(component.isWindowActive(item)).toBeFalse();
    expect(component.windowState(item)).toContain('انتهت');
  });

  it('confirms acknowledgement with the latest opaque RowVersion and blocks a duplicate click', () => {
    const item = queueItem();
    const pending = new Subject<ApiResponse<SecurityGatePassDetailDto>>();
    api.acknowledgeSecurity.and.returnValue(pending);
    const component = createComponentWithQueue(item);

    component.acknowledge(item);
    component.confirmAcknowledgement();
    component.confirmAcknowledgement();

    expect(api.acknowledgeSecurity).toHaveBeenCalledOnceWith(item.id, { rowVersion: 'latest-rv' });
    expect(component.mutatingId()).toBe(item.id);
    pending.next(success(detail('SecurityAcknowledged', 'next-rv')));
    pending.complete();
    expect(component.queue()[0].rowVersion).toBe('next-rv');
    expect(component.acknowledgementDialogVisible()).toBeFalse();
  });

  it('submits no client exit time and blocks double execution while in flight', () => {
    const item = queueItem('SecurityAcknowledged');
    const pending = new Subject<ApiResponse<SecurityGatePassDetailDto>>();
    api.execute.and.returnValue(pending);
    const component = createComponentWithQueue(item);
    component.openExit(item);
    component.exitForm.setValue({
      verificationMethod: 'Visual',
      verificationNote: '  matched guardian  ',
      gateNote: '  Gate 1  '
    });

    component.executeExit();
    component.executeExit();

    expect(api.execute).toHaveBeenCalledTimes(1);
    const request = api.execute.calls.mostRecent().args[1] as unknown as Record<string, unknown>;
    expect(request['exitedAt']).toBeUndefined();
    expect(request).toEqual({
      verificationMethod: 'Visual',
      verificationNote: 'matched guardian',
      gateNote: 'Gate 1',
      rowVersion: 'latest-rv'
    });
  });

  it('refetches after 409, preserves the draft, and never retries blindly', () => {
    const item = queueItem('SecurityAcknowledged');
    api.execute.and.returnValue(throwError(() => new HttpErrorResponse({ status: 409 })));
    api.securityDetail.and.returnValue(of(success(detail('SecurityAcknowledged', 'winner-rv'))));
    const component = createComponentWithQueue(item);
    component.openExit(item);
    component.exitForm.setValue({
      verificationMethod: 'Manual',
      verificationNote: 'Keep this draft',
      gateNote: 'Keep gate note'
    });

    component.executeExit();

    expect(api.execute).toHaveBeenCalledTimes(1);
    expect(api.securityDetail).toHaveBeenCalledOnceWith(item.id);
    expect(component.exitForm.getRawValue()).toEqual({
      verificationMethod: 'Manual',
      verificationNote: 'Keep this draft',
      gateNote: 'Keep gate note'
    });
    expect(component.queue()[0].rowVersion).toBe('winner-rv');
  });

  it('blocks retry while conflict reconciliation is still in flight', () => {
    const item = queueItem('SecurityAcknowledged');
    const reconciliation = new Subject<ApiResponse<SecurityGatePassDetailDto>>();
    api.execute.and.returnValue(throwError(() => new HttpErrorResponse({ status: 409 })));
    api.securityDetail.and.returnValue(reconciliation);
    const component = createComponentWithQueue(item);
    component.openExit(item);
    component.exitForm.setValue({ verificationMethod: 'Manual', verificationNote: 'Draft', gateNote: '' });

    component.executeExit();
    component.executeExit();

    expect(api.execute).toHaveBeenCalledTimes(1);
    expect(component.reconcilingId()).toBe(item.id);
    reconciliation.next(success(detail('SecurityAcknowledged', 'winner-rv')));
    reconciliation.complete();
    expect(component.reconcilingId()).toBeNull();
  });

  it('reconciles an uncertain network result and does not repeat an exit already saved', () => {
    const item = queueItem('SecurityAcknowledged');
    api.execute.and.returnValue(throwError(() => new HttpErrorResponse({ status: 0 })));
    api.securityDetail.and.returnValue(of(success(detail('Exited', 'done-rv'))));
    const component = createComponentWithQueue(item);
    component.openExit(item);
    component.exitForm.setValue({ verificationMethod: 'Visual', verificationNote: 'Verified', gateNote: '' });

    component.executeExit();

    expect(api.execute).toHaveBeenCalledTimes(1);
    expect(api.securityDetail).toHaveBeenCalledOnceWith(item.id);
    expect(component.receipt()?.status).toBe('Exited');
    expect(component.queue()).toEqual([]);
  });

  it('removes a stale card when reconciliation returns scoped 404', () => {
    const item = queueItem('SecurityAcknowledged');
    api.execute.and.returnValue(throwError(() => new HttpErrorResponse({ status: 409 })));
    api.securityDetail.and.returnValue(throwError(() => new HttpErrorResponse({ status: 404 })));
    const component = createComponentWithQueue(item);
    component.openExit(item);
    component.exitForm.setValue({ verificationMethod: 'Visual', verificationNote: 'Verified', gateNote: '' });

    component.executeExit();

    expect(component.queue()).toEqual([]);
    expect(component.exitDialogVisible()).toBeFalse();
    expect(toast.warn).toHaveBeenCalled();
  });

  it('keeps dashboard-only guards read-only even when the queue is visible', fakeAsync(() => {
    auth.hasPermission.and.callFake(permission => permission === 'StudentAffairsDashboard.Security');
    api.securityQueue.and.returnValue(of(success(page([queueItem()]))));
    const fixture = TestBed.createComponent(SecurityGateExecutionComponent);

    fixture.detectChanges();
    tick(0);
    fixture.detectChanges();

    expect(fixture.componentInstance.canAcknowledge).toBeFalse();
    expect(fixture.componentInstance.canExecute).toBeFalse();
    expect(fixture.nativeElement.textContent).toContain('العرض فقط');
    expect(fixture.nativeElement.querySelector('.ack-button')).toBeNull();
    fixture.destroy();
    discardPeriodicTasks();
  }));

  function createComponent(): SecurityGateExecutionComponent {
    return TestBed.createComponent(SecurityGateExecutionComponent).componentInstance;
  }

  function createComponentWithQueue(item: SecurityGatePassQueueItemDto): SecurityGateExecutionComponent {
    const component = createComponent();
    component.now.set(new Date('2026-09-29T08:00:00.000Z').getTime());
    (component as unknown as {
      applyQueue(items: readonly SecurityGatePassQueueItemDto[], total: number, page: number, pageSize: number, serverNow: string): void;
    }).applyQueue([item], 1, 1, 12, '2026-09-29T08:00:00.000Z');
    return component;
  }
});

function queueItem(status: SecurityGatePassQueueItemDto['status'] = 'Approved'): SecurityGatePassQueueItemDto {
  return {
    id: 7,
    student: {
      id: 11,
      studentNumber: 'ST-11',
      displayName: 'Student One',
      classroomId: null,
      classLabel: '1/A',
      isActive: true,
      photoUrl: null
    },
    classLabel: '1/A',
    approvedWindowStartsAt: '2026-09-29T07:55:00.000Z',
    approvedWindowEndsAt: '2026-09-29T08:30:00.000Z',
    pickupPerson: { name: 'Parent One', relationship: 'Father', identityHint: 'ID-4' },
    officerName: 'Officer One',
    approvedAt: '2026-09-29T07:50:00.000Z',
    securityAcknowledgedAt: status === 'SecurityAcknowledged' ? '2026-09-29T07:59:00.000Z' : null,
    status,
    rowVersion: 'latest-rv'
  };
}

function detail(status: SecurityGatePassDetailDto['status'], rowVersion: string): SecurityGatePassDetailDto {
  const item = queueItem(status === 'Approved' ? 'Approved' : 'SecurityAcknowledged');
  return {
    ...item,
    status,
    rowVersion,
    exitedAt: status === 'Exited' ? '2026-09-29T08:00:00.000Z' : null
  };
}

function page(
  items: readonly SecurityGatePassQueueItemDto[],
  serverNow = '2026-09-29T08:00:00.000Z'
): SecurityGatePassPage {
  return {
    items,
    totalCount: items.length,
    page: 1,
    pageSize: 12,
    totalPages: items.length ? 1 : 0,
    hasNext: false,
    hasPrevious: false,
    serverNow
  };
}

function success<T>(data: T): ApiResponse<T> {
  return { isSuccess: true, message: '', data, errors: [] };
}
