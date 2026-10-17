import { TestBed } from '@angular/core/testing';
import { TranslateModule } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { DailyCoverageService } from '../../../core/services/daily-coverage.service';
import { DailyCoverageLedgerComponent } from '../../intelligent-timetable/substitutions/daily-coverage-ledger.component';

describe('Daily coverage approval', () => {
  let api: jasmine.SpyObj<DailyCoverageService>; let component: DailyCoverageLedgerComponent;
  const board = { date: '2026-10-18', timetableId: 1, timetableRevision: 4, canManage: true, canOverride: true, issue: null, now: '2026-10-18T06:00:00Z', needs: [], teachers: [] };
  beforeEach(async () => {
    api = jasmine.createSpyObj('DailyCoverageService', ['board', 'reconcile', 'preview', 'approve']);
    api.board.and.callFake(() => of({ isSuccess: true, data: structuredClone(board), errors: [], message: '' }));
    api.preview.and.returnValue(of({ isSuccess: true, data: { proposalId: 'reviewed-hash', expiresAt: '2026-10-18T06:04:30Z', mode: 'Manual', assignments: [], uncoveredNeedIds: [] }, errors: [], message: '' }));
    TestBed.configureTestingModule({ imports: [TranslateModule.forRoot()], providers: [{ provide: DailyCoverageService, useValue: api }] });
    component = TestBed.runInInjectionContext(() => new DailyCoverageLedgerComponent()); await component.load(false);
  });
  it('never applies on load or preview and retries the exact reviewed request', async () => {
    component.mode = 'Manual'; component.choices = [{ needId: 1, teacherId: 2 }];
    await component.preview(); expect(api.approve).not.toHaveBeenCalled();
    api.approve.and.returnValue(throwError(() => ({ status: 0 })));
    await component.approve(); const first = api.approve.calls.mostRecent().args[0];
    component.choices = [{ needId: 99, teacherId: 99 }]; await component.approve();
    expect(api.approve.calls.mostRecent().args[0]).toEqual(first);
    expect(first.preview.choices).toEqual([{ needId: 1, teacherId: 2 }]);
    expect(first.proposalId).toBe('reviewed-hash'); expect(component.reviewOpen).toBeTrue();
  });
  it('refreshes the server clock before preview and changing mode invalidates approval', async () => {
    component.board!.now = '2020-01-01T00:00:00Z'; await component.preview();
    expect(api.preview.calls.mostRecent().args[0].expiresAt).toBe('2026-10-18T06:04:30.000Z');
    component.setMode('Manual'); expect(component.proposal).toBeNull();
    component.busy = true; expect(component.canLeave()).toBeFalse();
  });
});
