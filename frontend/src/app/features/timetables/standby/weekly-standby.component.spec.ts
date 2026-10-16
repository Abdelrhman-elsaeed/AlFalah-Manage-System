import { TestBed } from '@angular/core/testing';
import { ElementRef } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { WeeklyStandbyComponent } from './weekly-standby.component';
import { WeeklyStandbyService } from '../../../core/services/weekly-standby.service';
import { WeeklyStandbyWorkbench } from '../../../core/models/weekly-standby.models';

describe('Weekly standby workspace', () => {
  let component: WeeklyStandbyComponent;
  let api: jasmine.SpyObj<WeeklyStandbyService>;
  const view = (): WeeklyStandbyWorkbench => ({ timetableId: 1, title: 'Test', revision: 4, setupRevision: 2,
    bellScheduleRevisionId: 1, isPublished: true, ready: true, issues: [],
    settings: { requiredPerPeriod: 1, overrides: [], excludedTeacherIds: [] },
    periods: [{ day: 2, period: 1, start: '07:00', end: '07:45', required: 1, allocated: 0, available: 1 }],
    teachers: [1, 2].map(id => ({ id, name: `Teacher ${id}`, teachingCount: id === 1 ? 1 : 0,
      availablePeriods: 1, standbyCount: 0, capacity: id === 1 ? 0 : 1,
      cells: [{ day: 2, period: 1, kind: id === 1 ? 'Lesson' : 'Free', eligible: id !== 1, canStandby: id !== 1,
        reason: null, classroom: '1/1', subject: 'Math', isProtected: false }] })), placements: [] });
  beforeEach(() => {
    api = jasmine.createSpyObj('WeeklyStandbyService', ['get', 'preview', 'apply']);
    TestBed.configureTestingModule({ imports: [TranslateModule.forRoot()], providers: [{ provide: WeeklyStandbyService, useValue: api }, { provide: ElementRef, useValue: new ElementRef(document.createElement('section')) }] });
    api.get.and.returnValue(of({ isSuccess: true, data: view(), errors: [], message: '' }));
    component = TestBed.runInInjectionContext(() => new WeeklyStandbyComponent()); component.timetableId = 1;
  });
  it('previews without applying and saves the exact reviewed request rather than recomputed preview output', async () => {
    await component.load(); const output = view(); output.placements = [{ instructorProfileId: 2, day: 2, period: 1, isLocked: false, source: 'Fair' }];
    api.preview.and.returnValue(of({ isSuccess: true, errors: [], message: '', data: { proposalId: 'hash', mode: 'Fair', workbench: output } }));
    await component.preview(); expect(api.apply).not.toHaveBeenCalled(); expect(component.totalAllocated).toBe(1);
    api.apply.and.returnValue(of({ isSuccess: true, data: { ...output, revision: 5, isPublished: false }, errors: [], message: '' }));
    await component.save(); const sent = api.apply.calls.mostRecent().args[1];
    expect(sent.placements).toEqual([]); expect(sent.proposalId).toBe('hash'); expect(sent.revision).toBe(4); expect(component.dirty).toBeFalse();
  });
  it('manual cells never overwrite teaching, mode changes retain locks and undo restores the prior selection', async () => {
    await component.load(); component.setMode('Manual');
    component.toggleCell(component.view!.teachers[0], 1); expect(component.totalAllocated).toBe(0);
    component.toggleCell(component.view!.teachers[1], 1); expect(component.view!.placements[0].isLocked).toBeTrue();
    component.setMode('Fair'); expect(component.totalAllocated).toBe(1);
    component.undo(); expect(component.totalAllocated).toBe(0); expect(component.dirty).toBeFalse();
  });
  it('editing demand invalidates a reviewed proposal and an HTTP conflict retains edits', async () => {
    await component.load(); component.setMode('Manual'); component.toggleCell(component.view!.teachers[1], 1);
    api.preview.and.returnValue(of({ isSuccess: true, errors: [], message: '', data: { proposalId: 'hash', mode: 'Manual', workbench: structuredClone(component.view!) } }));
    await component.preview(); component.setDemand(1, 2); expect(component.accepted).toBeNull();
    api.preview.and.returnValue(throwError(() => ({ status: 409, error: { message: 'changed' } })));
    await component.preview(); expect(component.totalAllocated).toBe(1); expect(component.dirty).toBeTrue(); expect(component.busy).toBeFalse();
  });
  it('confirmed cover cannot be removed or unlocked and excluded teachers cannot be selected', async () => {
    await component.load(); component.setMode('Manual'); const teacher = component.view!.teachers[1];
    component.toggleCell(teacher, 1); teacher.cells[0].isProtected = true;
    component.toggleCell(teacher, 1); component.toggleLock(teacher, 1); component.toggleTeacher(teacher);
    expect(component.totalAllocated).toBe(1); expect(component.view!.placements[0].isLocked).toBeTrue(); expect(component.excluded(teacher)).toBeFalse();
    teacher.cells[0].isProtected = false; component.toggleCell(teacher, 1); component.toggleTeacher(teacher); component.toggleCell(teacher, 1);
    expect(component.excluded(teacher)).toBeTrue(); expect(component.totalAllocated).toBe(0);
  });
  it('readiness blocks both preview and manual changes', async () => {
    await component.load(); component.view!.ready = false; component.setMode('Manual');
    component.toggleCell(component.view!.teachers[1], 1); await component.preview();
    expect(component.totalAllocated).toBe(0); expect(api.preview).not.toHaveBeenCalled();
  });
});
