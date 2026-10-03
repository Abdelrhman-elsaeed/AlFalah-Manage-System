import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { StorageApiService } from './storage-api.service';

describe('Storage API transport', () => {
  let api: StorageApiService;
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(StorageApiService); http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  it('retains the caller key across retries and puts metadata before streamed file content', () => {
    const file = new File(['%PDF-1.7'], 'شاهد.pdf', { type: 'application/pdf' });
    for (let i = 0; i < 2; i++) {
      api.upload(true, 17, file, 'stable-retry-key').subscribe();
      const request = http.expectOne(r => r.url.endsWith('/storage/me/files'));
      expect(request.request.headers.get('Idempotency-Key')).toBe('stable-retry-key');
      const keys: string[] = [];
      (request.request.body as FormData).forEach((_value, key) => keys.push(key));
      expect(keys).toEqual(['parentFolderId', 'length', 'file']);
      request.flush({ isSuccess: true, data: { operationId: 1, status: 'Completed' } });
    }
  });
  it('uses server search/pagination with internal folder ids', () => {
    api.files(false, 12, 'خطة', true, 'date', true, 3).subscribe();
    const req = http.expectOne(r => r.url.endsWith('/storage/files'));
    expect(req.request.params.get('page')).toBe('3');
    expect(req.request.params.get('pageSize')).toBe('25');
    expect(req.request.params.get('folderId')).toBe('12');
    expect(req.request.params.has('DriveId')).toBeFalse();
    req.flush({ isSuccess: true, data: { items: [], total: 0, page: 3, pageSize: 25 } });
  });
  it('fetches preview through the authorized API as a blob', () => {
    api.content(33, true).subscribe();
    const req = http.expectOne(r => r.url.endsWith('/storage/files/33/content'));
    expect(req.request.responseType).toBe('blob'); expect(req.request.params.get('preview')).toBe('true');
    req.flush(new Blob(['%PDF-1.7']));
  });
});
