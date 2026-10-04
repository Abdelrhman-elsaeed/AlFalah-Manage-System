import { CommonModule } from '@angular/common';
import { HttpEventType } from '@angular/common/http';
import { Component, EventEmitter, Input, OnChanges, OnDestroy, Output, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { PaginatorModule } from 'primeng/paginator';
import { ProgressBarModule } from 'primeng/progressbar';
import { Subject, Subscription, forkJoin, takeUntil, of } from 'rxjs';
import { ClearableSelectComponent } from '../../shared/components/clearable-select/clearable-select.component';
import { StorageEvidenceApiService } from './evidence-api.service';
import { EvidenceCounts, EvidenceLink, FileChange, Requirement } from './evidence.models';
import { StorageFile } from './storage.models';
import { StorageApiService } from './storage-api.service';

@Component({selector: 'app-storage-evidence', standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule, ButtonModule, PaginatorModule, ProgressBarModule, ClearableSelectComponent],
  templateUrl: './evidence-workspace.component.html', styleUrls: ['./evidence-workspace.component.css']})
export class EvidenceWorkspaceComponent implements OnChanges, OnDestroy {
  private readonly api = inject(StorageEvidenceApiService);
  private readonly library = inject(StorageApiService);
  private readonly destroyed = new Subject<void>();
  private load?: Subscription;
  @Input() file?: StorageFile;
  @Input() year?: number;
  @Input() own = false;
  @Input() canManage = false;
  @Output() openFile = new EventEmitter<number>();
  @Output() changed = new EventEmitter<void>();
  @Output() denied = new EventEmitter<void>();
  catalog: Requirement[] = []; links: EvidenceLink[] = []; changes: FileChange[] = [];
  counts?: EvidenceCounts; years: {id: number; nameAr: string}[] = [];
  teachers: {id:number; displayName:string}[] = [];
  changeQueue: {id:number; storedFileId:number; fileName:string; kind:string; status:string; reason:string}[] = [];
  changePage=1; changeTotal=0; changeStatus='Pending';
  selectedRequirement?: number; search = ''; teacher?: number; standard?: string; status?: number;
  editing?: Requirement; importance = 1; minimum = 1; policy = 1; mappedStandard?: string; responsibleRole?: string;
  readonly importanceOptions = [{label:'STORAGE_EVIDENCE.NORMAL',value:1},{label:'STORAGE_EVIDENCE.IMPORTANT',value:2},{label:'STORAGE_EVIDENCE.CRITICAL',value:3}];
  readonly policyOptions = [{label:'STORAGE_EVIDENCE.ANY_LINK',value:1},{label:'STORAGE_EVIDENCE.MIN_LINKS',value:2}];
  readonly roleOptions = ['Instructor','SchoolManager','Secretary','SocialWorker','StudentAffairsOfficer'].map(value => ({label:'STORAGE_EVIDENCE.ROLE_'+value,value}));
  page = 1; total = 0; busy = false; saving = false; error = ''; note = ''; reason = ''; kind = 'Replace';
  candidate?: File; uploadKey = ''; uploadChange?: number; operation?: number; progress = 0;
  readonly statusOptions = [{label: 'قيد المراجعة', value: 2}, {label: 'معاد تقديمه', value: 5}, {label: 'معتمد', value: 3}, {label: 'مرفوض', value: 4}, {label: 'مسودة', value: 1}];
  readonly standardOptions = ['1.1','1.2','1.3','1.4','1.5','2.1','2.2','3.1','3.2','4.1','4.2'].map(value => ({label: value, value}));
  ngOnChanges() { this.reload(); }
  reload(preserveError = false) {
    if (!this.year) return;
    this.load?.unsubscribe(); this.busy = true; if (!preserveError) this.error = ''; this.links = []; this.changes = [];
    const rows = this.file ? this.api.links(this.file.storedFileId, this.own) : this.api.queue(this.year, this.page, this.selectedRequirement, this.teacher, this.standard, this.status);
    // Teachers use the file panel; the school queue uses current backend delegation on every request.
    if (!this.file && this.own) { this.busy = false; return; }
    this.load = forkJoin({ catalog: this.api.catalog(this.year, this.search), years: this.api.years(), teachers: this.own ? of([]) : this.api.teachers(), rows,
      changes: this.file ? this.api.changes(this.file.storedFileId) : of([] as FileChange[]), changeQueue: !this.file ? this.api.changeQueue(this.year,this.changePage,this.changeStatus) : of({items:[],total:0}), counts: this.api.counts(this.year, this.own)
    }).pipe(takeUntil(this.destroyed)).subscribe({next: result => {
      this.busy = false; this.catalog = result.catalog; this.years = result.years; this.counts = result.counts; this.teachers = result.teachers;
      this.links = Array.isArray(result.rows) ? result.rows : result.rows.items; this.total = Array.isArray(result.rows) ? result.rows.length : result.rows.total;
      this.changes = result.changes;
      this.changeQueue = result.changeQueue.items; this.changeTotal = result.changeQueue.total;
    }, error: e => { this.busy = false; this.fail(e); }});
  }
  initialize() { this.perform(this.api.initialize(this.year!)); }
  editCatalog() {
    this.editing = this.catalog.find(r => r.id === this.selectedRequirement);
    if (!this.editing) return;
    this.importance = this.editing.importance === 'Critical' ? 3 : this.editing.importance === 'Important' ? 2 : 1;
    this.minimum = this.editing.minimumApprovedLinks; this.policy = this.editing.fulfillmentPolicy === 'MinimumApprovedLinks' ? 2 : 1;
    this.mappedStandard = this.editing.standardCode; this.responsibleRole = this.editing.responsibleRole;
  }
  saveCatalog() {
    if (!this.editing) return;
    this.perform(this.api.configure(this.editing, {domainCode:this.mappedStandard?.split('.')[0] || null,standardCode:this.mappedStandard || null,
      importance:this.importance,responsibleRole:this.responsibleRole || null,responsibleUserId:this.editing.responsibleUserId || null,
      fulfillmentPolicy:this.policy,minimumApprovedLinks:this.minimum,rowVersion:this.editing.rowVersion}));
    this.editing = undefined;
  }
  link() { if (this.file && this.selectedRequirement) this.perform(this.api.link(this.file.storedFileId, this.selectedRequirement, this.year!)); }
  submit(link: EvidenceLink) { this.perform(this.api.submit(link)); }
  review(link: EvidenceLink, approve: boolean) { if (!approve && !this.note.trim()) return; this.perform(this.api.review(link, approve, this.note)); }
  requestChange() { if (this.file && this.reason.trim()) this.perform(this.api.requestChange(this.file.storedFileId, this.kind, this.reason, this.file.rowVersion, this.kind === 'Replace' && !this.file.isProtected)); }
  decide(change: FileChange, approve: boolean) { if (!approve && !this.note.trim()) return; this.perform(this.api.decideChange(change, approve, this.note)); }
  choose(event: Event, change: FileChange) {
    const input = event.target as HTMLInputElement; const file = input.files?.[0]; input.value = '';
    if (!file || !file.size || file.size > 262144000) return;
    const identity = `${change.id}:${file.name}:${file.size}:${file.lastModified}`;
    let saved: {identity: string; key: string} | undefined;
    try { saved = JSON.parse(sessionStorage.getItem('alfalah-storage-version') || 'null'); } catch { }
    this.candidate = file; this.uploadChange = change.id; this.operation = undefined;
    this.uploadKey = saved?.identity === identity ? saved.key : crypto.randomUUID();
    sessionStorage.setItem('alfalah-storage-version', JSON.stringify({identity, key: this.uploadKey}));
  }
  upload(change: FileChange) {
    if (!this.candidate || this.saving) return; this.saving = true; this.error = '';
    this.api.uploadVersion(change, this.candidate, this.uploadKey).pipe(takeUntil(this.destroyed)).subscribe({next: e => {
      if (e.type === HttpEventType.UploadProgress) this.progress = Math.min(99, Math.round(e.loaded * 100 / (e.total || this.candidate!.size)));
      if (e.type === HttpEventType.Response) { this.saving = false; this.operation = e.body?.data?.operationId;
        if (e.body?.data?.status === 'Completed') { this.candidate = undefined; this.operation = undefined; sessionStorage.removeItem('alfalah-storage-version'); this.reload(); this.changed.emit(); }
      }
    }, error: e => { this.saving = false; this.fail(e); }});
  }
  reconcile() {
    if (!this.operation || this.saving) return;
    this.saving = true;
    this.library.reconcile(this.operation).pipe(takeUntil(this.destroyed)).subscribe({next: result => {
      this.saving = false;
      if (result.status === 'Completed') {
        this.candidate = undefined; this.operation = undefined; sessionStorage.removeItem('alfalah-storage-version');
        this.reload(); this.changed.emit();
      }
    }, error: e => { this.saving = false; this.fail(e); }});
  }
  downloadVersion(version: number) {
    if (!this.file) return;
    this.api.versionContent(this.file.storedFileId, version).pipe(takeUntil(this.destroyed)).subscribe({ next: blob => {
      const url = URL.createObjectURL(blob); const a = document.createElement('a'); a.href = url; a.download = this.file!.displayName; a.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
    }, error: e => this.fail(e) });
  }
  private perform(action: import('rxjs').Observable<unknown>) {
    if (this.saving) return; this.saving = true; this.error = '';
    action.pipe(takeUntil(this.destroyed)).subscribe({next: () => { this.saving = false; this.note = ''; this.reason = ''; this.reload(); this.changed.emit(); }, error: e => { this.saving = false; this.fail(e); if (e.status === 409) this.reload(true); }});
  }
  private fail(e: any) { this.error = e?.error?.message || e?.message || 'تعذر تحميل الشواهد.'; if (e.status === 403) { this.links = []; this.catalog = []; this.changes = []; this.counts = undefined; this.denied.emit(); } }
  ngOnDestroy() { this.destroyed.next(); this.destroyed.complete(); this.load?.unsubscribe(); }
}
