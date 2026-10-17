import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { DailyCoverageService } from '../../../core/services/daily-coverage.service';
import { CoverageTask } from '../../../core/models/daily-coverage.models';
import { CoverageTasksComponent } from './coverage-tasks.component';

describe('Coverage teacher workflow', () => {
  let api: jasmine.SpyObj<DailyCoverageService>; let component: CoverageTasksComponent;
  const task = (): CoverageTask => ({ need: { id: 1, revision: 3, timetableId: 1, sourceEntryId: 1, date: '2026-10-18', originalTeacher: 'Original', classroom: '1/1', subject: 'Math', periods: [1], startsAt: '2026-10-18T07:00:00+03:00', endsAt: '2026-10-18T07:45:00+03:00', timeZoneId: 'Africa/Cairo', state: 'Assigned', isRequired: true, substitutionId: 4, assignedTeacherId: 2, assignedTeacher: 'Replacement', reviewReason: null, acknowledgedAt: null, submittedAt: null, narrative: '', notes: '', correctionReason: null }, canAcknowledge: true, canExecute: false, canEditDraft: true, blockedReason: null, accountSignature: 'data:image/png;base64,test', history: [] });
  beforeEach(async () => {
    api = jasmine.createSpyObj('DailyCoverageService', ['tasks', 'task', 'draft', 'act']);
    api.tasks.and.returnValue(of({ isSuccess: true, errors: [], message: '', data: { items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0, hasNext: false, hasPrevious: false } }));
    api.task.and.returnValue(of({ isSuccess: true, errors: [], message: '', data: task() }));
    TestBed.configureTestingModule({ imports: [TranslateModule.forRoot()], providers: [{ provide: DailyCoverageService, useValue: api }, { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } }] });
    component = TestBed.runInInjectionContext(() => new CoverageTasksComponent()); await component.initialize(); await component.open(1);
  });
  it('protects unsaved text and does not show unload warnings for a clean read', () => {
    const clean = new Event('beforeunload', { cancelable: true }) as BeforeUnloadEvent;
    component.beforeUnload(clean); expect(clean.defaultPrevented).toBeFalse();
    component.narrative = 'Unsaved'; const dirty = new Event('beforeunload', { cancelable: true }) as BeforeUnloadEvent;
    component.beforeUnload(dirty); expect(dirty.defaultPrevented).toBeTrue();
    spyOn(window, 'confirm').and.returnValue(false); expect(component.canLeave()).toBeFalse();
    component.prepare('acknowledge'); expect(component.review).toBeNull(); expect(api.act).not.toHaveBeenCalled();
  });
  it('keeps the draft on conflict and requires an explicit signed action after saving', async () => {
    component.narrative = 'What I did'; api.draft.and.returnValue(throwError(() => ({ status: 409, error: { message: 'Changed' } })));
    await component.save(); expect(component.narrative).toBe('What I did'); expect(component.dirty).toBeTrue(); expect(api.act).not.toHaveBeenCalled();
    api.draft.and.returnValue(of({ isSuccess: true, errors: [], message: '', data: { ...task(), need: { ...task().need, revision: 4, narrative: 'What I did' } } }));
    await component.save(); expect(component.dirty).toBeFalse(); expect(component.detail!.need.submittedAt).toBeNull();
  });
  it('retries the same signed request after a lost response', async () => {
    component.prepare('acknowledge'); api.act.and.returnValue(throwError(() => ({ status: 0 })));
    await component.confirm(); const first = api.act.calls.mostRecent().args[2];
    api.act.and.returnValue(of({ isSuccess: true, errors: [], message: '', data: { ...task(), canAcknowledge: false, need: { ...task().need, state: 'Acknowledged', acknowledgedAt: '2026-10-18T06:59:00Z' } } }));
    await component.confirm(); expect(api.act.calls.mostRecent().args[2]).toEqual(first); expect(component.detail!.need.state).toBe('Acknowledged'); expect(component.detail!.need.submittedAt).toBeNull();
  });
});
