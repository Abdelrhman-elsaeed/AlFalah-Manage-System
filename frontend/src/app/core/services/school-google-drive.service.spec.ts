import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { SchoolGoogleDriveService } from './school-google-drive.service';

describe('School Google Drive settings transport', () => {
  let service: SchoolGoogleDriveService; let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(SchoolGoogleDriveService); http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  it('sends bounded folder browsing parameters through the authenticated API without credentials or school IDs', () => {
    service.folders('parent', 'opaque+/page', '  خطة  ').subscribe();
    const req = http.expectOne(r => r.url.endsWith('/school-google-drive/folders'));
    expect(req.request.method).toBe('GET'); expect(req.request.params.get('parentItemId')).toBe('parent');
    expect(req.request.params.get('pageToken')).toBe('opaque+/page'); expect(req.request.params.get('search')).toBe('خطة');
    expect(req.request.params.has('schoolId')).toBeFalse(); expect(req.request.body).toBeNull(); req.flush({ isSuccess: true, data: {} });
  });
  it('omits absent parameters when opening the connected account', () => {
    service.folders().subscribe(); const req = http.expectOne(r => r.url.endsWith('/school-google-drive/folders'));
    expect(req.request.params.keys()).toEqual([]); req.flush({ isSuccess: true, data: {} });
  });
});
