import { TestBed } from '@angular/core/testing';
import { ElementRef } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { WeeklyStandbyComponent } from './weekly-standby.component';
import { WeeklyStandbyService } from '../../../core/services/weekly-standby.service';
import { WeeklyStandbyWorkbench } from '../../../core/models/weekly-standby.models';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { TimetableService } from '../../../core/services/timetable.service';

describe('Weekly standby workspace', () => {
  let component: WeeklyStandbyComponent;
  let api: jasmine.SpyObj<WeeklyStandbyService>;
  let timetables: jasmine.SpyObj<TimetableService>;
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
    timetables = jasmine.createSpyObj('TimetableService', ['publish']);
    TestBed.configureTestingModule({ imports: [TranslateModule.forRoot(), NoopAnimationsModule], providers: [{ provide: TimetableService, useValue: timetables }, { provide: WeeklyStandbyService, useValue: api }, { provide: ElementRef, useValue: new ElementRef(document.createElement('section')) }] });
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
  it('offers an actionable save review for manual choices without requiring a hidden prior preview', async () => {
    const fixture = TestBed.createComponent(WeeklyStandbyComponent);
    const planner = fixture.componentInstance; planner.timetableId = 1;
    await planner.load(); planner.setMode('Manual'); planner.toggleCell(planner.view!.teachers[1], 1);
    api.preview.and.returnValue(of({isSuccess:true,errors:[],message:'',data:{proposalId:'reviewed-manual',mode:'Manual',workbench:structuredClone(planner.view!)}}));
    fixture.detectChanges();
    const action = fixture.nativeElement.querySelector('.save-actions .primary') as HTMLButtonElement;
    expect(action.disabled).toBeFalse();action.click();await fixture.whenStable();fixture.detectChanges();
    expect(api.preview).toHaveBeenCalled();expect(api.apply).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('.standby-save-review')).not.toBeNull();
    api.apply.and.returnValue(of({isSuccess:true,data:{...planner.view!,revision:5,isPublished:false},errors:[],message:''}));
    (fixture.nativeElement.querySelector('.review-actions .save-draft') as HTMLButtonElement).click();await fixture.whenStable();fixture.detectChanges();
    expect(api.apply.calls.mostRecent().args[1].proposalId).toBe('reviewed-manual');expect(planner.reviewOpen).toBeFalse();expect(planner.dirty).toBeFalse();
    expect(timetables.publish).not.toHaveBeenCalled();
  });
  it('explains unallocated demand without claiming a capacity failure before review', async () => {
    const fixture = TestBed.createComponent(WeeklyStandbyComponent);const planner=fixture.componentInstance;planner.timetableId=1;
    await planner.load();planner.setMode('Manual');fixture.detectChanges();
    expect(planner.shortages).toBe(1);expect(fixture.nativeElement.querySelector('.planning-status').textContent).toContain('STANDBY.MANUAL_NEXT');
    expect(fixture.nativeElement.querySelectorAll('.message.warning').length).toBe(0);
    api.preview.and.returnValue(of({isSuccess:true,errors:[],message:'',data:{proposalId:'partial',mode:'Manual',workbench:view()}}));
    await planner.reviewForSave();fixture.detectChanges();expect(fixture.nativeElement.querySelector('.standby-save-review .message.warning')).not.toBeNull();
    planner.setDemand(1,2);fixture.detectChanges();expect(planner.accepted).toBeNull();expect(planner.reviewOpen).toBeFalse();
  });
  it('undoes a fair proposal from its review dialog and restores manual locks and demand without saving', async () => {
    const initial=view();initial.periods.push({...initial.periods[0],period:2});
    initial.teachers.forEach(t=>t.cells.push({...t.cells[0],period:2,kind:'Free',canStandby:true,eligible:true}));
    api.get.and.returnValue(of({isSuccess:true,data:initial,errors:[],message:''}));
    const fixture=TestBed.createComponent(WeeklyStandbyComponent),planner=fixture.componentInstance;planner.timetableId=1;
    await planner.load();planner.setMode('Manual');planner.toggleCell(planner.view!.teachers[1],1);planner.setDemand(2,2);
    const previous=structuredClone({settings:planner.view!.settings,placements:planner.view!.placements});planner.setMode('Fair');
    const proposed=structuredClone(planner.view!);proposed.placements.push({instructorProfileId:1,day:2,period:2,isLocked:false,source:'Fair'});
    api.preview.and.returnValue(of({isSuccess:true,errors:[],message:'',data:{proposalId:'fair-undo',mode:'Fair',workbench:proposed}}));
    await planner.reviewForSave();fixture.detectChanges();
    const undo=fixture.nativeElement.querySelector('.review-actions .undo-review') as HTMLButtonElement|null;
    expect(undo).not.toBeNull();undo?.click();fixture.detectChanges();
    expect(planner.reviewOpen).toBeFalse();expect(planner.accepted).toBeNull();expect(planner.view!.placements).toEqual(previous.placements);
    expect(planner.view!.settings).toEqual(previous.settings);expect(planner.canUndo).toBeTrue();expect(api.apply).not.toHaveBeenCalled();
  });
  it('does not insert an empty undo step when review leaves the allocation unchanged', async () => {
    await component.load();component.setMode('Manual');component.toggleCell(component.view!.teachers[1],1);
    api.preview.and.returnValue(of({isSuccess:true,errors:[],message:'',data:{proposalId:'unchanged',mode:'Manual',workbench:structuredClone(component.view!)}}));
    await component.preview();component.undo();expect(component.totalAllocated).toBe(0);expect(component.dirty).toBeFalse();expect(component.canUndo).toBeFalse();
  });
  it('offers explicit save and publication from the reviewed proposal using the saved revision', async () => {
    const fixture=TestBed.createComponent(WeeklyStandbyComponent),planner=fixture.componentInstance;planner.timetableId=1;
    await planner.load();api.preview.and.returnValue(of({isSuccess:true,errors:[],message:'',data:{proposalId:'publish-reviewed',mode:'Fair',workbench:view()}}));
    await planner.preview();fixture.detectChanges();
    expect(api.apply).not.toHaveBeenCalled();expect(timetables.publish).not.toHaveBeenCalled();
    api.apply.and.returnValue(of({isSuccess:true,errors:[],message:'',data:{...view(),revision:5,isPublished:false}}));
    timetables.publish.and.returnValue(of({isSuccess:true,errors:[],message:'',data:{id:1,revision:6,isPublished:true} as any}));
    const publish=fixture.nativeElement.querySelector('.review-actions .save-and-publish') as HTMLButtonElement|null;
    expect(publish).not.toBeNull();publish?.click();await fixture.whenStable();fixture.detectChanges();
    expect(api.apply).toHaveBeenCalledTimes(1);expect(timetables.publish).toHaveBeenCalledOnceWith(1,5);
    expect(planner.view!.isPublished).toBeTrue();expect(planner.view!.revision).toBe(6);expect(planner.dirty).toBeFalse();
  });
  it('retains the saved draft on publication failure and retries publication without applying twice', async () => {
    await component.load();api.preview.and.returnValue(of({isSuccess:true,errors:[],message:'',data:{proposalId:'retry',mode:'Fair',workbench:view()}}));
    await component.preview();api.apply.and.returnValue(of({isSuccess:true,errors:[],message:'',data:{...view(),revision:5,isPublished:false}}));
    timetables.publish.and.returnValue(throwError(()=>({status:503,error:{message:'offline'}})));
    await component.save(true);expect(component.dirty).toBeFalse();expect(component.view!.revision).toBe(5);expect(component.canPublishSaved).toBeTrue();expect(component.error).toContain('STANDBY.SAVED_NOT_PUBLISHED');
    timetables.publish.and.returnValue(of({isSuccess:true,errors:[],message:'',data:{id:1,revision:6,isPublished:true} as any}));
    await component.publishSaved();expect(api.apply).toHaveBeenCalledTimes(1);expect(timetables.publish).toHaveBeenCalledTimes(2);expect(component.view!.isPublished).toBeTrue();
  });
  it('never publishes when save conflicts or when working on an experimental copy', async () => {
    await component.load();api.preview.and.returnValue(of({isSuccess:true,errors:[],message:'',data:{proposalId:'stale',mode:'Fair',workbench:view()}}));
    await component.preview();api.apply.and.returnValue(throwError(()=>({status:409,error:{message:'changed'}})));
    await component.save(true);expect(timetables.publish).not.toHaveBeenCalled();expect(component.accepted).toBeNull();
    await component.preview();component.publicationAllowed=false;await component.save(true);await component.publishSaved();
    expect(api.apply).toHaveBeenCalledTimes(1);expect(timetables.publish).not.toHaveBeenCalled();
  });
});
