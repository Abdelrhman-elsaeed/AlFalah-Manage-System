import { CommonModule } from '@angular/common';
import { HttpEventType } from '@angular/common/http';
import { Component, EventEmitter, Input, OnChanges, OnDestroy, OnInit, Output, SimpleChanges, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { PaginatorModule } from 'primeng/paginator';
import { ProgressBarModule } from 'primeng/progressbar';
import { Observable, Subject, Subscription, forkJoin, takeUntil, of, skip } from 'rxjs';
import { ClearableSelectComponent } from '../../shared/components/clearable-select/clearable-select.component';
import { StorageEvidenceApiService } from './evidence-api.service';
import { EvidenceCounts, EvidenceLink, FileChange, Requirement } from './evidence.models';
import { StorageFile, StoragePage } from './storage.models';
import { StorageApiService } from './storage-api.service';

@Component({selector: 'app-storage-evidence', standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule, ButtonModule, PaginatorModule, ProgressBarModule, ClearableSelectComponent],
  templateUrl: './evidence-workspace.component.html', styleUrls: ['./evidence-workspace.component.css']})
export class EvidenceWorkspaceComponent implements OnChanges, OnInit, OnDestroy {
  private readonly api = inject(StorageEvidenceApiService);
  private readonly library = inject(StorageApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyed = new Subject<void>();
  private load?: Subscription;
  private changeLoad?: Subscription;
  @Input() file?: StorageFile;
  @Input() fileMode: 'all' | 'links' | 'changes' = 'all';
  @Input() year?: number;
  @Input() own = false;
  @Input() canManage = false;
  @Input() canReview = false;
  @Input() initialRequirement?: number;
  @Input() initialLink?: number;
  get hasInitialRequirementLink() { return this.links.some(link => link.requirementId === this.initialRequirement); }
  get hasPendingChange() { return this.changes.some(change => change.status === 'Pending'); }
  selectedReviewId?: number;
  get selectedReviewLink() { return this.links.find(link => link.id === this.selectedReviewId) || this.links[0]; }
  reviewTab: 'queue' | 'changes' | 'history' = 'queue';
  filtersOpen = false;
  private queueKey = '';
  private displayedLinkScope = '';
  private displayedChangeScope = '';
  private metadataYear?: number;
  @Output() openFile = new EventEmitter<EvidenceLink | number>();
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
  queueReady = false; changeQueueReady = false; queueTotal = 0;
  catalogLoaded = false; metadataError = ''; countsError = ''; changesError = ''; changesBusy = false;
  candidate?: File; uploadKey = ''; uploadChange?: number; operation?: number; progress = 0;
  readonly statusOptions = [{label: 'قيد المراجعة', value: 2}, {label: 'معاد تقديمه', value: 5}, {label: 'معتمد', value: 3}, {label: 'مرفوض', value: 4}, {label: 'مسودة', value: 1}];
  readonly standardOptions = ['1.1','1.2','1.3','1.4','1.5','2.1','2.2','3.1','3.2','4.1','4.2'].map(value => ({label: value, value}));
  ngOnChanges(changes: SimpleChanges) {
    if (changes['initialLink'] && !changes['year'] && !changes['file'] && !changes['fileMode'] && !changes['own'] && !changes['initialRequirement']) {
      this.selectedReviewId = this.initialLink;
      return;
    }
    if (!changes['year'] && !changes['file'] && !changes['fileMode'] && !changes['own'] && !changes['initialRequirement']) return;
    if (this.file) { if(this.initialRequirement) this.selectedRequirement=this.initialRequirement; this.reload(); }
    else { this.syncQueueUrl(); this.reload(); }
  }
  ngOnInit() { if (!this.file) this.route.queryParamMap.pipe(skip(1),takeUntil(this.destroyed)).subscribe(() => { if (this.syncQueueUrl()) this.reload(); }); }
  private syncQueueUrl(): boolean {
    const q = this.route.snapshot.queryParamMap;
    this.selectedReviewId = Number(q.get('evidenceLink')) || this.initialLink;
    const key = ['requirement','evidenceStatus','teacherId','standardCode','evidencePage','evidenceTab'].map(name => q.get(name) || '').join('|');
    if (key === this.queueKey) return false;
    this.queueKey = key;
    this.selectedRequirement = Number(q.get('requirement')) || this.initialRequirement;
    this.status = Number(q.get('evidenceStatus')) || undefined;
    this.teacher = Number(q.get('teacherId')) || undefined;
    this.standard = q.get('standardCode') || undefined;
    this.page = Math.max(1, Number(q.get('evidencePage')) || 1);
    const tab = q.get('evidenceTab');
    this.reviewTab = tab === 'changes' || tab === 'history' ? tab : 'queue';
    if (this.selectedRequirement || this.status || this.teacher || this.standard) this.filtersOpen = true;
    return true;
  }
  queueFilterChanged() {
    if (this.file) { this.reload(); return; }
    this.router.navigate([], { relativeTo:this.route, queryParamsHandling:'merge', queryParams:{
      requirement:this.selectedRequirement || null, academicYearId:this.year || null,
      evidenceStatus:this.status || null, teacherId:this.teacher || null, standardCode:this.standard || null,
      evidencePage:this.page > 1 ? this.page : null, evidenceTab:this.reviewTab === 'queue' ? null : this.reviewTab,
      evidenceLink:null
    }});
  }
  toggleFilters() {
    this.filtersOpen = !this.filtersOpen;
    if (this.filtersOpen && !this.catalogLoaded) this.loadMetadata();
  }
  selectReview(link: EvidenceLink) { this.selectedReviewId = link.id; this.router.navigate([], {relativeTo:this.route,queryParamsHandling:'merge',queryParams:{evidenceLink:link.id}}); }
  reload(preserveError = false) {
    if (!this.year) return;
    this.load?.unsubscribe(); this.load = new Subscription();
    this.busy = true; if (!preserveError) this.error = '';
    const linkScope = this.file ? `file:${this.file.storedFileId}:${this.own}`
      : JSON.stringify([this.year,this.page,this.selectedRequirement,this.teacher,this.standard,this.status,this.reviewTab === 'history']);
    const cachedQueue = !this.file && this.api.peekQueue(this.year,this.page,this.selectedRequirement,this.teacher,this.standard,this.status,this.reviewTab === 'history');
    if (cachedQueue) { this.links = cachedQueue.items; this.total = cachedQueue.total; this.displayedLinkScope = linkScope; this.queueReady = true; if (this.reviewTab === 'queue') this.queueTotal = this.total; }
    else if (this.displayedLinkScope !== linkScope) { this.links = []; this.total = 0; this.displayedLinkScope = linkScope; this.queueReady = false; }
    if (!this.file) this.counts = this.api.peekCounts(this.year,this.own) || (this.metadataYear === this.year ? this.counts : undefined);
    if (this.metadataYear !== this.year) { this.catalog = []; this.catalogLoaded = false; this.metadataYear = this.year; }
    this.metadataError = ''; this.countsError = ''; this.changesError = '';
    const loadLinks = this.file ? this.fileMode !== 'changes' : this.reviewTab !== 'changes';
    const loadFileChanges = this.file ? this.fileMode !== 'links' : this.reviewTab === 'changes' || !this.changeQueueReady;
    // Teachers use the file panel; the school queue uses current backend delegation on every request.
    if (!this.file && this.own) { this.busy = false; return; }
    if (loadLinks) {
      const rows: Observable<EvidenceLink[] | StoragePage<EvidenceLink>> = this.file ? this.api.links(this.file.storedFileId, this.own) : this.api.queue(this.year, this.page, this.selectedRequirement, this.teacher, this.standard, this.status, this.reviewTab === 'history');
      this.load.add(rows.pipe(takeUntil(this.destroyed)).subscribe({next: result => {
        this.busy = false;
        this.links = Array.isArray(result) ? result : result.items;
        this.total = Array.isArray(result) ? result.length : result.total;
        if (!this.file) { this.queueReady = true; if (this.reviewTab === 'queue') this.queueTotal = this.total; }
        this.displayedLinkScope = linkScope;
        if (!this.file && !this.links.some(link => link.id === this.selectedReviewId)) this.selectedReviewId = this.initialLink || this.links[0]?.id;
      }, error: e => { this.busy = false; this.fail(e, 'تعذر تحميل الشواهد. حاول مرة أخرى.'); }}));
    } else this.busy = false;
    if (this.file || this.filtersOpen) this.loadMetadata();
    if (!this.file) this.loadCounts();
    if (loadFileChanges) this.loadChanges();
  }
  loadMetadata() {
    if (!this.year || this.catalogLoaded) return;
    this.metadataError = '';
    this.load?.add(forkJoin({ catalog: this.api.catalog(this.year, this.search), years: this.api.years(),
      teachers: this.own ? of([]) : this.api.teachers() }).pipe(takeUntil(this.destroyed)).subscribe({
      next: result => { this.catalog = result.catalog; this.years = result.years; this.teachers = result.teachers; this.catalogLoaded = true; },
      error: e => { if (e.status === 403) this.fail(e); else this.metadataError = 'تعذر تحميل فلاتر الشواهد.'; }
    }));
  }
  loadCounts() {
    if (!this.year) return;
    this.countsError = '';
    this.load?.add(this.api.counts(this.year, this.own).pipe(takeUntil(this.destroyed)).subscribe({
      next: counts => this.counts = counts,
      error: e => { if (e.status === 403) this.fail(e); else this.countsError = 'تعذر تحميل عدادات الشواهد.'; }
    }));
  }
  loadChanges() {
    if (!this.year) return;
    this.changeLoad?.unsubscribe();
    const scope = this.file ? `file:${this.file.storedFileId}` : JSON.stringify([this.year,this.changePage,this.changeStatus]);
    const cached = !this.file && this.api.peekChangeQueue(this.year,this.changePage,this.changeStatus);
    if (cached) { this.changeQueue = cached.items; this.changeTotal = cached.total; this.displayedChangeScope = scope; this.changeQueueReady = true; }
    else if (this.displayedChangeScope !== scope) {
      this.changes = []; this.changeQueue = []; this.changeTotal = 0; this.displayedChangeScope = scope; this.changeQueueReady = false;
    }
    this.changesError = ''; this.changesBusy = true;
    const request: Observable<FileChange[] | StoragePage<(typeof this.changeQueue)[number]>> = this.file ? this.api.changes(this.file.storedFileId) : this.api.changeQueue(this.year, this.changePage, this.changeStatus);
    this.changeLoad = request.pipe(takeUntil(this.destroyed)).subscribe({
      next: result => {
        this.changesBusy = false;
        if (Array.isArray(result)) this.changes = result;
        else { this.changeQueue = result.items; this.changeTotal = result.total; this.changeQueueReady = true; }
        this.displayedChangeScope = scope;
      },
      error: e => {
        this.changesBusy = false;
        if (e.status === 403) this.fail(e); else this.changesError = this.file ? 'تعذر تحميل طلبات تغيير الملف.' : 'تعذر تحميل طلبات التغيير.';
      }
    });
    this.load?.add(this.changeLoad);
  }
  initialize() { this.catalogLoaded = false; this.perform(this.api.initialize(this.year!)); }
  editCatalog() {
    this.editing = this.catalog.find(r => r.id === this.selectedRequirement);
    if (!this.editing) return;
    this.importance = this.editing.importance === 'Critical' ? 3 : this.editing.importance === 'Important' ? 2 : 1;
    this.minimum = this.editing.minimumApprovedLinks; this.policy = this.editing.fulfillmentPolicy === 'MinimumApprovedLinks' ? 2 : 1;
    this.mappedStandard = this.editing.standardCode; this.responsibleRole = this.editing.responsibleRole;
  }
  saveCatalog() {
    if (!this.editing) return;
    this.catalogLoaded = false;
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
  private fail(e: any, fallback = 'تعذر إكمال الطلب. حاول مرة أخرى.') { this.error = e?.status >= 500 ? fallback : e?.error?.message || e?.message || fallback; if (e.status === 403) { this.load?.unsubscribe(); this.api.clearReviewSnapshots(); this.links = []; this.catalog = []; this.changes = []; this.changeQueue = []; this.counts = undefined; this.catalogLoaded = false; this.queueReady = false; this.changeQueueReady = false; this.denied.emit(); } }
  ngOnDestroy() { this.destroyed.next(); this.destroyed.complete(); this.load?.unsubscribe(); this.changeLoad?.unsubscribe(); }
}
