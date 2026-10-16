import {TestBed} from '@angular/core/testing';
import {HttpHeaders,HttpResponse} from '@angular/common/http';
import {Subject,throwError} from 'rxjs';
import {AssignmentStatementComponent} from './assignment-statement.component';
import {TeachingAssignmentService} from '../../../core/services/teaching-assignment.service';
import {TeachingOverview} from '../../../core/models/teaching-assignment.models';

describe('Assignment statement',()=>{
  let component:AssignmentStatementComponent;let api:jasmine.SpyObj<TeachingAssignmentService>;
  const overview=():TeachingOverview=>({revision:8,subjects:[{id:1,name:'رياضيات',color:'#12563a',revision:1}],classrooms:[{id:1,name:'1/1',stage:1,gradeLevel:1},{id:2,name:'1/1',stage:2,gradeLevel:1}],
    requirements:[1,2].map(id=>({id,classroomId:id,classroomName:'1/1',subjectId:1,revision:1,totalWeeklyPeriods:4,rules:{individualPeriodCount:2,pairedBlockCount:1,timePreference:'None',earliestPeriodSequence:null,latestPreferredPeriodSequence:null,allowedDays:[],fixedSlots:[],roomIds:[],preferredRoomId:null}})),
    assignments:[1,2].map(id=>({id,classSubjectRequirementId:id,mode:'SingleTeacher',members:[{teacherTimetableProfileId:1,instructorProfileId:1,name:'معلم',allocatedPeriodCount:4,allocatedPairedBlockCount:1}]})),
    teachers:[{id:1,instructorProfileId:1,name:'معلم',specialization:'رياضيات',isActive:true,allocatedPeriods:8,maximumWeeklyPeriods:24,remainingPeriods:16,subjectCount:1,classroomCount:2,isVisiting:false,warnings:[]}],warnings:[]});
  beforeEach(()=>{api=jasmine.createSpyObj('TeachingAssignmentService',['save','statement']);TestBed.configureTestingModule({providers:[{provide:TeachingAssignmentService,useValue:api}]});
    component=TestBed.runInInjectionContext(()=>new AssignmentStatementComponent());component.setupId=2;component.data=overview();component.ngOnChanges();});
  it('starts with explicit full-setup scope and never guesses classification from matching specialization text',()=>{
    expect(component.scope).toBe('all');expect(component.rows.length).toBe(1);expect(component.missing).toBe(1);component.prepare();expect(api.statement).not.toHaveBeenCalled();
  });
  it('saves classification only in the explicitly selected stage with the original revision and allocations',()=>{
    component.changeScope('stage:2');component.rows[0].category='Other';const pending=new Subject<any>();api.save.and.returnValue(pending);component.saveCategories();
    const [setup,revision,changes]=api.save.calls.mostRecent().args;expect(setup).toBe(2);expect(revision).toBe(8);expect(changes.length).toBe(1);expect(changes[0].classSubjectRequirementId).toBe(2);
    expect(changes[0].members[0]).toEqual(jasmine.objectContaining({allocatedPeriodCount:4,allocatedPairedBlockCount:1,statementCategory:'Other'}));
    component.saveCategories();expect(api.save).toHaveBeenCalledTimes(1);pending.next({isSuccess:true,data:{...overview(),revision:9}});pending.complete();expect(component.busy).toBeFalse();
  });
  it('blocks preview of uncommitted classifications and preserves them after conflict',async()=>{
    component.rows[0].category='Specialization';component.prepare();expect(api.statement).not.toHaveBeenCalled();
    api.save.and.returnValue(throwError(()=>({status:409})));component.saveCategories();await Promise.resolve();await Promise.resolve();
    expect(component.conflict).toBeTrue();expect(component.rows[0].category).toBe('Specialization');expect(component.hasUnsavedWork()).toBeTrue();
  });
  it('allows saving a completed subset without losing progress while export still requires all classifications',()=>{
    component.data.assignments[1].members[0].teacherTimetableProfileId=2;component.data.teachers.push({...component.data.teachers[0],id:2,name:'معلم ثان'});component.ngOnChanges();
    expect(component.missing).toBe(2);component.rows[0].category='Specialization';expect(component.missing).toBe(1);
    const pending=new Subject<any>();api.save.and.returnValue(pending);component.saveCategories();expect(api.save).toHaveBeenCalledTimes(1);
    expect(api.save.calls.mostRecent().args[2]).toHaveSize(1);
    pending.next({isSuccess:true,data:{...component.data,revision:9,assignments:component.data.assignments.map((cell,i)=>({...cell,members:cell.members.map(m=>({...m,statementCategory:i===0?'Specialization':null}))}))}});pending.complete();
    expect(component.busy).toBeFalse();expect(component.missing).toBe(1);expect(component.hasUnsavedWork()).toBeFalse();component.prepare();expect(api.statement).not.toHaveBeenCalled();
  });
  it('does not change scope or close with real classification changes unless discard is accepted',()=>{
    component.rows[0].category='Activity';spyOn(window,'confirm').and.returnValue(false);spyOn(component.closed,'emit');component.changeScope('stage:1');component.close();
    expect(component.scope).toBe('all');expect(component.closed.emit).not.toHaveBeenCalled();
  });
  it('previews a saved revision and retains the exact received PDF and filename',()=>{
    component.data.assignments.forEach(a=>a.members[0].statementCategory='Specialization');component.ngOnChanges();
    const blob=new Blob(['%PDF-test'],{type:'application/pdf'}),pending=new Subject<HttpResponse<Blob>>();api.statement.and.returnValue(pending);
    component.prepare();expect(component.hasUnsavedWork()).toBeFalse();spyOn(component.closed,'emit');component.close();expect(component.closed.emit).toHaveBeenCalled();
    pending.next(new HttpResponse({body:blob,headers:new HttpHeaders({'Content-Disposition':"attachment; filename*=UTF-8''statement.pdf"})}));pending.complete();
    expect(api.statement).toHaveBeenCalledOnceWith(2,8,'all');expect(component.blob).toBe(blob);expect(component.fileName).toBe('statement.pdf');expect(component.busy).toBeFalse();
  });
  it('reads binary JSON errors rather than reporting a successful download',async()=>{
    component.data.assignments.forEach(a=>a.members[0].statementCategory='Specialization');component.ngOnChanges();
    api.statement.and.returnValue(throwError(()=>({status:400,error:new Blob([JSON.stringify({message:'تصنيف مطلوب'})],{type:'application/json'})})));
    component.prepare();await new Promise(resolve=>setTimeout(resolve,20));expect(component.blob).toBeNull();expect(component.error).toBe('تصنيف مطلوب');
  });
});
