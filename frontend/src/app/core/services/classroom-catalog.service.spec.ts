import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ClassroomCatalogService, ClassroomOption } from './classroom-catalog.service';

describe('ClassroomCatalogService paging', () => {
  let api:ClassroomCatalogService; let http:HttpTestingController;
  const context={stage:'Primary',isManager:false,isMember:true,years:[]};
  beforeEach(()=>{TestBed.configureTestingModule({providers:[provideHttpClient(),provideHttpClientTesting()]});api=TestBed.inject(ClassroomCatalogService);http=TestBed.inject(HttpTestingController);});
  afterEach(()=>http.verify());
  it('loads every page and retains options selected after the first hundred',()=>{
    let ids:number[]=[];
    api.all(3).subscribe(r=>ids=r.items.map(x=>x.id));
    http.expectOne(r=>r.url.endsWith('/classroom-catalog')&&r.params.get('page')==='1').flush({isSuccess:true,data:{context,items:Array.from({length:100},(_,i)=>({id:i+1} as ClassroomOption)),hasMore:true}});
    http.expectOne(r=>r.params.get('page')==='2'&&r.params.get('academicYearId')==='3').flush({isSuccess:true,data:{context,items:[{id:101}],hasMore:false}});
    expect(ids.length).toBe(101); expect(ids[100]).toBe(101);
  });
  it('fails the whole lookup when a later page fails rather than returning a partial catalogue',()=>{
    let success=false,error=false;api.all(3).subscribe({next:()=>success=true,error:()=>error=true});
    http.expectOne(r=>r.params.get('page')==='1').flush({isSuccess:true,data:{context,items:[{id:1}],hasMore:true}});
    http.expectOne(r=>r.params.get('page')==='2').flush({}, {status:503,statusText:'Unavailable'});
    expect(success).toBeFalse(); expect(error).toBeTrue();
  });
});
