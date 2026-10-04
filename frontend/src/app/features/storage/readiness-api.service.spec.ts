import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ReadinessApiService } from './readiness-api.service';
import { EvaluationFilter } from './readiness.models';

describe('Readiness API scope and export contracts',()=>{
  let api:ReadinessApiService;let http:HttpTestingController;
  const filter:EvaluationFilter={academicYearId:8,templateVersion:2,standardCode:'2.1',responsibleUserId:'member',criticalOnly:true,hideCompleted:true,trackerOnly:false,page:3,pageSize:25};
  beforeEach(()=>{TestBed.configureTestingModule({providers:[provideHttpClient(),provideHttpClientTesting()]});api=TestBed.inject(ReadinessApiService);http=TestBed.inject(HttpTestingController);});
  afterEach(()=>http.verify());
  it('uses identical scope and filters for readiness rows gaps index and export',()=>{
    api.readiness(filter).subscribe();api.requirements(filter).subscribe();api.requirements(filter,true).subscribe();api.index(filter).subscribe();api.export('pdf',filter).subscribe();
    const requests=http.match(r=>r.url.includes('/storage/'));expect(requests.length).toBe(5);
    for(const req of requests){expect(req.request.params.get('academicYearId')).toBe('8');expect(req.request.params.get('templateVersion')).toBe('2');expect(req.request.params.get('standardCode')).toBe('2.1');expect(req.request.params.get('responsibleUserId')).toBe('member');expect(req.request.params.get('criticalOnly')).toBe('true');expect(req.request.params.has('schoolId')).toBeFalse();expect(req.request.params.has('permissions')).toBeFalse();req.flush(req.request.responseType==='blob'?new Blob(['pdf']):{isSuccess:true,data:{}});}
  });
  it('preserves the original concurrency token and the explicit change reason',()=>{
    const body={rowVersion:'AAAAAAAAAAE=',reason:'تعيين مسؤول',isMandatory:true};api.configure(14,body).subscribe();const r=http.expectOne(x=>x.url.endsWith('/requirements/14/follow-up'));expect(r.request.method).toBe('PATCH');expect(r.request.body).toEqual(body);r.flush({isSuccess:true,data:{}});
  });
  it('requests authenticated binary exports',()=>{api.export('csv',filter).subscribe();const r=http.expectOne(x=>x.url.endsWith('/exports/csv'));expect(r.request.responseType).toBe('blob');r.flush(new Blob(['csv']));});
});
