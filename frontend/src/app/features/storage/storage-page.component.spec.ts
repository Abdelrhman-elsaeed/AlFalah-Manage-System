import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { StorageApiService } from './storage-api.service';
import { StoragePageComponent } from './storage-page.component';

describe('Storage page safety', () => {
  let page: StoragePageComponent;
  let api: jasmine.SpyObj<StorageApiService>;
  beforeEach(() => {
    api = jasmine.createSpyObj('StorageApiService', ['contextInfo', 'folders', 'files', 'upload', 'details', 'content', 'reconcile']);
    api.contextInfo.and.returnValue(of({ schoolId: 1, schoolName: 'مدرسة', canManage: true, isTeacher: true, connectionState: 'Connected', rootFolderId: 7 }));
    api.folders.and.returnValue(of({ items: [], total: 0, page: 1, pageSize: 25 }));
    api.files.and.returnValue(of({ items: [], total: 0, page: 1, pageSize: 25 }));
    TestBed.configureTestingModule({ imports: [TranslateModule.forRoot()], providers: [
      { provide: StorageApiService, useValue: api },
      { provide: ActivatedRoute, useValue: { snapshot: { data: { own: true }, queryParamMap: convertToParamMap({}) } } },
      { provide: Router, useValue: { url: '/instructor/my-files', navigate: jasmine.createSpy('navigate') } }
    ] });
    page = TestBed.runInInjectionContext(() => new StoragePageComponent());
    page.ngOnInit(); sessionStorage.removeItem('alfalah-storage-upload');
  });
  afterEach(() => { page.ngOnDestroy(); sessionStorage.removeItem('alfalah-storage-upload'); });
  it('retains the upload key after network failure and resets it for different content selection', () => {
    const file = new File(['%PDF-1.7'], 'file.pdf', { lastModified: 123 });
    page.queue(file); const key = page.uploadKey;
    api.upload.and.returnValue(throwError(() => ({ status: 0 })));
    page.upload(); page.queue(file); expect(page.uploadKey).toBe(key);
    page.queue(new File(['different'], 'other.pdf', { lastModified: 124 })); expect(page.uploadKey).not.toBe(key);
  });
  it('requires completed server status before displaying upload success', () => {
    page.queue(new File(['%PDF-1.7'], 'file.pdf'));
    api.reconcile.and.returnValue(of({ operationId: 2, status: 'NeedsAttention', displayName: 'file.pdf', size: 8, mimeType: 'application/pdf', uploadedAt: '' }));
    page.operation = { operationId: 2, status: 'Pending', displayName: 'file.pdf', size: 8, mimeType: 'application/pdf', uploadedAt: '' };
    page.reconcile(); expect(page.selectedFile).toBeDefined(); expect(page.operation?.status).toBe('NeedsAttention');
  });
  it('clears file data and preview access after a revoked authorization response', () => {
    api.files.and.returnValue(throwError(() => ({ status: 403, error: { message: 'Access revoked' } })));
    page.reload(); expect(page.files).toEqual([]); expect(page.context).toBeUndefined(); expect(page.previewUrl).toBeUndefined();
  });
  it('does not preview unsupported formats or files above the preview memory bound', () => {
    page.details = { file: { storedFileId: 1, folderId: 7, displayName: 'office.docx', size: 100, mimeType: 'application/msword', uploadedAt: '', state: 'Managed', isProtected: false, rowVersion: '' }, versions: [] };
    expect(page.previewable).toBeFalse();
    page.details.file.mimeType = 'application/pdf'; page.details.file.size = 21 * 1024 * 1024; expect(page.previewable).toBeFalse();
  });
});
