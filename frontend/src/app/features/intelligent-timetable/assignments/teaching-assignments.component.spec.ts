import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of, Subject, throwError } from 'rxjs';
import { TeachingAssignmentsComponent } from './teaching-assignments.component';
import { TeachingAssignmentService } from '../../../core/services/teaching-assignment.service';
import { TimetableSettingsService } from '../../../core/services/timetable-settings.service';
import { TeachingOverview } from '../../../core/models/teaching-assignment.models';

describe('Teaching assignments', () => {
  let component: TeachingAssignmentsComponent;
  let api: jasmine.SpyObj<TeachingAssignmentService>;
  const overview = (): TeachingOverview => ({ revision: 3, subjects: [{ id: 1, name: 'رياضيات', color: '#2563eb', revision: 1 }],
    classrooms: [{ id: 10, name: '1/A', stage: 1, gradeLevel: 1 }, { id: 20, name: '1/B', stage: 1, gradeLevel: 1 }],
    requirements: [10, 20].map(id => ({ id, subjectId: 1, classroomId: id, classroomName: `${id}`, revision: 1, totalWeeklyPeriods: 4,
      rules: { individualPeriodCount: 2, pairedBlockCount: 1, timePreference: 'None', earliestPeriodSequence: null,
        latestPreferredPeriodSequence: null, allowedDays: [], fixedSlots: [], roomIds: [], preferredRoomId: null } })),
    assignments: [], teachers: [1, 2].map(id => ({ id, instructorProfileId: id, name: `معلم ${id}`, specialization: 'رياضيات',
      isActive: true, allocatedPeriods: 0, maximumWeeklyPeriods: 6, remainingPeriods: 6, subjectCount: 0, classroomCount: 0, isVisiting: false, warnings: [] })), warnings: [] });
  beforeEach(() => {
    api = jasmine.createSpyObj('TeachingAssignmentService', ['get', 'save', 'teachers']);
    api.teachers.and.returnValue(of({ isSuccess: true, data: { items: overview().teachers, totalCount: 2 }, errors: [], message: '' }));
    TestBed.configureTestingModule({ providers: [{ provide: TeachingAssignmentService, useValue: api },
      { provide: TimetableSettingsService, useValue: {} }, { provide: ActivatedRoute, useValue: {} }] });
    component = TestBed.runInInjectionContext(() => new TeachingAssignmentsComponent()); component.setupId = 1; component.data = overview();
  });
  it('preserves selected teachers when searching and paging the server table', () => {
    component.open(component.data!.requirements[0]); component.toggle(component.data!.teachers[0]);
    component.teacherSearch = 'معلم'; component.searchTeachers(); component.lazyTeachers({ first: 10, rows: 10 });
    expect(component.selected(1)).toBeTrue(); expect(component.members.length).toBe(1);
    expect(api.teachers.calls.mostRecent().args[1]['page']).toBe(2);
  });
  it('includes all unsaved cells in capacity even when the matrix is filtered', () => {
    component.open(component.data!.requirements[0]); component.toggle(component.data!.teachers[0]); component.confirmCell();
    component.classroomId = 20; component.open(component.data!.requirements[1]); component.toggle(component.data!.teachers[0]);
    expect(component.workload(1, true).periods).toBe(8); expect(component.dialogError).toContain('تجاوز');
    component.confirmCell(); expect(component.drafts.size).toBe(1);
  });
  it('requires an explicit multi-teacher mode and keeps each pair with one teacher', () => {
    component.open(component.data!.requirements[0]); component.toggle(component.data!.teachers[0]); component.toggle(component.data!.teachers[1]);
    expect(component.mode).toBeNull(); expect(component.dialogError).not.toBe('');
    component.mode = 'SplitQuota'; component.members = [
      { teacherTimetableProfileId: 1, allocatedPeriodCount: 1, allocatedPairedBlockCount: 1 },
      { teacherTimetableProfileId: 2, allocatedPeriodCount: 3, allocatedPairedBlockCount: 0 }];
    expect(component.dialogError).not.toBe('');
    component.members[0].allocatedPeriodCount = 3; component.members[1].allocatedPeriodCount = 1;
    expect(component.dialogError).toBe(''); component.confirmCell(); expect(component.drafts.size).toBe(1);
  });
  it('co-teaching counts the whole subject quota against each teacher', () => {
    component.open(component.data!.requirements[0]); component.toggle(component.data!.teachers[0]); component.toggle(component.data!.teachers[1]);
    component.mode = 'CoTeaching'; component.modeChanged(); expect(component.dialogError).toBe('');
    component.confirmCell(); expect(component.workload(1).periods).toBe(4); expect(component.workload(2).periods).toBe(4);
  });
  it('keeps the draft after an HTTP conflict and sends the original setup revision', () => {
    api.save.and.returnValue(throwError(() => ({ status: 409 })));
    component.open(component.data!.requirements[0]); component.toggle(component.data!.teachers[0]); component.confirmCell(); component.save();
    expect(api.save.calls.mostRecent().args[1]).toBe(3); expect(component.drafts.size).toBe(1);
    expect(component.saving).toBeFalse(); expect(component.error).not.toBe(''); expect(component.hasUnsavedChanges()).toBeTrue();
  });
  it('canceling a dialog does not mutate the persisted cell and unassigning releases draft load', () => {
    component.open(component.data!.requirements[0]); component.toggle(component.data!.teachers[0]);
    spyOn(window, 'confirm').and.returnValue(true); component.closeDialog(); expect(component.cell(10).members.length).toBe(0);
    component.open(component.data!.requirements[0]); component.toggle(component.data!.teachers[0]); component.confirmCell();
    component.clear(component.data!.requirements[0]); expect(component.workload(1).periods).toBe(0);
  });
  it('does not mark unchanged cells as unsaved and reverting releases the draft', () => {
    component.open(component.data!.requirements[0]); component.confirmCell();
    expect(component.hasUnsavedChanges()).toBeFalse();
    component.open(component.data!.requirements[0]); component.toggle(component.data!.teachers[0]); component.confirmCell();
    expect(component.drafts.size).toBe(1);
    component.clear(component.data!.requirements[0]);expect(component.hasUnsavedChanges()).toBeFalse();
  });
  it('does not warn when a picker selection returns to the same allocation in another order', () => {
    const data=overview();data.assignments=[{id:10,classSubjectRequirementId:10,mode:'CoTeaching',members:data.teachers.map(t=>({
      teacherTimetableProfileId:t.id,instructorProfileId:t.instructorProfileId,name:t.name,allocatedPeriodCount:4,allocatedPairedBlockCount:1,statementCategory:'Specialization'}))}];
    component.data=data;component.open(data.requirements[0]);component.removeMember(1);component.toggle(data.teachers[0]);
    component.mode='CoTeaching';component.modeChanged();
    expect(component.hasUnsavedChanges()).toBeFalse();expect(component.members.every(m=>m.statementCategory==='Specialization')).toBeTrue();
    component.confirmCell();expect(component.drafts.size).toBe(0);component.open(data.requirements[0]);
    const confirmation=spyOn(window,'confirm');component.closeDialog();
    expect(confirmation).not.toHaveBeenCalled();expect(component.dialogOpen).toBeFalse();
  });
  it('keeps stage-local grades separate and pages classrooms without losing off-page load', () => {
    const data=overview();data.classrooms=Array.from({length:24},(_,i)=>({id:i+1,name:'Class '+(i+1),stage:i<12?1:2,gradeLevel:1}));
    data.requirements=data.classrooms.map(c=>({...data.requirements[0],id:c.id,classroomId:c.id,classroomName:c.name}));component.data=data;
    expect(component.gradeGroups.length).toBe(2);expect(component.pageGroups.flatMap(g=>g.classrooms).length).toBe(10);
    component.open(data.requirements[0]);component.toggle(data.teachers[0]);component.confirmCell();component.matrixPage=3;
    expect(component.pageGroups.flatMap(g=>g.classrooms).length).toBe(4);expect(component.workload(1).periods).toBe(4);
    component.selectGrade('2:1');expect(component.rows.length).toBe(12);expect(component.rows.every(c=>c.stage===2)).toBeTrue();
    component.search='Class 24';component.filtersChanged();expect(component.rows.map(c=>c.id)).toEqual([24]);
    expect(component.pending).toBe(23);
  });
  it('indexes the whole ledger once until the immutable draft or server snapshot changes', () => {
    const before=component.ledger;component.workload(1);component.rows;component.requirement(10,1);
    expect(component.ledger).toBe(before);
    component.open(component.data!.requirements[0]);component.toggle(component.data!.teachers[0]);component.confirmCell();
    expect(component.ledger).not.toBe(before);expect(component.workload(1).periods).toBe(4);
  });
  it('retains an orphan teacher load until the old assignment is explicitly removed', () => {
    const data=overview();data.assignments=[{id:99,classSubjectRequirementId:99,mode:'SingleTeacher',members:[{teacherTimetableProfileId:1,instructorProfileId:1,name:'Old',allocatedPeriodCount:3,allocatedPairedBlockCount:0}]}];
    component.data=data;expect(component.workload(1).periods).toBe(3);expect(component.orphanedAssignments.length).toBe(1);
    component.clearId(99);expect(component.workload(1).periods).toBe(0);expect(component.invalidDraft).toBeFalse();
  });
  it('keeps every server picker filter and direction when paging', () => {
    component.open(component.data!.requirements[0]);component.activeFilter=null;component.visitingFilter=false;
    component.specialization='Math';component.capacityOnly=true;component.sortTeachers('remaining');component.reverseTeachers();
    component.lazyTeachers({first:25,rows:25,sortField:'remaining',sortOrder:-1});
    expect(api.teachers.calls.mostRecent().args[1]).toEqual(jasmine.objectContaining({page:2,pageSize:25,isActive:'',isVisiting:false,specialization:'Math',hasCapacity:true,sort:'remaining',descending:true}));
  });
  it('ignores stale teacher searches and preserves selected members after a failed page', () => {
    component.open(component.data!.requirements[0]);component.toggle(component.data!.teachers[0]);
    const old=new Subject<any>(),latest=new Subject<any>();api.teachers.and.returnValues(old,latest);
    component.searchTeachers();component.searchTeachers();latest.error({status:503});
    old.next({isSuccess:true,data:{items:overview().teachers,totalCount:2}});
    expect(component.teacherRows).toEqual([]);expect(component.teacherError).not.toBe('');expect(component.selected(1)).toBeTrue();
  });
  it('does not accept delayed results from a previously closed picker', () => {
    const pending=new Subject<any>();api.teachers.and.returnValue(pending);
    component.open(component.data!.requirements[0]);component.closeDialog();
    pending.next({isSuccess:true,data:{items:overview().teachers,totalCount:9}});
    expect(component.teacherTotal).toBe(0);expect(component.dialogOpen).toBeFalse();
  });
  it('keeps Escape and navigation protection scoped to real edits', () => {
    const confirmation=spyOn(window,'confirm').and.returnValue(false);
    component.open(component.data!.requirements[0]);component.escape();expect(confirmation).not.toHaveBeenCalled();
    component.open(component.data!.requirements[0]);component.toggle(component.data!.teachers[0]);component.escape();
    expect(component.dialogOpen).toBeTrue();expect(component.hasUnsavedChanges()).toBeTrue();
    const event={preventDefault:jasmine.createSpy(),returnValue:undefined} as unknown as BeforeUnloadEvent;
    component.beforeUnload(event);expect(event.preventDefault).toHaveBeenCalled();
  });
  it('prevents edits while the atomic save is running and uses the returned snapshot', () => {
    const pending=new Subject<any>();api.save.and.returnValue(pending);
    component.open(component.data!.requirements[0]);component.toggle(component.data!.teachers[0]);component.confirmCell();component.save();component.save();
    component.clearId(10);component.open(component.data!.requirements[1]);expect(component.dialogOpen).toBeFalse();expect(component.drafts.size).toBe(1);
    expect(api.save).toHaveBeenCalledTimes(1);pending.next({isSuccess:true,data:{...overview(),revision:4}});pending.complete();
    expect(component.data!.revision).toBe(4);expect(component.hasUnsavedChanges()).toBeFalse();
  });
  it('guards conflict refresh and preserves the draft until an accepted reload succeeds', () => {
    api.save.and.returnValue(throwError(()=>({status:409})));
    component.open(component.data!.requirements[0]);component.toggle(component.data!.teachers[0]);component.confirmCell();component.save();
    const confirmation=spyOn(window,'confirm').and.returnValue(false);component.reload();expect(api.get).not.toHaveBeenCalled();expect(component.conflict).toBeTrue();
    const pending=new Subject<any>();api.get.and.returnValue(pending);confirmation.and.returnValue(true);component.reload();expect(component.drafts.size).toBe(1);
    pending.next({isSuccess:true,data:overview()});pending.complete();expect(component.drafts.size).toBe(0);expect(component.conflict).toBeFalse();
  });
  it('ignores an old setup response after the manager chooses another setup', () => {
    const first=new Subject<any>(),second=new Subject<any>();api.get.and.returnValues(first,second);
    component.chooseSetup(1);component.chooseSetup(2);second.next({isSuccess:true,data:{...overview(),revision:8}});second.complete();
    first.next({isSuccess:true,data:{...overview(),revision:2}});first.complete();expect(component.data!.revision).toBe(8);expect(component.loading).toBeFalse();
  });
  it('renders the grouped ledger, actual-change save bar and labelled native picker controls', async () => {
    TestBed.resetTestingModule();api.get.and.returnValue(of({isSuccess:true,data:overview(),message:'',errors:[]}));
    const settings={getOverview:()=>of({isSuccess:true,data:{selectedAcademicYearId:1,selectedProfile:{id:1},academicYears:[],profiles:[]}})};
    await TestBed.configureTestingModule({imports:[TeachingAssignmentsComponent,NoopAnimationsModule],providers:[provideRouter([]),
      {provide:TeachingAssignmentService,useValue:api},{provide:TimetableSettingsService,useValue:settings}]}).compileComponents();
    const fixture=TestBed.createComponent(TeachingAssignmentsComponent);fixture.detectChanges();await fixture.whenStable();fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('.grade-divider').length).toBe(1);expect(fixture.nativeElement.querySelector('.draft-bar')).toBeNull();
    (fixture.nativeElement.querySelector('.assignment-cell') as HTMLButtonElement).click();fixture.detectChanges();await fixture.whenStable();fixture.detectChanges();
    const checkbox=fixture.nativeElement.querySelector('.teacher-choice input') as HTMLInputElement;expect(checkbox.getAttribute('aria-label')).toContain('اختيار');
    checkbox.click();fixture.detectChanges();fixture.componentInstance.confirmCell();fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.draft-bar')).not.toBeNull();expect(fixture.nativeElement.querySelector('.changed-cell')).not.toBeNull();
  });
});
