import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { SUPPRESS_ERROR_TOAST } from '../http/http-context.tokens';
import { VisitsV2Service } from './visits-v2.service';

describe('VisitsV2Service', () => {
  let service: VisitsV2Service;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule] });
    service = TestBed.inject(VisitsV2Service);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('queries the school-aware availability endpoint', () => {
    service.availability().subscribe(response => expect(response.data?.isEnabled).toBeTrue());
    const request = http.expectOne(`${environment.apiUrl}/api/v2/visits/availability`);
    expect(request.request.method).toBe('GET');
    request.flush({ isSuccess: true, message: '', errors: [], data: { isEnabled: true } });
  });

  it('sends archive paging and filters to the server', () => {
    service.list({ page: 3, pageSize: 15, search: 'science', evaluatorUserId: 'u-7' }).subscribe();
    const request = http.expectOne(req => req.url === `${environment.apiUrl}/api/v2/visits`);
    expect(request.request.params.get('page')).toBe('3');
    expect(request.request.params.get('pageSize')).toBe('15');
    expect(request.request.params.get('search')).toBe('science');
    expect(request.request.params.get('evaluatorUserId')).toBe('u-7');
    request.flush({ isSuccess: true, message: '', errors: [], data: null });
  });

  it('keeps PDF failures out of the global generic-error toast path', () => {
    service.exportPdf(42).subscribe();

    const request = http.expectOne(`${environment.apiUrl}/api/v2/visits/42/report/pdf`);
    expect(request.request.responseType).toBe('blob');
    expect(request.request.context.get(SUPPRESS_ERROR_TOAST)).toBeTrue();
    request.flush(new Blob(['pdf']), { status: 200, statusText: 'OK' });
  });
});
