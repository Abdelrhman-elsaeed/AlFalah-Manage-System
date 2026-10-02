import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ApiResponse, PagedResult } from '../../../core/models/api-response.model';
import { Phase5Service } from '../../../core/services/phase5.service';
import { SocialWorkerDashboardComponent } from './social-worker-dashboard.component';

describe('SocialWorkerDashboardComponent', () => {
  it('shows the social worker priorities and loads both operational previews', async () => {
    const api = jasmine.createSpyObj<Phase5Service>('Phase5Service', [
      'getSocialWorkerDashboard', 'listReferrals', 'listSummons'
    ]);
    api.getSocialWorkerDashboard.and.returnValue(of(success({
      cases: [{ code: 'ActiveCases', label: 'حالات المتابعة النشطة', count: 3, severity: 'warning' }],
      summons: [
        { code: 'Pending', label: 'بانتظار الموعد/الحضور', count: 2, severity: 'warning' },
        { code: 'UnderObservation', label: 'تحت الملاحظة', count: 1, severity: 'info' },
        { code: 'Improved', label: 'تحسّن', count: 4, severity: 'success' }
      ]
    })));
    api.listReferrals.and.returnValue(of(success(page([]))));
    api.listSummons.and.returnValue(of(success(page([]))));

    await TestBed.configureTestingModule({
      imports: [SocialWorkerDashboardComponent],
      providers: [{ provide: Phase5Service, useValue: api }, provideRouter([])]
    }).compileComponents();

    const fixture = TestBed.createComponent(SocialWorkerDashboardComponent);
    fixture.detectChanges();

    expect(api.listReferrals).toHaveBeenCalledWith(jasmine.objectContaining({ pageSize: 6 }));
    expect(api.listSummons).toHaveBeenCalledWith(jasmine.objectContaining({ pageSize: 6 }));
    expect(fixture.nativeElement.textContent).toContain('لوحة الموجه الطلابي');
    expect(fixture.nativeElement.textContent).toContain('حالات نشطة');
    expect(fixture.nativeElement.textContent).toContain('حالات تحسّنت');
  });
});

function success<T>(data: T): ApiResponse<T> {
  return { isSuccess: true, message: '', data, errors: [] };
}

function page<T>(items: readonly T[]): PagedResult<T> {
  return { items, totalCount: items.length, page: 1, pageSize: 6, totalPages: 1, hasNext: false, hasPrevious: false };
}
