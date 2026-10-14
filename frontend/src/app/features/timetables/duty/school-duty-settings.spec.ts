import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import { Subject } from 'rxjs';
import { SchoolDutyService } from '../../../core/services/school-duty.service';
import { DutyWorkspace } from '../../../core/models/school-duty.models';
import { SchoolDutyComponent } from './school-duty.component';
import { AuthService } from '../../../core/services/auth.service';

describe('Duty settings amendment', () => {
  let component: SchoolDutyComponent;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([]),
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: new Map() } } },
      { provide: SchoolDutyService, useValue: {} },
      { provide: AuthService, useValue: { activeSchoolId:()=>1,currentUser:()=>({userId:'a'}) } },
      { provide: TranslateService, useValue: { currentLang: 'ar', onLangChange: new Subject(), instant: (key: string) => key } }
    ] });
    component = TestBed.runInInjectionContext(() => new SchoolDutyComponent());
    component.data.set({ today: '2030-01-06', termId: 1, terms: [{ id: 1, from: '2030-01-06', to: '2030-02-06' }], settings: null,
      teachers: [{ userId: 'a', name: 'A' }, { userId: 'b', name: 'B' }, { userId: 'c', name: 'C', isEligible: false }], suggestedWeekdays: [0,2,4] } as DutyWorkspace);
  });
  it('starts with every active teacher and only timetable study weekdays', () => {
    component.openSetup(); expect(component.draft()!.teacherIds).toEqual(['a','b']); expect(component.draft()!.weekdays).toEqual([0,2,4]);
    expect(component.draft()!.permanentlyExcludedTeacherIds).toEqual([]);
  });
  it('applies permanent exclusion once and restores participation', () => {
    component.openSetup(); component.excludePermanently('a'); component.excludePermanently('a');
    expect(component.draft()!.teacherIds).toEqual(['b']); expect(component.draft()!.permanentlyExcludedTeacherIds).toEqual(['a']);
    component.restorePermanent('a'); component.restorePermanent('a'); expect(component.draft()!.teacherIds).toEqual(['b','a']);
  });
  it('keeps legacy omissions explicit while preserving rotation order', () => {
    component.openSetup(); const old = { ...component.draft()!, teacherIds: ['b'], permanentlyExcludedTeacherIds: undefined, weekdays: [6] };
    component.data.update(x => ({ ...x!, settings: old })); component.openSetup();
    expect(component.draft()!.teacherIds).toEqual(['b']); expect(component.draft()!.permanentlyExcludedTeacherIds).toEqual(['a']); expect(component.draft()!.weekdays).toEqual([0,2,4]);
  });
  it('adds newly active teachers at next review without restoring explicit exclusions', () => {
    component.openSetup(); component.excludePermanently('a'); component.data.update(x => ({ ...x!, settings: component.draft(), teachers: [...x!.teachers, { userId: 'd', profileId: 4, name: 'D' }] })); component.openSetup();
    expect(component.draft()!.teacherIds).toEqual(['b','d']); expect(component.draft()!.permanentlyExcludedTeacherIds).toEqual(['a']);
  });
  it('dated exclusions retain participation and survive permanent exclusion/restoration', () => {
    component.openSetup(); component.exclusionMode = 'dated'; component.exclusionTeacher = 'a'; component.exclusionFrom = new Date(2030,0,6); component.exclusionTo = new Date(2030,0,8); component.exclusionReason = 'Reason'; component.addExclusion();
    component.excludePermanently('a'); component.restorePermanent('a'); expect(component.draft()!.exclusions.length).toBe(1); expect(component.draft()!.teacherIds).toContain('a');
  });
  it('rejects reversed dates and keeps form inputs', () => {
    component.openSetup(); component.exclusionMode = 'dated'; component.exclusionTeacher = 'a'; component.exclusionFrom = new Date(2030,0,8); component.exclusionTo = new Date(2030,0,6); component.exclusionReason = 'Reason'; component.addExclusion();
    expect(component.draft()!.exclusions).toEqual([]); expect(component.error()).toBe('DUTY.EXCLUSION_DATE_ERROR'); expect(component.exclusionReason).toBe('Reason');
  });
  it('permanent picker action requires no dates or manufactured reason', () => {
    component.openSetup(); component.exclusionTeacher = 'a'; component.addExclusion(); expect(component.draft()!.permanentlyExcludedTeacherIds).toEqual(['a']); expect(component.error()).toBe('');
  });
  it('quick dated action starts at the canonical school duty date rather than the host date', () => {
    component.openSetup(); component.chooseDatedExclusion('a');
    expect(component.exclusionMode).toBe('dated'); expect(component.iso(component.exclusionFrom!)).toBe('2030-01-06');
    expect(component.iso(component.exclusionTo!)).toBe('2030-01-06'); expect(component.draft()!.teacherIds).toEqual(['a','b']);
  });
  it('does not restore an inactive teacher into the rotation', () => {
    component.openSetup(); component.restorePermanent('c'); expect(component.draft()!.teacherIds).not.toContain('c');
  });
  it('shows at most ten days and resets pagination when switching between own and teacher filters', () => {
    const days=Array.from({length:23},(_,i)=>({id:i+1,date:`2030-01-${String(i+1).padStart(2,'0')}`,teacherId:i%2?'b':'a',teacher:i%2?'B':'A',isMine:i%2===0}));
    component.data.set({...component.data()!,days} as DutyWorkspace);
    expect(component.pageDays().length).toBe(10);expect(component.pageCount()).toBe(3);
    component.movePage(2);expect(component.pageDays().length).toBe(3);
    component.selectScheduleFilter('mine');expect(component.currentPage()).toBe(0);expect(component.pageDays().every(x=>x.isMine)).toBeTrue();
    component.movePage(1);component.selectScheduleTeacher('b');expect(component.currentPage()).toBe(0);expect(component.pageDays().every(x=>x.teacherId==='b')).toBeTrue();
    component.selectScheduleFilter('all');expect(component.teacher()).toBeNull();expect(component.pageCount()).toBe(3);
  });
  it('uses school today for the alert and finds the nearest own date even if the response is unsorted', () => {
    component.data.set({...component.data()!,days:[{date:'2030-01-10',isMine:true},{date:'2030-01-06',isMine:false},{date:'2030-01-08',isMine:true}]} as DutyWorkspace);
    expect(component.ownToday()).toBeUndefined();expect(component.next()?.date).toBe('2030-01-08');
    component.data.set({...component.data()!,days:[{date:'2030-01-06',isMine:true}]} as DutyWorkspace);
    expect(component.ownToday()?.date).toBe('2030-01-06');
  });
  it('ignores a selected future assignment in the edit page and selects only the current school day', () => {
    const response=new Subject<any>();const api=TestBed.inject(SchoolDutyService) as any;api.context=jasmine.createSpy().and.returnValue(response);
    (TestBed.inject(ActivatedRoute).snapshot.queryParamMap as any).set('execution','99');
    spyOn(TestBed.inject(Router),'navigate').and.returnValue(Promise.resolve(true));
    component.activateSection('execution');
    response.next({isSuccess:true,data:{...component.data()!,today:'2030-01-06',canManage:false,days:[{id:99,date:'2030-01-08',isMine:true},{id:7,date:'2030-01-06',isMine:true}]}});
    expect(api.context).toHaveBeenCalledWith(null);expect(component.executionId).toBe(7);expect(component.inspection).toBeFalse();
  });
  it('shows no duty today instead of opening the next available own assignment', () => {
    const response=new Subject<any>();(TestBed.inject(SchoolDutyService) as any).context=()=>response;
    spyOn(TestBed.inject(Router),'navigate').and.returnValue(Promise.resolve(true));
    component.activateSection('execution');
    response.next({isSuccess:true,data:{...component.data()!,canManage:false,days:[{id:9,date:'2030-01-08',isMine:true},{id:7,date:'2030-01-06',isMine:false}]}});
    expect(component.executionId).toBeNull();expect(component.executionLoading()).toBeFalse();
  });
  it('does not apply an old current-day response after leaving the edit page', () => {
    const response=new Subject<any>();(TestBed.inject(SchoolDutyService) as any).context=()=>response;
    component.activateSection('execution');component.activateSection('schedule');
    response.next({isSuccess:true,data:{...component.data()!,days:[{id:7,date:'2030-01-06',isMine:true}]}});
    expect(component.section()).toBe('schedule');expect(component.executionId).toBeNull();
  });
  it('waits for historical query parameters to settle before opening the edit section', () => {
    const api=TestBed.inject(SchoolDutyService) as any;api.context=jasmine.createSpy();
    spyOn(TestBed.inject(Router),'getCurrentNavigation').and.returnValue({} as any);
    component.activateSection('execution');expect(api.context).not.toHaveBeenCalled();
    const query=TestBed.inject(ActivatedRoute).snapshot.queryParamMap as any;query.set('review','true');query.set('execution','8');
    (component as any).syncNavigation(true);
    expect(component.executionId).toBe(8);expect(component.inspection).toBeTrue();expect(api.context).not.toHaveBeenCalled();
  });
  it('does not let a pending today request overwrite subsequent historical inspection', () => {
    const response=new Subject<any>();(TestBed.inject(SchoolDutyService) as any).context=()=>response;
    component.activateSection('execution');
    const query=TestBed.inject(ActivatedRoute).snapshot.queryParamMap as any;query.set('review','true');query.set('execution','8');
    (component as any).syncNavigation(true);
    response.next({isSuccess:true,data:{...component.data()!,canManage:false,days:[{id:7,date:'2030-01-06',isMine:true}]}});
    expect(component.executionId).toBe(8);expect(component.inspection).toBeTrue();
  });
  it('ignores picker remount emissions for the already selected term and duty', () => {
    const api=TestBed.inject(SchoolDutyService) as any;api.context=jasmine.createSpy();
    const navigate=spyOn(TestBed.inject(Router),'navigate');
    component.termId=1;component.executionId=8;
    component.switchTerm(1);component.selectExecution(8);
    expect(component.executionId).toBe(8);expect(api.context).not.toHaveBeenCalled();expect(navigate).not.toHaveBeenCalled();
  });
  it('retains explicit overdue completion in the archive only for the current owner and unsubmitted revision', () => {
    component.executionId=7;
    component.data.set({...component.data()!,canManage:false,userId:'a',days:[{id:7,date:'2030-01-05',isMine:true,revision:2,originalTeacherId:'a',originalTeacher:'A',teacherId:'a',teacher:'A',protected:false,locked:false,exceptionCode:null,attendance:null}],ownHistory:[]});
    expect(component.canResumeArchive).toBeTrue();
    component.data.update(x=>({...x!,ownHistory:[{assignmentId:7,assignmentRevision:1,authorUserId:'a',submitted:true} as any]}));expect(component.canResumeArchive).toBeTrue();
    component.data.update(x=>({...x!,ownHistory:[{assignmentId:7,assignmentRevision:2,authorUserId:'a',submitted:true} as any]}));expect(component.canResumeArchive).toBeFalse();
    component.data.update(x=>({...x!,ownHistory:[],days:[{...x!.days[0],isMine:false}]}));expect(component.canResumeArchive).toBeFalse();
    component.data.update(x=>({...x!,days:[{...x!.days[0],isMine:true,date:'2030-01-08'}]}));expect(component.canResumeArchive).toBeFalse();
  });
  it('does not reopen a settings draft by refreshing before post-publication navigation finishes', async () => {
    const result=new Subject<any>();(TestBed.inject(SchoolDutyService) as any).publish=()=>result;
    let completeNavigation!: (value: boolean) => void;
    spyOn(TestBed.inject(Router),'navigate').and.returnValue(new Promise<boolean>(resolve=>completeNavigation=resolve));
    const load=spyOn(component,'load');component.openSetup();component.section.set('settings');
    (component as any).publishRequest={};component.publish();result.next({isSuccess:true,data:{planId:1,revision:2}});
    expect(component.draft()).toBeNull();expect(component.hasDraft()).toBeFalse();expect(load).not.toHaveBeenCalled();
    completeNavigation(true);await Promise.resolve();expect(load).toHaveBeenCalledTimes(1);
  });
});
