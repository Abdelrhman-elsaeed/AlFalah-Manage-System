import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { BehaviorSubject, of, throwError } from 'rxjs';
import { ReadinessPageComponent } from './readiness-page.component';
import { ReadinessApiService } from './readiness-api.service';
import { StorageApiService } from './storage-api.service';
import { StorageEvidenceApiService } from './evidence-api.service';
import { EvaluationRequirement, ReadinessSummary } from './readiness.models';

describe('Readiness page authorization and history',()=>{
  let page:ReadinessPageComponent;let api:jasmine.SpyObj<ReadinessApiService>;let query:BehaviorSubject<any>;
  const row={id:7,name:'متطلب',rowVersion:'AAAAAAAAAAE=',responsibleRole:'مدير المدرسة',importance:'Normal',isMandatory:true,policy:'AnyApprovedLink',requiredLinks:1,followUpStatus:'NotStarted',gapReasons:['NoFile']} as EvaluationRequirement;
  beforeEach(()=>{
    query=new BehaviorSubject(convertToParamMap({academicYearId:'8',templateVersion:'1',criticalOnly:'true'}));
    api=jasmine.createSpyObj('ReadinessApiService',['template','readiness','requirements','index','manuals','members','versions','initialize','configure','saveManual','manualHistory','followUpHistory','export']);
    api.template.and.returnValue(of({version:1,name:'القالب',rounding:'DecimalTwoPlacesAwayFromZero',sourceName:'source',sourceSha256:'hash',domains:[],standards:[],items:[],matrixReferences:[]}));
    api.readiness.and.returnValue(of({overall:{numerator:1,denominator:36,percentage:2.78,uniqueFiles:1,links:2,approvedLinks:1,gaps:35},domains:[],standards:[]} as unknown as ReadinessSummary));
    api.requirements.and.returnValue(of({items:[row],total:1,page:1,pageSize:25}));api.index.and.returnValue(of({items:[],total:0,page:1,pageSize:25}));api.manuals.and.returnValue(of([]));api.members.and.returnValue(of([]));api.versions.and.returnValue(of([{version:1,name:'القالب',sha256:'hash'}]));
    TestBed.configureTestingModule({imports:[TranslateModule.forRoot()],providers:[{provide:ReadinessApiService,useValue:api},{provide:StorageApiService,useValue:{contextInfo:()=>of({canManage:true,academicYearId:8})}},{provide:StorageEvidenceApiService,useValue:{years:()=>of([{id:8,nameAr:'السنة'}])}},{provide:ActivatedRoute,useValue:{snapshot:{data:{mode:'tracker'}},paramMap:of(convertToParamMap({})),queryParamMap:query}},{provide:Router,useValue:{navigate:jasmine.createSpy('navigate')}}]});
    page=TestBed.runInInjectionContext(()=>new ReadinessPageComponent());page.ngOnInit();
  });
  afterEach(()=>page.ngOnDestroy());
  it('shows the server numerator and denominator without recomputing after a display filter',()=>{expect(page.filter.criticalOnly).toBeTrue();expect(page.summary?.overall.denominator).toBe(36);expect(page.summary?.overall.percentage).toBe(2.78);expect(api.requirements.calls.first().args[0].trackerOnly).toBeTrue();});
  it('clears school judgments rows files and history after access is revoked',()=>{
    page.edit(row);page.historyOpen=true;api.readiness.and.returnValue(throwError(()=>({status:403,error:{message:'تم سحب التفويض'}})));page.reload();expect(page.summary).toBeUndefined();expect(page.rows).toEqual([]);expect(page.manuals).toEqual([]);expect(page.files).toEqual([]);expect(page.editOpen).toBeFalse();expect(page.historyOpen).toBeFalse();expect(page.canManage).toBeFalse();
  });
  it('sends the existing rowversion and a date without timezone shifting when assigning follow-up',()=>{
    api.configure.and.returnValue(of(row));page.edit(row);page.reason='تحديث التكليف';page.dueDate=new Date(2026,9,20,12);page.saveFollowUp();expect(api.configure.calls.mostRecent().args[1]).toEqual(jasmine.objectContaining({rowVersion:'AAAAAAAAAAE=',reason:'تحديث التكليف',dueDate:'2026-10-20',isMandatory:true}));
  });
  it('refreshes after a concurrency conflict and preserves the explanation',()=>{
    api.configure.and.returnValue(throwError(()=>({status:409,error:{message:'حدّث البيانات ثم أعد المحاولة'}})));page.edit(row);page.reason='تكليف';page.saveFollowUp();expect(page.editOpen).toBeFalse();expect(page.error).toBe('حدّث البيانات ثم أعد المحاولة');expect(api.readiness.calls.count()).toBe(2);
  });
  it('does not turn an empty calculation into 100 percent',()=>{api.readiness.and.returnValue(of({overall:{percentage:null,numerator:0,denominator:0}} as ReadinessSummary));page.reload();expect(page.summary?.overall.percentage).toBeNull();});
});
