import { TestBed } from '@angular/core/testing';
import { HttpResponse } from '@angular/common/http';
import { TranslateModule } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { CoverageRegisterService } from '../../../core/services/coverage-register.service';
import { CoverageRegisterContext, CoverageRegisterInfo } from '../../../core/models/coverage-register.models';
import { CoverageRegisterComponent } from '../../intelligent-timetable/substitutions/coverage-register.component';

describe('Retained coverage register',()=>{
  let api:jasmine.SpyObj<CoverageRegisterService>,component:CoverageRegisterComponent,info:CoverageRegisterInfo,blob:Blob;
  const context=():CoverageRegisterContext=>({date:'2026-10-18',sourceRevision:'a'.repeat(64),issue:null,teacherCount:1,periodCount:2,executedPeriods:0,isComplete:false,history:{items:[],page:1,pageSize:10,totalCount:0,totalPages:0,hasNext:false,hasPrevious:false}});
  beforeEach(async()=>{
    blob=new Blob(['%PDF-'+ 'a'.repeat(150)],{type:'application/pdf'});
    const sha256=[...new Uint8Array(await crypto.subtle.digest('SHA-256',await blob.arrayBuffer()))].map(n=>n.toString(16).padStart(2,'0')).join('').toUpperCase();
    info={id:1,date:'2026-10-18',revision:1,sourceRevision:'a'.repeat(64),isComplete:false,fileName:'register.pdf',sha256,issuedAt:'2026-10-18T08:00:00Z'};
    api=jasmine.createSpyObj('CoverageRegisterService',['context','issue','content']);
    api.context.and.callFake(()=>of({isSuccess:true,data:context(),errors:[],message:''}));
    api.issue.and.returnValue(of({isSuccess:true,data:info,errors:[],message:''}));
    api.content.and.returnValue(of(new HttpResponse({body:blob})));
    TestBed.configureTestingModule({imports:[TranslateModule.forRoot()],providers:[{provide:CoverageRegisterService,useValue:api}]});
    component=TestBed.runInInjectionContext(()=>new CoverageRegisterComponent());component.date='2026-10-18';await component.load();
  });
  it('reads without issuing, then downloads the exact retained preview blob',async()=>{
    expect(api.issue).not.toHaveBeenCalled();await component.prepare();expect(component.blob).toBe(blob);
    spyOn(URL,'createObjectURL').and.returnValue('blob:register-test');spyOn(HTMLAnchorElement.prototype,'click');
    component.download();expect(URL.createObjectURL).toHaveBeenCalledOnceWith(blob);
    expect(api.issue.calls.mostRecent().args).toEqual(['2026-10-18','a'.repeat(64),'Day']);
  });
  it('switches to a canonical school week and issues that scope without mixing daily preview or history',async()=>{
    component.date='2026-10-21';component.blob=blob;component.selected=info;component.page=2;
    api.context.and.callFake(()=>of({isSuccess:true,data:{...context(),scope:'Week',endDate:'2026-10-24'},errors:[],message:''}));
    await component.changeScope('Week');expect(component.scope).toBe('Week');expect(component.blob).toBeNull();expect(component.selected).toBeNull();expect(component.page).toBe(1);
    expect(api.context.calls.mostRecent().args).toEqual(['2026-10-21',1,'Week']);expect(api.issue).not.toHaveBeenCalled();
    api.issue.and.returnValue(of({isSuccess:true,data:{...info,scope:'Week',endDate:'2026-10-24'},errors:[],message:''}));
    await component.prepare();expect(api.issue.calls.mostRecent().args).toEqual(['2026-10-18','a'.repeat(64),'Week']);expect(component.blob).toBe(blob);
    component.busy=true;await component.changeScope('Day');expect(component.scope).toBe('Week');component.busy=false;
    await component.changeScope('Day');expect(component.blob).toBeNull();expect(component.selected).toBeNull();expect(api.context.calls.mostRecent().args).toEqual(['2026-10-21',1,'Day']);
  });
  it('retries the same source after a lost response and retains the selected issue after a content failure',async()=>{
    api.issue.and.returnValue(throwError(()=>({status:0})));await component.prepare();
    component.context!.sourceRevision='b'.repeat(64);api.issue.and.returnValue(of({isSuccess:true,data:info,errors:[],message:''}));
    api.content.and.returnValue(throwError(()=>({status:0})));await component.prepare();
    expect(api.issue.calls.mostRecent().args[1]).toBe('a'.repeat(64));expect(component.selected?.id).toBe(1);expect(component.blob).toBeNull();
    api.content.and.returnValue(of(new HttpResponse({body:blob})));await component.open(info);expect(component.blob).toBe(blob);
  });
  it('rejects a successful non-PDF response and a PDF whose bytes differ from the retained hash',async()=>{
    api.content.and.returnValue(of(new HttpResponse({body:new Blob(['<html>error</html>'],{type:'text/html'})})));await component.open(info);expect(component.blob).toBeNull();expect(component.error).toBe('COVERAGE_REGISTER.INVALID_PDF');
    api.content.and.returnValue(of(new HttpResponse({body:new Blob(['%PDF-'+ 'b'.repeat(150)],{type:'application/pdf'})})));await component.open(info);expect(component.blob).toBeNull();expect(component.error).toBe('COVERAGE_REGISTER.INTEGRITY');
  });
  it('changing date removes the previous document and clean reads do not trigger reload warnings',async()=>{
    await component.open(info);component.date='2026-10-19';await component.changeDate();expect(component.blob).toBeNull();expect(component.selected).toBeNull();
    const event=new Event('beforeunload',{cancelable:true}) as BeforeUnloadEvent;component.beforeUnload(event);expect(event.defaultPrevented).toBeFalse();
    component.issuing=true;expect(component.canLeave()).toBeFalse();component.beforeUnload(event);expect(event.defaultPrevented).toBeTrue();
  });
  it('refreshes the first history page after issuing from a later page without mixing pages',async()=>{
    component.page=2;component.context!.history={...context().history,page:2,totalCount:12,items:[{...info,id:2,revision:2}]};
    const newest=Array.from({length:10},(_,i)=>({...info,id:13-i,revision:13-i}));
    api.context.and.returnValue(of({isSuccess:true,data:{...context(),history:{...context().history,items:newest,totalCount:13,page:1}},errors:[],message:''}));
    await component.prepare();expect(component.page).toBe(1);expect(component.context!.history.items).toEqual(newest);expect(component.blob).toBe(blob);
    component.page=2;component.context!.history={...component.context!.history,page:2};
    api.context.and.returnValue(throwError(()=>({status:0})));await component.prepare();
    expect(component.page).toBe(2);expect(component.context!.history.page).toBe(2);expect(component.blob).toBe(blob);
  });
});
