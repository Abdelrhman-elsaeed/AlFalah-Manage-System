import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import { DutyExecutionService } from '../../../core/services/duty-execution.service';
import { AuthService } from '../../../core/services/auth.service';
import { DutyExecution } from '../../../core/models/duty-execution.models';
import { DutyExecutionComponent } from './duty-execution.component';
import { schoolDutyUnsavedGuard } from '../../../core/guards/school-duty-navigation.guard';

describe('Duty draft departure is distinct from report submission', () => {
  let component: DutyExecutionComponent;
  let ask: jasmine.Spy;
  let save: jasmine.Spy;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([]),
      { provide: DutyExecutionService, useValue: {} },
      { provide: TranslateService, useValue: { currentLang: 'en', instant: (key: string) => key } },
      { provide: AuthService, useValue: { currentUser: () => ({ userId: 'synthetic' }) } }
    ] });
    component = TestBed.runInInjectionContext(() => new DutyExecutionComponent());
    component.assignmentId = 910;
    component.data.set({ assignmentId: 910, assignmentRevision: 1, canExecute: true, acknowledged: true,
      draft: { version: 1, submittedAt: null }, lateStudents: [], documents: [] } as unknown as DutyExecution);
    ask = jasmine.createSpy('ask').and.resolveTo(true);
    component.draftExit = { ask } as any;
    save = spyOn(component, 'save').and.callFake(async () => { component.dirty.set(false); return true; });
  });
  afterEach(() => sessionStorage.removeItem(component.cacheKey));
  it('never prompts just for loading or viewing an existing draft', () => {
    expect(component.requestLeave()).toBeTrue(); expect(ask).not.toHaveBeenCalled(); expect(save).not.toHaveBeenCalled();
  });
  it('warns after editing even after autosave cleared dirty; approval permits departure only once', async () => {
    component.changed(); component.dirty.set(false);
    expect(await component.requestLeave()).toBeTrue(); expect(ask).toHaveBeenCalledTimes(1);
    expect(component.requestLeave()).toBeTrue(); expect(ask).toHaveBeenCalledTimes(1);
    expect(component.data()!.draft!.submittedAt).toBeNull();
  });
  it('saves before explaining draft-only status, without signing or delivering', async () => {
    component.changed(); expect(await component.requestLeave()).toBeTrue();
    expect(save).toHaveBeenCalledBefore(ask); expect(component.data()!.documents).toEqual([]);
  });
  it('keeps work and stays on the page when saving fails, without claiming it was saved', async () => {
    component.changed(); save.and.resolveTo(false);
    expect(await component.requestLeave()).toBeFalse(); expect(ask).not.toHaveBeenCalled(); expect(component.dirty()).toBeTrue();
  });
  it('does not claim saved status if newer input arrived during the save request', async () => {
    component.changed(); save.and.resolveTo(true);
    expect(await component.requestLeave()).toBeFalse(); expect(ask).not.toHaveBeenCalled(); expect(component.dirty()).toBeTrue();
  });
  it('continue keeps the report open, and another real departure still explains the draft', async () => {
    component.changed(); component.dirty.set(false); ask.and.resolveTo(false);
    expect(await component.requestLeave()).toBeFalse(); expect(await component.requestLeave()).toBeFalse();
    expect(ask).toHaveBeenCalledTimes(2);
  });
  it('deduplicates simultaneous guard/close requests into one dialog', async () => {
    component.changed(); component.dirty.set(false); let finish!: (value: boolean) => void;
    ask.and.returnValue(new Promise<boolean>(resolve => finish = resolve));
    const first = component.requestLeave(); const second = component.requestLeave();
    expect(first).toBe(second); expect(ask).toHaveBeenCalledTimes(1); finish(true); expect(await first).toBeTrue();
  });
  it('does not prompt for submitted, unauthorized or historical read-only reports', () => {
    component.changed(); component.dirty.set(false);
    component.data.update(d => ({ ...d!, draft: { ...d!.draft!, submittedAt: '2026-10-15T12:00:00Z' } }));
    expect(component.requestLeave()).toBeTrue();
    component.data.update(d => ({ ...d!, canExecute: false, draft: { ...d!.draft!, submittedAt: null } }));
    expect(component.requestLeave()).toBeTrue(); expect(ask).not.toHaveBeenCalled();
  });
  it('blocks disposal while save/sign is in flight and resumes safely afterward', async () => {
    component.changed(); component.busy.set(true); expect(component.requestLeave()).toBeFalse(); expect(ask).not.toHaveBeenCalled();
    component.busy.set(false); expect(await component.requestLeave()).toBeTrue();
  });
  it('guard waits for the centered notice instead of also showing a native discard prompt', async () => {
    const confirm = spyOn(window, 'confirm');
    const result = TestBed.runInInjectionContext(() => schoolDutyUnsavedGuard({ hasDraft: () => false, prepareExecutionLeave: () => Promise.resolve(false) }, {} as any, {} as any, { url: '/school-manager' } as any));
    expect(await result).toBeFalse(); expect(confirm).not.toHaveBeenCalled();
  });
});
