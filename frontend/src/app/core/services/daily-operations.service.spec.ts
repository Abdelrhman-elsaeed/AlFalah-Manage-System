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
});
