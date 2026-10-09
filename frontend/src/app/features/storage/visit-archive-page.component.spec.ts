import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { VisitArchiveApiService } from './visit-archive-api.service';
import { VisitArchivePageComponent } from './visit-archive-page.component';
describe('Visit archive page safety',()=>{
  let page:VisitArchivePageComponent;let api:jasmine.SpyObj<VisitArchiveApiService>;
  beforeEach(()=>{
    api=jasmine.createSpyObj('archive',['list','teachers','get','retry','content','operationsStatus','peekList','forgetList']);api.list.and.returnValue(of({items:[],total:0,page:1,pageSize:25}));api.teachers.and.returnValue(of([]));api.operationsStatus.and.returnValue(of({workerEnabled:true,externalWritesEnabled:true,ready:true}));
    TestBed.configureTestingModule({imports:[TranslateModule.forRoot()],providers:[{provide:VisitArchiveApiService,useValue:api},{provide:ActivatedRoute,useValue:{queryParamMap:of(convertToParamMap({}))}},{provide:Router,useValue:{navigate:jasmine.createSpy('navigate')}}]});
    page=TestBed.runInInjectionContext(()=>new VisitArchivePageComponent());page.ngOnInit();
  });
  afterEach(()=>page.ngOnDestroy());
  it('clears reports, teacher names and recreation dialog when delegation is revoked',()=>{
    page.teachers=[{userId:'t',name:'teacher'}];page.recreateOpen=true;api.list.and.returnValue(throwError(()=>({status:403})));page.reload();expect(page.rows).toEqual([]);expect(page.teachers).toEqual([]);expect(page.disabled).toBeTrue();expect(page.recreateOpen).toBeFalse();
  });
  it('requires an explicit recreation reason and retains the original approval revision',()=>{
    const visit={visitId:4,revisions:[]} as any;const revision={approvalRevision:2,status:'MissingFromDrive'} as any;page.retry(visit,revision);expect(page.recreateOpen).toBeTrue();page.recreate();expect(api.retry).not.toHaveBeenCalled();page.reason='restore original snapshot';api.retry.and.returnValue(of(visit));page.recreate();expect(api.retry).toHaveBeenCalledWith(4,2,true,'restore original snapshot');
  });
  it('does not display completed success for a queued retry',()=>{
    api.retry.and.returnValue(of({visitId:4,revisions:[]} as any));page.retry({visitId:4} as any,{approvalRevision:1,status:'RetryScheduled'} as any);expect(page.notice).toBe('S5.QUEUED');expect(page.rows).toEqual([]);
  });
});
