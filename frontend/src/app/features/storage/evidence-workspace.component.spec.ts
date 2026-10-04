import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { HttpEventType } from '@angular/common/http';
import { EvidenceWorkspaceComponent } from './evidence-workspace.component';
import { StorageEvidenceApiService } from './evidence-api.service';
import { StorageApiService } from './storage-api.service';
import { EvidenceLink, FileChange } from './evidence.models';

describe('Evidence workflow safety', () => {
  let page: EvidenceWorkspaceComponent;
  let api: jasmine.SpyObj<StorageEvidenceApiService>;
  let library: jasmine.SpyObj<StorageApiService>;
  const link: EvidenceLink = { id: 9, storedFileId: 31, requirementId: 2, academicYearId: 1, versionId: 4, fileName:'شاهد.pdf', requirementName:'خطة', teacherName:'معلم', status:'PendingReview',availability:'Available',rowVersion:'AAAAAAAAAAE=',decisions:[] };
  const change: FileChange = {id:3,storedFileId:31,originalVersionId:4,kind:'Replace',status:'Pending',reason:'نسخة جديدة',requestedByUserId:'teacher',rowVersion:'AAAAAAAAAAI=',decisions:[]};
  beforeEach(() => {
    api = jasmine.createSpyObj('StorageEvidenceApiService',['catalog','years','teachers','links','changes','counts','queue','changeQueue','review','submit','link','requestChange','uploadVersion','decideChange']);
    api.catalog.and.returnValue(of([])); api.years.and.returnValue(of([{id:1,nameAr:'السنة'}])); api.teachers.and.returnValue(of([]));
    api.links.and.returnValue(of([link])); api.changes.and.returnValue(of([])); api.counts.and.returnValue(of({files:1,links:2,approvedLinks:0,fulfilledRequirements:0,requirements:11}));
    api.queue.and.returnValue(of({items:[link],total:1,page:1,pageSize:25})); api.changeQueue.and.returnValue(of({items:[],total:0,page:1,pageSize:25}));
    library=jasmine.createSpyObj('StorageApiService',['reconcile']);
    TestBed.configureTestingModule({providers:[{provide:StorageEvidenceApiService,useValue:api},{provide:StorageApiService,useValue:library}]});
    page=TestBed.runInInjectionContext(()=>new EvidenceWorkspaceComponent());page.year=1;page.own=true;page.canManage=true;
    page.file={storedFileId:31,folderId:1,displayName:'شاهد.pdf',size:10,mimeType:'application/pdf',uploadedAt:'',state:'Managed',isProtected:true,rowVersion:'AAAAAAAAAAI='};
  });
  afterEach(()=>{page.ngOnDestroy();sessionStorage.removeItem('alfalah-storage-version');});
  it('does not send rejection without a reason and passes the selected link token',()=>{
    page.review(link,false);expect(api.review).not.toHaveBeenCalled();
    api.review.and.returnValue(of({...link,status:'Rejected'}));page.note='توضيح مطلوب';page.review(link,false);
    expect(api.review).toHaveBeenCalledOnceWith(link,false,'توضيح مطلوب');
  });
  it('clears links, history and counts on revoked access',()=>{
    page.ngOnChanges();expect(page.links.length).toBe(1);
    spyOn(page.denied,'emit');api.links.and.returnValue(throwError(()=>({status:403,error:{message:'تم سحب التفويض'}})));page.reload();
    expect(page.links).toEqual([]);expect(page.counts).toBeUndefined();expect(page.denied.emit).toHaveBeenCalled();
  });
  it('shows file and link and requirement counts independently',()=>{
    page.ngOnChanges();expect(page.counts?.files).toBe(1);expect(page.counts?.links).toBe(2);expect(page.counts?.fulfilledRequirements).toBe(0);
  });
  it('preserves the candidate idempotency key after a lost response and until Completed',()=>{
    const file=new File(['%PDF-1.7'],'شاهد.pdf',{lastModified:123});const input={target:{files:[file],value:''}} as unknown as Event;
    page.choose(input,change);const key=page.uploadKey;
    api.uploadVersion.and.returnValue(throwError(()=>({status:503,error:{message:'انقطع الاتصال'}})));page.upload(change);page.choose(input,change);
    expect(page.uploadKey).toBe(key);expect(page.candidate).toBeDefined();
    api.uploadVersion.and.returnValue(of({type:HttpEventType.Response,body:{isSuccess:true,data:{operationId:7,status:'NeedsAttention',displayName:'شاهد.pdf',size:8,mimeType:'application/pdf',uploadedAt:''}}} as any));page.upload(change);
    expect(page.operation).toBe(7);expect(page.candidate).toBeDefined();expect(sessionStorage.getItem('alfalah-storage-version')).not.toBeNull();
  });
  it('requires a change reason and sends a protected file through review',()=>{
    page.requestChange();expect(api.requestChange).not.toHaveBeenCalled();
    api.requestChange.and.returnValue(of(change));page.reason='نسخة محدثة';page.requestChange();
    expect(api.requestChange).toHaveBeenCalledWith(31,'Replace','نسخة محدثة','AAAAAAAAAAI=',false);
  });
  it('uses server pages and selected year in the manager queue',()=>{
    page.file=undefined;page.own=false;page.page=3;page.standard='2.1';page.teacher=5;page.status=5;page.reload();
    expect(api.queue).toHaveBeenCalledWith(1,3,undefined,5,'2.1',5);expect(api.changeQueue).toHaveBeenCalledWith(1,1,'Pending');
  });
});
