import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { StorageEvidenceApiService } from './evidence-api.service';
import { EvidenceLink, Requirement } from './evidence.models';

describe('Storage evidence API contracts',()=>{
  let api:StorageEvidenceApiService;let http:HttpTestingController;
  beforeEach(()=>{TestBed.configureTestingModule({providers:[provideHttpClient(),provideHttpClientTesting()]});api=TestBed.inject(StorageEvidenceApiService);http=TestBed.inject(HttpTestingController);});
  afterEach(()=>http.verify());
  it('configures catalog policy through PATCH with the catalog rowversion',()=>{
    const body={fulfillmentPolicy:2,minimumApprovedLinks:2,rowVersion:'AAAAAAAAAAE='};
    api.configure({id:2} as Requirement,body).subscribe();const req=http.expectOne(r=>r.url.endsWith('/storage/requirements/2'));
    expect(req.request.method).toBe('PATCH');expect(req.request.body).toEqual(body);req.flush({isSuccess:true,data:{}});
  });
  it('reviews one internal link with its rowversion and decision, never a school body',()=>{
    const link={id:9,rowVersion:'AAAAAAAAAAE='} as EvidenceLink;api.review(link,false,'سبب').subscribe();
    const req=http.expectOne(r=>r.url.endsWith('/storage/links/9/review'));expect(req.request.body).toEqual({decision:4,note:'سبب',rowVersion:'AAAAAAAAAAE='});req.flush({isSuccess:true,data:{}});
  });
  it('links an existing asset to a year requirement without sending bytes',()=>{
    api.link(31,2,4).subscribe();const req=http.expectOne(r=>r.url.endsWith('/storage/files/31/links'));
    expect(req.request.body).toEqual({requirementId:2,academicYearId:4});req.flush({isSuccess:true,data:{}});
  });
  it('reads historical version bytes through authenticated API only',()=>{
    api.versionContent(31,4).subscribe();const req=http.expectOne(r=>r.url.endsWith('/storage/files/31/versions/4/content'));
    expect(req.request.responseType).toBe('blob');req.flush(new Blob(['%PDF-1.7']));
  });
});
