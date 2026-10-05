import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { VisitArchiveApiService } from './visit-archive-api.service';
describe('Visit archive transport',()=>{
  let api:VisitArchiveApiService;let http:HttpTestingController;
  beforeEach(()=>{TestBed.configureTestingModule({providers:[provideHttpClient(),provideHttpClientTesting()]});api=TestBed.inject(VisitArchiveApiService);http=TestBed.inject(HttpTestingController);});
  afterEach(()=>http.verify());
  it('sends server pagination and filters without a school or provider destination',()=>{
    api.list({page:3,pageSize:25,status:'RetryScheduled',teacherId:'teacher',from:'2026-10-05T00:00:00Z'}).subscribe();
    const request=http.expectOne(r=>r.url.endsWith('/storage/visits'));expect(request.request.params.get('page')).toBe('3');expect(request.request.params.get('teacherId')).toBe('teacher');expect(request.request.params.has('schoolId')).toBeFalse();request.flush({isSuccess:true,data:{items:[],total:0,page:3,pageSize:25}});
  });
  it('retries the existing approval revision and requires explicit recreation input',()=>{
    api.retry(4,2,true,'approved recreation').subscribe();const request=http.expectOne(r=>r.url.endsWith('/visits/4/archive/retry'));expect(request.request.body).toEqual({approvalRevision:2,recreateMissing:true,reason:'approved recreation'});request.flush({isSuccess:true,data:{visitId:4,revisions:[]}});
  });
  it('downloads historical bytes from the authorized API using internal version IDs',()=>{
    api.content(4,2,8).subscribe();const request=http.expectOne(r=>r.url.endsWith('/visits/4/archive/2/content'));expect(request.request.responseType).toBe('blob');expect(request.request.params.get('versionId')).toBe('8');request.flush(new Blob(['%PDF']));
  });
});
