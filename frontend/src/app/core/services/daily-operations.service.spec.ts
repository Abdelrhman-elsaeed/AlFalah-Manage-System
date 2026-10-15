import { HttpClient, HttpContext } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { SUPPRESS_FORBIDDEN_REDIRECT } from '../http/http-context.tokens';
import { DailyOperationsService } from './daily-operations.service';

describe('DailyOperationsService guardian management requests', () => {
  let http: jasmine.SpyObj<HttpClient>;
  let service: DailyOperationsService;

  beforeEach(() => {
    http = jasmine.createSpyObj<HttpClient>('HttpClient', ['get', 'post', 'delete']);
    http.get.and.returnValue(of({ isSuccess: true, data: [], errors: [] }));
    http.post.and.returnValue(of({ isSuccess: true, data: null, errors: [] }));
    http.delete.and.returnValue(of({ isSuccess: true, data: true, errors: [] }));

    TestBed.configureTestingModule({ providers: [{ provide: HttpClient, useValue: http }] });
    service = TestBed.inject(DailyOperationsService);
  });

  it('keeps a forbidden guardian dialog request inside the students page', () => {
    service.getGuardianOptions().subscribe();
    service.getStudentGuardians(17).subscribe();
    service.linkStudentGuardian(17, {
      guardianProfileId: 9,
      relationship: 'Father',
      isPrimary: true,
      receivesNotifications: true,
      canSubmitExcuses: true,
      canRequestGatePass: true,
      validFrom: '2026-10-02',
      validTo: null
    }).subscribe();
    service.revokeStudentGuardian(17, 3, { reason: 'correction', rowVersion: '' }).subscribe();

    const contexts = [
      http.get.calls.argsFor(0)[1]?.context,
      http.get.calls.argsFor(1)[1]?.context,
      http.post.calls.argsFor(0)[2]?.context,
      http.delete.calls.argsFor(0)[1]?.context
    ] as HttpContext[];

    expect(contexts.every(context => context.get(SUPPRESS_FORBIDDEN_REDIRECT))).toBeTrue();
  });

  it('loads every classroom page in the requested year without truncating at 100', () => {
    http.get.and.callFake((_:any,options:any)=> {
      const page=Number(options.params.get('pageNumber'));
      expect(options.params.get('academicYearId')).toBe('7');
      return of({isSuccess:true,data:{items:Array.from({length:page===1?100:5},(_,i)=>({id:(page-1)*100+i+1})),page,hasNext:page===1}}) as any;
    });
    let count=0;service.getClassrooms(7).subscribe(result=>count=result.data!.items.length);
    expect(count).toBe(105);expect(http.get).toHaveBeenCalledTimes(2);
  });

  it('uses the current school year by default and rejects incomplete multi-page results', () => {
    http.get.and.returnValues(of({isSuccess:true,data:{items:[{id:1}],page:1,hasNext:true}}) as any,
      of({isSuccess:false,message:'Second page failed'}) as any);
    let emitted=false,failed=false;
    service.getClassrooms().subscribe({next:()=>emitted=true,error:()=>failed=true});
    expect((http.get.calls.first().args[1] as any).params.get('currentYearOnly')).toBe('true');
    expect(emitted).toBeFalse();expect(failed).toBeTrue();
  });
});
