import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { ImportApiService, ImportBatch } from './import-api.service';
describe('Import transport', () => {
  let api: ImportApiService; let http: HttpTestingController;
  beforeEach(() => {TestBed.configureTestingModule({imports:[HttpClientTestingModule]});api=TestBed.inject(ImportApiService);http=TestBed.inject(HttpTestingController);});
  afterEach(()=>http.verify());
  it('sends a bounded source as multipart without a client school identifier',()=>{
    api.preview(new File(['[]'],'reference.json'),3,1,'source-v1').subscribe();
    const req=http.expectOne(r=>r.url.endsWith('/imports/preview'));const form=req.request.body as FormData;
    expect(form.get('academicYearId')).toBe('3');expect(form.get('schoolId')).toBeNull();expect(form.get('sourceVersion')).toBe('source-v1');req.flush({isSuccess:true,data:{id:1}});
  });
  it('commits precisely the reviewed digest and rowversion',()=>{
    api.decide({id:4,rowVersion:'version',digest:'reviewed'} as ImportBatch,true,'review reason').subscribe();
    const req=http.expectOne(r=>r.url.endsWith('/imports/4/commit'));expect(req.request.body).toEqual({rowVersion:'version',digest:'reviewed',reason:'review reason'});req.flush({isSuccess:true,data:{id:4}});
  });
  it('keeps reference paths out of the separate byte request',()=>{
    api.bytes({id:4} as ImportBatch,{id:5,rowVersion:'row',source:{key:'a',name:'a.pdf',referencePath:'C:\\old'}} as any,new File(['%PDF'],'a.pdf'),'checked').subscribe();
    const req=http.expectOne(r=>r.url.endsWith('/imports/4/rows/5/bytes'));const body=req.request.body as FormData;
    expect(body.get('rowVersion')).toBe('row');expect(body.get('referencePath')).toBeNull();req.flush({isSuccess:true,data:{id:5}});
  });
});
