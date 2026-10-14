import { TestBed } from '@angular/core/testing';
import { HttpHeaders, HttpResponse } from '@angular/common/http';
import { TranslateModule } from '@ngx-translate/core';
import { of, Subject, throwError } from 'rxjs';
import { DutyDocumentsComponent } from './duty-documents.component';
import { DutyDocumentState, DutyDocumentsService } from '../../../core/services/duty-documents.service';

describe('Duty retained documents', () => {
  let api: jasmine.SpyObj<DutyDocumentsService>;
  let component: DutyDocumentsComponent;
  const doc = (id = 1, kind: 'Letter' | 'Report' = 'Letter'): DutyDocumentState => ({
    documentId: id, kind, assignmentRevision: 2, signedAt: '2026-10-13T10:00:00Z',
    previousDocumentId: null, correctionReason: null, artifactState: 'Available',
    archiveState: 'Pending', canRetry: false, attempts: 0, nextAttemptAt: null, verifiedAt: null, errorCode: null,
  });
  const collection = (documents = [doc()]) => ({ isSuccess: true, data: {
    readiness: { enabled: true, processingEnabled: true, connectionState: 'Connected' }, documents,
  }, message: '', errors: [] });
  beforeEach(() => {
    api = jasmine.createSpyObj('DutyDocumentsService', ['list', 'pdf', 'retry', 'enable']);
    api.list.and.returnValue(of(collection()));
    TestBed.configureTestingModule({ imports: [DutyDocumentsComponent, TranslateModule.forRoot()], providers: [{ provide: DutyDocumentsService, useValue: api }] });
    TestBed.overrideComponent(DutyDocumentsComponent, { set: { template: '' } });
    component = TestBed.createComponent(DutyDocumentsComponent).componentInstance;
    component.assignmentId = 7;
  });
  afterEach(() => TestBed.resetTestingModule());
  it('keeps letter available while a report has not been submitted', async () => {
    await component.load(); expect(component.latest('Letter')?.documentId).toBe(1); expect(component.latest('Report')).toBeUndefined();
  });
  it('keeps the displaced teacher document in history without presenting it as the replacement report', async () => {
    component.canManage = true; api.list.and.returnValue(of(collection([{ ...doc(2, 'Report'), isCurrent: false }, { ...doc(), isCurrent: true }])));
    await component.load(); expect(component.latest('Report')).toBeUndefined(); expect(component.latest('Letter')).toBeDefined(); expect(component.data()?.documents.length).toBe(2);
  });
  it('retains correction history and selects the newest signed report', async () => {
    api.list.and.returnValue(of(collection([{ ...doc(3, 'Report'), previousDocumentId: 2, correctionReason: 'Reviewed' }, doc(2, 'Report'), doc()])));
    await component.load(); expect(component.latest('Report')?.previousDocumentId).toBe(2); expect(component.data()?.documents.length).toBe(3);
  });
  it('clears retained presentation and preview when live membership denies a background read', async () => {
    await component.load(); component.previewOpen = true; component.historyOpen = true;
    api.list.and.returnValue(throwError(() => ({ status: 403 })));
    await component.load(true); expect(component.data()).toBeNull(); expect(component.previewOpen).toBeFalse(); expect(component.historyOpen).toBeFalse();
  });
  it('ignores a delayed list from the previous assignment', async () => {
    const delayed = new Subject<any>(); api.list.and.returnValue(delayed); const old = component.load();
    component.assignmentId = 8; api.list.and.returnValue(of(collection([doc(8)]))); await component.load();
    delayed.next(collection([doc(7)])); delayed.complete(); await old; expect(component.latest('Letter')?.documentId).toBe(8);
  });
  it('prevents duplicate clicks and rejects a late PDF after changing duty', async () => {
    const delayed = new Subject<HttpResponse<Blob>>(); api.pdf.and.returnValue(delayed);
    const opening = component.open(doc()); await component.open(doc()); expect(api.pdf).toHaveBeenCalledTimes(1);
    component.assignmentId = 8; delayed.next(new HttpResponse({ body: new Blob(['%PDF-synthetic'], { type: 'application/pdf' }) })); delayed.complete();
    await opening; expect(component.previewOpen).toBeFalse(); expect(component.previewBlob).toBeNull(); expect(component.busy()).toBeFalse();
  });
  it('keeps signed success when a local artifact read fails', async () => {
    await component.load(); api.pdf.and.returnValue(throwError(() => ({ status: 409, error: new Blob([JSON.stringify({ message: 'Retained artifact unavailable' })], { type: 'application/json' }) })));
    await component.open(doc()); expect(component.error()).toBe('Retained artifact unavailable'); expect(component.latest('Letter')).toBeDefined(); expect(component.previewOpen).toBeFalse();
  });
  it('does not reopen a delayed manager PDF after access context changes on the same duty', async () => {
    const delayed = new Subject<HttpResponse<Blob>>(); api.pdf.and.returnValue(delayed); component.canManage = true;
    const opening = component.open(doc()); component.canManage = false; component.ngOnChanges();
    delayed.next(new HttpResponse({ body: new Blob(['%PDF-synthetic'], { type: 'application/pdf' }) })); delayed.complete(); await opening;
    expect(component.previewOpen).toBeFalse(); expect(component.previewBlob).toBeNull(); expect(component.selected).toBeNull();
  });
  it('does not expose manager retry through a teacher document', async () => {
    await component.retry(doc()); expect(api.retry).not.toHaveBeenCalled();
  });
  it('refreshes a retry to pending without inventing verified archive delivery', async () => {
    api.retry.and.returnValue(of({ isSuccess: true, data: true, message: '', errors: [] }));
    api.list.and.returnValue(of(collection([doc()]))); await component.retry({ ...doc(), canRetry: true, archiveState: 'NeedsAttention' });
    expect(api.retry).toHaveBeenCalledOnceWith(1); expect(component.latest('Letter')?.archiveState).toBe('Pending'); expect(component.message()).toContain('RETRY_QUEUED');
  });
});
