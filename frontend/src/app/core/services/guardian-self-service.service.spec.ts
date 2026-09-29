import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { SUPPRESS_ERROR_TOAST } from '../http/http-context.tokens';
import { GuardianSelfServiceService } from './guardian-self-service.service';

describe('GuardianSelfServiceService', () => {
  let service: GuardianSelfServiceService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule] });
    service = TestBed.inject(GuardianSelfServiceService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends permit paging to the server and suppresses the generic error toast', () => {
    service.listEntryPermits(3, 10).subscribe();

    const request = http.expectOne(req => req.url === `${environment.apiUrl}/api/v1/classroom-entry-permits`);
    expect(request.request.params.get('pageNumber')).toBe('3');
    expect(request.request.params.get('pageSize')).toBe('10');
    expect(request.request.params.get('sortDirection')).toBe('desc');
    expect(request.request.context.get(SUPPRESS_ERROR_TOAST)).toBeTrue();
    request.flush({ isSuccess: true, message: '', errors: [], data: null });
  });

  it('uses the Guardian-specific summons routes', () => {
    service.listSummons(2, 20).subscribe();
    const list = http.expectOne(req => req.url === `${environment.apiUrl}/api/v1/summons/mine`);
    expect(list.request.params.get('pageNumber')).toBe('2');
    expect(list.request.params.get('pageSize')).toBe('20');
    list.flush({ isSuccess: true, message: '', errors: [], data: null });

    service.getSummon(17).subscribe();
    const detail = http.expectOne(`${environment.apiUrl}/api/v1/summons/mine/17`);
    expect(detail.request.method).toBe('GET');
    detail.flush({ isSuccess: true, message: '', errors: [], data: null });
  });

  it('sends the unread filter and page to the notification inbox', () => {
    service.listNotifications(4, 10, false).subscribe();

    const request = http.expectOne(req => req.url === `${environment.apiUrl}/api/v1/notifications`);
    expect(request.request.params.get('pageNumber')).toBe('4');
    expect(request.request.params.get('isRead')).toBe('false');
    request.flush({ isSuccess: true, message: '', errors: [], data: null });
  });

  it('uses owner-scoped notification mutation routes', () => {
    service.markRead(8).subscribe();
    const one = http.expectOne(`${environment.apiUrl}/api/v1/notifications/8/read`);
    expect(one.request.method).toBe('POST');
    one.flush({ isSuccess: true, message: '', errors: [], data: true });

    service.markAllRead().subscribe();
    const all = http.expectOne(`${environment.apiUrl}/api/v1/notifications/read-all`);
    expect(all.request.method).toBe('POST');
    all.flush({ isSuccess: true, message: '', errors: [], data: true });
  });

  it('publishes the server unread count for the shell badge', () => {
    service.unreadCount().subscribe();
    const request = http.expectOne(`${environment.apiUrl}/api/v1/notifications/unread-count`);
    request.flush({ isSuccess: true, message: '', errors: [], data: 7 });

    expect(service.unreadNotifications()).toBe(7);
  });
});
