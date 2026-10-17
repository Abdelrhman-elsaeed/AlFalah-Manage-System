import { CommonModule } from '@angular/common';
import { Component, DestroyRef, EventEmitter, Input, OnChanges, Output, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { DialogModule } from 'primeng/dialog';
import { firstValueFrom, timer } from 'rxjs';
import { DutyDocumentCollection, DutyDocumentState, DutyDocumentsService } from '../../../core/services/duty-documents.service';
import { ActivatedRoute } from '@angular/router';
import { filenameFromContentDisposition } from '../../../core/services/visits.service';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import { DutyPdfPreviewComponent } from './duty-pdf-preview.component';
import { DutyErrorDirective } from './duty-error.directive';
import { isDutyPdf } from './duty-pdf-content';

@Component({ selector: 'app-duty-documents', standalone: true, imports: [CommonModule, TranslateModule, DialogModule, DutyPdfPreviewComponent, DutyErrorDirective],
  templateUrl: './duty-documents.component.html', styleUrls: ['./duty-documents.component.css'] })
export class DutyDocumentsComponent implements OnChanges {
  private readonly notificationRoute = inject(ActivatedRoute);
  private linkedDocumentOpened = false;
  @Input({ required: true }) assignmentId = 0; @Input() canManage = false; @Input() signedCount = 0;
  @Input() actionsOnly = false; @Output() changed=new EventEmitter<void>();
  private readonly api = inject(DutyDocumentsService); private readonly translate = inject(TranslateService);
  private readonly destroy = inject(DestroyRef);
  readonly data = signal<DutyDocumentCollection | null>(null); readonly loading = signal(true); readonly busy = signal(false);
  readonly error = signal(''); readonly message = signal(''); readonly kinds = ['Letter', 'Report'] as const;
  historyOpen = false; previewOpen = false; previewBlob: Blob | null = null; selected: DutyDocumentState | null = null;
  private objectUrl = ''; private fileName = ''; private focusOrigin: HTMLElement | null = null; private sequence = 0; private context = 0;
  private denied = false;
  constructor() {
    timer(10000, 10000).pipe(takeUntilDestroyed(this.destroy)).subscribe(() => {
      if (!this.actionsOnly && !this.denied && !this.busy() && !this.previewOpen && !this.historyOpen && document.visibilityState === 'visible') void this.load(true);
    });
    this.destroy.onDestroy(() => { this.sequence++; this.context++; this.release(); });
  }
  get dir() { return this.translate.currentLang === 'en' ? 'ltr' : 'rtl'; }
  t(key: string) { return this.translate.instant(`DUTY_DOC.${key}`); }
  ngOnChanges() { this.context++; this.denied = false; this.release(); this.previewOpen = false; this.historyOpen = false; this.selected = null; this.data.set(null); this.message.set(''); if(!this.actionsOnly)void this.load(); }
  openForAssignment(id:number,doc:DutyDocumentState,download=false) { if(this.busy())return;this.assignmentId=id;this.ngOnChanges();void this.open(doc,download); }
  async historyForAssignment(id:number) { if(this.busy())return;this.assignmentId=id;this.ngOnChanges();this.busy.set(true);try{await this.load();if(this.data())this.openHistory();}finally{this.busy.set(false);} }
  latest(kind: string) { return this.data()?.documents?.find(x => x.kind === kind && (!this.canManage || x.isCurrent !== false)); }
  async load(background = false) {
    const sequence = ++this.sequence;
    if (!background) { this.loading.set(true); this.error.set(''); }
    try {
      const r = await firstValueFrom(this.api.list(this.assignmentId)); if (sequence !== this.sequence) return;
      if (!r.isSuccess || !r.data || !Array.isArray(r.data.documents)) throw new Error(r.message || this.t('ERROR')); this.data.set(r.data);
      const linkedId = Number(this.notificationRoute.snapshot.queryParamMap.get('documentId'));
      if (linkedId > 0 && !this.linkedDocumentOpened && !this.actionsOnly) {
        const document = r.data.documents.find(x => x.documentId === linkedId);
        this.linkedDocumentOpened = true;
        if (document) void this.open(document); else this.error.set(this.t('UNAVAILABLE'));
      }
    } catch (e: any) {
      if (sequence !== this.sequence) return;
      if (e.status === 403 || e.status === 404) { this.denied = e.status === 403; this.data.set(null); this.previewOpen = false; this.historyOpen = false; this.release(); }
      if (!background || e.status === 403) this.error.set(e.status === 403 ? this.t('DENIED') : extractHttpErrorMessage(e) || this.t('ERROR'));
    } finally { if (sequence === this.sequence) this.loading.set(false); }
  }
  private release() { if (this.objectUrl) URL.revokeObjectURL(this.objectUrl); this.objectUrl = ''; this.previewBlob = null; }
  async open(doc: DutyDocumentState, download = false, archived = false) {
    if (this.busy()) return; const assignment = this.assignmentId; const context = this.context; this.focusOrigin = document.activeElement as HTMLElement;
    this.busy.set(true); this.error.set(''); this.message.set('');
    try {
      const response = await firstValueFrom(this.api.pdf(doc.documentId, archived)); if (assignment !== this.assignmentId || context !== this.context) return;
      if (!await isDutyPdf(response.body)) throw new Error(this.t('UNAVAILABLE'));
      if (assignment !== this.assignmentId || context !== this.context) return;
      const blob = response.body!;
      this.release(); this.objectUrl = URL.createObjectURL(blob);
      this.fileName = filenameFromContentDisposition(response.headers.get('Content-Disposition')) || `${doc.kind}.pdf`; this.selected = doc;
      if (download) { const url = this.objectUrl; this.download(); this.objectUrl = ''; setTimeout(() => URL.revokeObjectURL(url), 1000); this.message.set(this.t('DOWNLOADED')); }
      else { this.previewBlob = blob; this.previewOpen = true; }
      if(!this.actionsOnly)void this.load(true);
    } catch (e: any) {
      if (assignment !== this.assignmentId || context !== this.context) return;
      if (e.error instanceof Blob) { try { const body = JSON.parse(await e.error.text()); this.error.set(body.message || this.t('UNAVAILABLE')); } catch { this.error.set(this.t('UNAVAILABLE')); } }
      else this.error.set(extractHttpErrorMessage(e) || this.t('UNAVAILABLE'));
      if (e.status === 403) { this.denied = true; this.error.set(this.t('DENIED')); this.data.set(null); this.previewOpen = false; this.historyOpen = false; this.release(); }
    } finally { this.busy.set(false); }
  }
  download() { if (!this.objectUrl) return; const a = document.createElement('a'); a.href = this.objectUrl; a.download = this.fileName; document.body.append(a); a.click(); a.remove(); }
  closePreview() { this.release(); this.selected = null; this.focusOrigin?.focus(); }
  openHistory() { this.focusOrigin = document.activeElement as HTMLElement; this.historyOpen = true; }
  closeHistory() { this.focusOrigin?.focus(); }
  async retry(doc: DutyDocumentState) {
    if (!doc.canRetry || this.busy()) return; this.busy.set(true); this.error.set('');
    try { const r = await firstValueFrom(this.api.retry(doc.documentId)); if (!r.isSuccess) throw new Error(r.message); this.message.set(this.t('RETRY_QUEUED')); if(!this.actionsOnly)await this.load();this.changed.emit(); }
    catch (e) { this.error.set(extractHttpErrorMessage(e) || this.t('ERROR')); } finally { this.busy.set(false); }
  }
  async enable() {
    if(this.busy())return;this.busy.set(true);this.error.set('');
    try {const r=await firstValueFrom(this.api.enable(true));if(!r.isSuccess)throw new Error(r.message);await this.load();}
    catch(e){this.error.set(extractHttpErrorMessage(e)||this.t('ERROR'));}finally{this.busy.set(false);}
  }
}
