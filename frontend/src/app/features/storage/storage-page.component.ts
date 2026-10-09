import { CommonModule } from '@angular/common';
import { HttpEventType } from '@angular/common/http';
import { Component, ElementRef, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import type { PDFDocumentLoadingTask, PDFDocumentProxy } from 'pdfjs-dist';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { PaginatorModule } from 'primeng/paginator';
import { TreeNode } from 'primeng/api';
import { Subject, Subscription, forkJoin, takeUntil } from 'rxjs';
import { StorageEvidenceApiService } from './evidence-api.service';
import { EvidenceLink, Requirement } from './evidence.models';
import { StorageApiService } from './storage-api.service';
import { StorageContext, StorageDetails, StorageFile, StorageFolder, StorageUpload, StorageDiscovery } from './storage.models';
import { EvidenceWorkspaceComponent } from './evidence-workspace.component';

@Component({
  selector: 'app-storage-page', standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, TranslateModule, ButtonModule, DialogModule, PaginatorModule, EvidenceWorkspaceComponent],
  templateUrl: './storage-page.component.html', styleUrls: ['./storage-page.component.css']
})
export class StoragePageComponent implements OnInit, OnDestroy {
  @ViewChild('previewStage') private previewStage?: ElementRef<HTMLElement>;
  private readonly api = inject(StorageApiService);
  private readonly evidenceApi = inject(StorageEvidenceApiService);
  readonly route = inject(ActivatedRoute);
  readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  private readonly destroyed = new Subject<void>();
  private load?: Subscription;
  private pathLoad?: Subscription;
  private detailsLoad?: Subscription;
  private folderLoad?: Subscription;
  private contentLoad?: Subscription;
  private moveLoad?: Subscription;
  private objectUrl?: string;
  private pdfPageObjectUrl?: string;
  private pdfDocument?: PDFDocumentProxy;
  private pdfLoadingTask?: PDFDocumentLoadingTask;
  private previewRequestId = 0;
  private searchTimer?: ReturnType<typeof setTimeout>;
  private pendingSearchText: string | null = null;
  private previewResizeObserver?: ResizeObserver;
  private previewNaturalWidth = 0;
  private previewNaturalHeight = 0;
  private readonly folderPages = new Map<number, number>();
  private readonly folderPaths = new Map<number, StorageFolder[]>();
  private listKey = '';
  private displayedFolderId?: number;
  private openedFileId?: number;
  readonly own = this.route.snapshot.data['own'] === true;
  context?: StorageContext;
  evaluationYear?: number;
  initialRequirement?: number;
  teacherRequirements: Requirement[] = [];
  teacherRequirementsLoading = false;
  teacherRequirementsError = '';
  teacherRequirementId?: number;
  teacherCreatedLink?: EvidenceLink;
  teacherCreatedFileId?: number;
  discovery?: StorageDiscovery;
  folders: StorageFolder[] = [];
  folderTotal = 0; folderPage = 1; folderLoading = false;
  tree: TreeNode<StorageFolder>[] = [];
  crumbs: StorageFolder[] = [];
  files: StorageFile[] = [];
  total = 0; page = 1; search = ''; searchInput = ''; global = false; sort = 'name'; descending = false; filter = 'all'; cards = false;
  readonly fileFilters = [{value:'all',label:'الكل'},{value:'pdf',label:'PDF'},{value:'image',label:'صور'},
    {value:'video',label:'فيديو'},{value:'unlinked',label:'غير مربوط'},{value:'protected',label:'محمي'}];
  activeView: 'library' | 'review' = 'library';
  busy = false; uploading = false; previewBusy = false; error = ''; notice = ''; disabled = false;
  uploadDialog = false; uploadError = '';
  selectedFile?: File;
  uploadKey = ''; progress = 0; operation?: StorageUpload;
  details?: StorageDetails;
  detailPreviewFile?: StorageFile;
  detailPreviewName = '';
  detailTab: 'info' | 'evidence' | 'modify' = 'info';
  detailEvidenceVisited = false;
  detailModifyVisited = false;
  private detailSelectedId?: number;
  rawPreviewUrl?: string;
  pdfPageUrl?: string;
  pdfPageNumber = 1;
  pdfPageCount = 0;
  previewFile?: StorageFile;
  previewError = '';
  detailOpen = false; previewOpen = false; previewMaximized = false;
  previewZoom = 100;
  previewFitWidth = 0;
  previewFitHeight = 0;
  get previewCanZoom() { return !!this.previewFile && (this.previewFile.mimeType === 'application/pdf' || !!this.previewFile.mimeType?.startsWith('image/')); }
  get previewDisplayWidth() { return Math.round(this.previewFitWidth * this.previewZoom / 100); }
  get previewDisplayHeight() { return Math.round(this.previewFitHeight * this.previewZoom / 100); }
  renameName = ''; folderName = ''; folderDialog = false; moveDialog = false;
  moveSource?: StorageFolder; moveDestination?: number;
  moveDestinationName = '';
  moveBrowsePath: StorageFolder[] = [];
  moveChoices: StorageFolder[] = [];
  moveChoicesLoading = false;
  moveChoicesError = '';
  moveChoicesPage = 1;
  moveChoicesTotal = 0;
  moveBusy = false;
  get currentFolder() { return this.crumbs.at(-1); }
  get moveBrowseFolder() { return this.moveBrowsePath.at(-1); }
  get detailFile() { return this.details?.file ?? this.detailPreviewFile; }
  get connected() { return this.context?.connectionState === 'Connected'; }
  get canReviewEvidence() { return !!(this.context?.canReviewEvidence ?? this.context?.canManage) && !this.historicalYear; }
  get historicalYear() { return !!this.evaluationYear && !!this.context?.academicYearId && this.evaluationYear !== this.context.academicYearId; }
  get previewable() { return !!this.detailFile && this.canPreview(this.detailFile); }
  canPreview(file: StorageFile) {
    return file.size <= 20 * 1024 * 1024 &&
      ['application/pdf', 'image/jpeg', 'image/png', 'image/webp', 'video/mp4'].includes(file.mimeType || '') &&
      file.state !== 'MissingFromDrive' && file.state !== 'Withdrawn';
  }
  fileExtension(file: StorageFile) { return file.displayName.split('.').pop()?.toUpperCase().slice(0, 5) || 'FILE'; }
  fileKind(file: StorageFile) {
    const ext = this.fileExtension(file);
    if (ext === 'PDF') return 'pdf';
    if (['JPG', 'JPEG', 'PNG', 'WEBP', 'HEIC'].includes(ext)) return 'image';
    if (['MP4', 'MOV'].includes(ext)) return 'video';
    if (['XLS', 'XLSX'].includes(ext)) return 'sheet';
    if (['DOC', 'DOCX'].includes(ext)) return 'document';
    return 'other';
  }
  fileIcon(file: StorageFile) {
    const kind = this.fileKind(file);
    return kind === 'image' ? 'pi pi-image' : kind === 'video' ? 'pi pi-video' : kind === 'sheet' ? 'pi pi-table' : 'pi pi-file';
  }
  ngOnInit() {
    this.route.queryParamMap.pipe(takeUntil(this.destroyed)).subscribe(() => this.applyRoute());
    this.refreshContext();
  }
  private applyRoute() {
    if (!this.connected || !this.context?.rootFolderId) return;
    const params = this.route.snapshot.queryParamMap;
    this.initialRequirement = Number(params.get('requirement')) || undefined;
    if (this.own) this.teacherRequirementId = this.initialRequirement;
    this.evaluationYear = Number(params.get('academicYearId')) || this.context.academicYearId;
    const file = Number(params.get('file')) || undefined;
    this.activeView = !this.own && (this.route.snapshot.data['view'] === 'review' || params.get('view') === 'review' || !!this.initialRequirement && !file) ? 'review' : 'library';
    this.search = params.get('search') || '';
    if (this.pendingSearchText === null || this.pendingSearchText === this.search) {
      this.searchInput = this.search;
      this.pendingSearchText = null;
    }
    this.global = params.get('global') === 'true';
    this.sort = ['name', 'size', 'date'].includes(params.get('sort') || '') ? params.get('sort')! : 'name';
    this.descending = params.get('descending') === 'true';
    this.filter = this.fileFilters.some(item => item.value === params.get('filter')) ? params.get('filter')! : 'all';
    this.page = Math.max(1, Number(params.get('page')) || 1);
    const folder = Number(params.get('folder')) || this.context.rootFolderId;
    const key = [folder, this.search, this.global, this.sort, this.descending, this.filter, this.page, this.activeView].join('|');
    if (this.activeView === 'library' && !this.historicalYear && key !== this.listKey) {
      this.listKey = key;
      if (folder !== this.currentFolder?.id) {
        const knownPath = this.folderPaths.get(folder);
        if (knownPath) { this.crumbs = [...knownPath]; this.reload(); }
        else this.navigateFolder(folder);
      }
      else this.reload();
    }
    if (file !== this.openedFileId) {
      this.openedFileId = file;
      this.detailsLoad?.unsubscribe();
      if (file && !this.historicalYear) this.loadDetails(file);
      else { this.detailOpen = false; this.details = undefined; this.deleteConfirmed = false; }
    }
  }
  refreshContext() {
    this.busy = true; this.error = ''; this.disabled = false;
    this.api.contextInfo(this.own).pipe(takeUntil(this.destroyed)).subscribe({
      next: context => {
        this.context = context; this.busy = false;
        this.disabled = context.connectionState === 'Disabled';
        if (context.connectionState === 'Connected' && context.rootFolderId != null) {
          const root: StorageFolder = { id: context.rootFolderId, displayName: this.translate.instant(this.own ? 'STORAGE.OWN_TITLE' : 'STORAGE.TITLE'), kind: '', rowVersion: '' };
          this.crumbs = [root]; this.folderPaths.clear(); this.folderPaths.set(root.id, [root]);
          this.tree = [{ key: String(root.id), label: root.displayName, data: root, expanded: true, leaf: false }];
          this.listKey = '';
          this.applyRoute();
          if (this.own && context.academicYearId) this.loadTeacherRequirements(context.academicYearId);
        }
      }, error: error => { this.busy = false; this.disabled = error.status === 404; this.fail(error); }
    });
  }
  private loadTeacherRequirements(year: number) {
    this.teacherRequirementsLoading = true; this.teacherRequirementsError = '';
    this.evidenceApi.catalog(year).pipe(takeUntil(this.destroyed)).subscribe({
      next: items => { this.teacherRequirementsLoading = false; this.teacherRequirements = items; },
      error: e => { this.teacherRequirementsLoading = false; this.teacherRequirementsError = e?.error?.message || 'تعذر تحميل المتطلبات. يمكنك رفع الملف وربطه لاحقًا من تفاصيله.'; }
    });
  }
  selectTeacherRequirement(id?: number) {
    this.teacherCreatedLink = undefined;
    this.router.navigate([], { relativeTo: this.route, queryParamsHandling: 'merge', queryParams: { requirement: id || null } });
  }
  loadChildren(node: TreeNode<StorageFolder>, more = false) {
    if (!node.data || node.loading) return;
    node.loading = true;
    const page = more ? (this.folderPages.get(node.data.id) || 1) + 1 : 1;
    this.api.folders(this.own, node.data.id, page).pipe(takeUntil(this.destroyed)).subscribe({
      next: result => {
        node.loading = false; this.folderPages.set(node.data!.id, page);
        const children = result.items.map(data => ({ key: String(data.id), label: data.displayName, data, leaf: false }));
        node.children = [...(more ? (node.children || []).filter(x => x.type !== 'more') : []), ...children];
        if (page * 25 < result.total) node.children.push({ type: 'more', label: this.translate.instant('STORAGE.MORE'), data: node.data });
        this.tree = [...this.tree];
      }, error: error => { node.loading = false; this.fail(error); }
    });
  }
  selectNode(node: TreeNode<StorageFolder>) {
    if (node.type === 'more') { const parent = this.findNode(node.data!.id); if (parent) this.loadChildren(parent, true); return; }
    if (!node.data) return;
    const path: StorageFolder[] = [];
    const walk = (nodes: TreeNode<StorageFolder>[]): boolean => nodes.some(n => {
      if (!n.data || n.type === 'more') return false;
      path.push(n.data);
      if (n === node || walk(n.children || [])) return true;
      path.pop(); return false;
    });
    if (walk(this.tree) && path.length) this.selectFolder(path.at(-1)!.id);
  }
  private findNode(id: number): TreeNode<StorageFolder> | undefined {
    const find = (nodes: TreeNode<StorageFolder>[]): TreeNode<StorageFolder> | undefined => {
      for (const n of nodes) { if (n.data?.id === id && n.type !== 'more') return n; const nested = find(n.children || []); if (nested) return nested; }
      return undefined;
    }; return find(this.tree);
  }
  breadcrumb(index: number) { const id = this.crumbs[index]?.id; if (id && id !== this.currentFolder?.id) this.selectFolder(id); }
  backFolder() { if (this.crumbs.length > 1) this.breadcrumb(this.crumbs.length - 2); }
  openFolder(folder: StorageFolder) {
    if (folder.parentFolderId !== this.currentFolder?.id || this.crumbs.some(item => item.id === folder.id)) return;
    this.folderPaths.set(folder.id, [...this.crumbs, folder]);
    this.selectFolder(folder.id);
  }
  private selectFolder(id: number) {
    this.router.navigate([], { relativeTo: this.route, queryParamsHandling: 'merge', queryParams: { folder: id, page: 1, file: null } });
  }
  openMoveDialog(folder: StorageFolder) {
    this.moveSource = folder;
    this.moveDestination = undefined;
    this.moveDestinationName = '';
    this.moveChoicesError = '';
    this.moveBrowsePath = this.crumbs.slice(0, -1);
    this.moveDialog = true;
    this.loadMoveChoices();
  }
  closeMoveDialog() { this.moveLoad?.unsubscribe(); }
  loadMoveChoices(page = 1) {
    const folder = this.moveBrowseFolder;
    if (!folder) return;
    this.moveLoad?.unsubscribe();
    if (page === 1) this.moveChoices = [];
    this.moveChoicesLoading = true;
    this.moveChoicesError = '';
    this.moveLoad = this.api.folders(this.own, folder.id, page).pipe(takeUntil(this.destroyed)).subscribe({
      next: result => {
        this.moveChoices = page === 1 ? result.items : [...this.moveChoices, ...result.items];
        this.moveChoicesTotal = result.total;
        this.moveChoicesPage = page;
        this.moveChoicesLoading = false;
      },
      error: e => {
        this.moveChoicesLoading = false;
        this.moveChoicesError = e?.error?.message || 'تعذر تحميل المجلدات. حاول مرة أخرى.';
      }
    });
  }
  browseMoveFolder(folder: StorageFolder) {
    if (folder.id === this.moveSource?.id) return;
    this.moveBrowsePath = [...this.moveBrowsePath, folder];
    this.loadMoveChoices();
  }
  browseMoveCrumb(index: number) {
    if (index === this.moveBrowsePath.length - 1) return;
    this.moveBrowsePath = this.moveBrowsePath.slice(0, index + 1);
    this.loadMoveChoices();
  }
  selectMoveDestination(folder: StorageFolder) {
    if (folder.id === this.moveSource?.id || folder.id === this.moveSource?.parentFolderId) return;
    this.moveDestination = folder.id;
    this.moveDestinationName = folder.displayName;
  }
  private navigateFolder(id: number) {
    this.pathLoad?.unsubscribe(); this.load?.unsubscribe(); this.busy = true; this.error = '';
    this.pathLoad = this.api.folderPath(this.own, id).pipe(takeUntil(this.destroyed)).subscribe({
      next: path => {
        this.crumbs = path;
        path.forEach((part, index) => this.folderPaths.set(part.id, path.slice(0, index + 1)));
        this.reload();
      },
      error: error => { this.busy = false; this.fail(error); }
    });
  }
  setView(view: 'library' | 'review') {
    this.activeView = view;
    this.router.navigate([], { relativeTo: this.route, queryParamsHandling: 'merge', queryParams: { view: view === 'review' ? 'review' : null } });
  }
  reload() {
    if (!this.currentFolder) return;
    this.load?.unsubscribe(); this.folderLoad?.unsubscribe(); this.folderLoading = false; this.folderPage = 1;
    const folderId = this.currentFolder.id;
    const { search, global, sort, descending, page, filter } = this;
    const cached = this.api.peekLibrary(this.own, folderId, search, global, sort, descending, page, filter);
    this.busy = true; this.error = '';
    if (cached) {
      this.files = cached.files.items; this.total = cached.files.total;
      this.folders = cached.folders.items; this.folderTotal = cached.folders.total;
      this.displayedFolderId = folderId;
    } else if (this.displayedFolderId !== folderId) {
      this.files = []; this.total = 0; this.folders = []; this.folderTotal = 0;
      this.displayedFolderId = folderId;
    }
    this.discovery = undefined;
    this.load = forkJoin({ files: this.api.files(this.own, folderId, search, global, sort, descending, page, filter),
      folders: this.api.folders(this.own, this.currentFolder.id) }).pipe(takeUntil(this.destroyed)).subscribe({
      next: result => { this.api.rememberLibrary(this.own, folderId, search, global, sort, descending, page, filter, result.files, result.folders);
        this.busy = false; this.files = result.files.items; this.total = result.files.total; this.folders = result.folders.items; this.folderTotal = result.folders.total; this.displayedFolderId = folderId; },
      error: error => { this.busy = false; this.fail(error); }
    });
  }
  loadMoreFolders() {
    if (!this.currentFolder || this.folderLoading || this.busy || this.folders.length >= this.folderTotal) return;
    const folderId = this.currentFolder.id;
    const page = this.folderPage + 1;
    this.folderLoading = true;
    this.folderLoad = this.api.folders(this.own, folderId, page).pipe(takeUntil(this.destroyed)).subscribe({
      next: result => {
        this.folderLoading = false;
        if (this.currentFolder?.id !== folderId) return;
        this.folders = [...this.folders, ...result.items.filter(item => !this.folders.some(existing => existing.id === item.id))];
        this.folderTotal = result.total; this.folderPage = page;
      }, error: e => { this.folderLoading = false; this.fail(e); }
    });
  }
  setFilter(filter: string) { this.router.navigate([], {relativeTo:this.route,queryParamsHandling:'merge',queryParams:{filter:filter === 'all' ? null : filter,page:1}}); }
  queueSearch(value: string) {
    this.searchInput = value;
    this.pendingSearchText = value;
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => { this.searchTimer = undefined; this.searchSubmit(); }, 300);
  }
  searchSubmit() {
    clearTimeout(this.searchTimer); this.searchTimer = undefined;
    const submittedSearch = this.searchInput;
    this.pendingSearchText = submittedSearch;
    const key = [this.currentFolder?.id, submittedSearch, this.global, this.sort, this.descending, this.filter, 1, this.activeView].join('|');
    const refreshCurrent = key === this.listKey;
    this.router.navigate([], { relativeTo: this.route, queryParamsHandling: 'merge', queryParams: {
    search: submittedSearch || null, global: this.global || null, sort: this.sort, descending: this.descending || null, page: 1
  } }).then(navigated => {
      if (navigated && refreshCurrent) this.reload();
      if (!navigated && this.pendingSearchText === submittedSearch) { this.pendingSearchText = null; this.searchInput = this.search; }
    });
  }
  discover(next = false) {
    if (!this.currentFolder) return;
    this.api.discover(this.own, this.currentFolder.id, next ? this.discovery?.nextPageToken : undefined).pipe(takeUntil(this.destroyed)).subscribe({
      next: data => { this.discovery = data; const node = this.findNode(this.currentFolder!.id); if (node) this.loadChildren(node); }, error: e => this.fail(e)
    });
  }
  paginate(first: number) { this.router.navigate([], { relativeTo: this.route, queryParamsHandling: 'merge', queryParams: { page: Math.floor(first / 25) + 1 } }); }
  choose(event: Event) { const input = event.target as HTMLInputElement; if (input.files?.[0]) this.queue(input.files[0]); input.value = ''; }
  drop(event: DragEvent) { event.preventDefault(); if (event.dataTransfer?.files[0] && this.connected && this.context?.canManage && !this.historicalYear) this.queue(event.dataTransfer.files[0]); }
  queue(file: File) {
    if (this.uploading || !this.context?.canManage || this.historicalYear) return;
    this.selectedFile = undefined;
    if (!/\.(pdf|doc|docx|ppt|pptx|xls|xlsx|jpg|jpeg|png|mp4|mov|webp|heic)$/i.test(file.name)) {
      this.uploadError = 'نوع الملف غير مدعوم. اختر PDF أو Office أو صورة أو فيديو مدعومًا.'; return;
    }
    if (!file.size || file.size > 262144000) { this.uploadError = this.translate.instant('STORAGE.SIZE_ERROR'); return; }
    this.teacherCreatedLink = undefined; this.teacherCreatedFileId = undefined;
    this.selectedFile = file; this.operation = undefined; this.progress = 0; this.uploadError = '';
    const fingerprint = `${this.context?.schoolId}:${this.currentFolder?.id}:${file.name}:${file.size}:${file.lastModified}`;
    const saved = sessionStorage.getItem('alfalah-storage-upload');
    let pending: { fingerprint: string; key: string } | undefined;
    try { pending = saved ? JSON.parse(saved) : undefined; } catch { /* stale local state */ }
    this.uploadKey = pending?.fingerprint === fingerprint ? pending.key : crypto.randomUUID();
    sessionStorage.setItem('alfalah-storage-upload', JSON.stringify({ fingerprint, key: this.uploadKey }));
  }
  cancelQueued() { if (!this.uploading) { this.selectedFile = undefined; this.uploadError = ''; this.progress = 0; } }
  closeUploadDialog() { if (!this.uploading) { this.uploadError = ''; this.progress = 0; this.selectedFile = undefined; } }
  upload() {
    if (!this.selectedFile || !this.currentFolder || this.uploading) return;
    this.uploading = true; this.uploadError = ''; this.notice = ''; this.progress = 0;
    this.api.upload(this.own, this.currentFolder.id, this.selectedFile, this.uploadKey).pipe(takeUntil(this.destroyed)).subscribe({
      next: event => {
        if (event.type === HttpEventType.UploadProgress) this.progress = Math.min(99, Math.round(100 * event.loaded / (event.total || this.selectedFile!.size)));
        if (event.type === HttpEventType.Response) {
          this.uploading = false;
          if (event.body?.data) this.uploadResult(event.body.data);
        }
      }, error: error => {
        this.uploading = false; this.progress = 0;
        this.uploadError = error?.status >= 500 ? 'تعذر حفظ الملف على الخادم. أعد المحاولة بنفس الملف.'
          : error?.error?.message || error?.message || 'تعذر رفع الملف. أعد المحاولة.';
      }
    });
  }
  private uploadResult(result: StorageUpload) {
    this.operation = result;
    if (result.status === 'Completed') {
      this.progress = 100; this.selectedFile = undefined; this.uploadDialog = false; this.uploadError = '';
      sessionStorage.removeItem('alfalah-storage-upload');
      this.teacherCreatedFileId = result.storedFileId;
      this.teacherCreatedLink = undefined;
      this.notice = this.translate.instant('STORAGE.UPLOADED'); this.reload();
      const requirement = this.teacherRequirements.find(item => item.id === this.teacherRequirementId && item.academicYearId === this.evaluationYear);
      if (this.own && requirement && result.storedFileId && this.evaluationYear) {
        this.evidenceApi.link(result.storedFileId, requirement.id, this.evaluationYear).pipe(takeUntil(this.destroyed)).subscribe({
          next: link => { this.teacherCreatedLink = link; this.notice = 'تم رفع الملف وربطه بالمتطلب كمسودة. افتح الرابط لإرساله للمراجعة.'; },
          error: e => { this.error = 'تم رفع الملف، لكن تعذر ربطه بالمتطلب: ' + (e?.error?.message || e?.message || 'خطأ غير معروف') + '. افتح الملف وأعد محاولة الربط.'; }
        });
      }
    } else this.notice = this.translate.instant('STORAGE.RECONCILIATION');
  }
  reconcile() { if (this.operation) this.api.reconcile(this.operation.operationId).pipe(takeUntil(this.destroyed)).subscribe({ next: result => this.uploadResult(result), error: e => this.fail(e) }); }
  initialize() {
    if (this.busy) return;
    this.busy = true;
    this.api.activateLibrary().pipe(takeUntil(this.destroyed)).subscribe({ next: () => this.refreshContext(), error: e => { this.busy = false; this.fail(e); } });
  }
  createFolder() {
    if (!this.currentFolder) return;
    this.api.createFolder(this.currentFolder.id, this.folderName).pipe(takeUntil(this.destroyed)).subscribe({
      next: () => { this.folderDialog = false; this.folderName = ''; this.loadChildren(this.findNode(this.currentFolder!.id)!); this.reload(); }, error: e => this.fail(e)
    });
  }
  moveFolder() {
    if (!this.moveSource || !this.moveDestination || this.moveBusy) return;
    this.moveBusy = true;
    this.moveChoicesError = '';
    this.api.moveFolder(this.moveSource, this.moveDestination).pipe(takeUntil(this.destroyed)).subscribe({
      next: () => {
        this.moveBusy = false;
        this.moveDialog = false;
        this.api.invalidateContext();
        this.refreshContext();
      },
      error: e => {
        this.moveBusy = false;
        this.moveChoicesError = e?.error?.message || 'تعذر نقل المجلد. تحقق من الوجهة وحاول مرة أخرى.';
      }
    });
  }
  openDetails(item: number | EvidenceLink) {
    const link = typeof item === 'number' ? undefined : item;
    const id = link?.storedFileId ?? item as number;
    if (this.detailSelectedId !== id) {
      this.details = undefined;
      this.detailEvidenceVisited = false;
      this.detailModifyVisited = false;
    }
    this.detailSelectedId = id;
    this.detailPreviewFile = this.files.find(file => file.storedFileId === id);
    this.detailPreviewName = link?.fileName || this.detailPreviewFile?.displayName || '';
    this.selectDetailTab(link || this.initialRequirement ? 'evidence' : 'info');
    this.detailOpen = true;
    this.router.navigate([], { relativeTo: this.route, queryParamsHandling: 'merge', queryParams: {
      file: id, link: link?.id ?? null, requirement: link?.requirementId ?? this.initialRequirement ?? null,
      folder: this.activeView === 'review' ? null : this.currentFolder?.id
    } });
  }
  private loadDetails(id: number) {
    const requestedFolder = Number(this.route.snapshot.queryParamMap.get('folder')) || undefined;
    if (this.detailSelectedId !== id) {
      this.details = undefined;
      this.detailPreviewName = '';
      this.detailEvidenceVisited = false;
      this.detailModifyVisited = false;
      this.selectDetailTab(this.initialRequirement || this.route.snapshot.queryParamMap.get('link') ? 'evidence' : 'info');
    }
    this.detailSelectedId = id;
    this.detailPreviewFile = this.files.find(file => file.storedFileId === id) ??
      (this.detailPreviewFile?.storedFileId === id ? this.detailPreviewFile : undefined);
    this.detailPreviewName = this.detailPreviewFile?.displayName || this.detailPreviewName;
    this.details = this.api.peekDetails(id, this.evaluationYear, requestedFolder, this.own) ??
      (this.details?.file.storedFileId === id ? this.details : undefined);
    this.detailOpen = true;
    this.detailsLoad = this.api.details(id, this.evaluationYear, requestedFolder, this.own)
      .pipe(takeUntil(this.destroyed)).subscribe({ next: data => {
      const linkId = Number(this.route.snapshot.queryParamMap.get('link')) || undefined;
      const accept = () => {
        this.details = data; this.renameName = data.file.displayName;
        if (this.route.snapshot.queryParamMap.has('file') && Number(this.route.snapshot.queryParamMap.get('folder')) !== data.file.folderId)
          this.router.navigate([], { relativeTo: this.route, replaceUrl: true, queryParamsHandling: 'merge', queryParams: { folder: data.file.folderId } });
      };
      if (linkId) this.evidenceApi.links(id, this.own).pipe(takeUntil(this.destroyed)).subscribe({
        next: links => {
          if (!links.some(link => link.id === linkId && link.storedFileId === id &&
            link.academicYearId === this.evaluationYear &&
            (!this.initialRequirement || link.requirementId === this.initialRequirement))) {
            this.details = undefined; this.detailOpen = false; this.error = 'رابط الشاهد لا يخص هذا الملف أو المتطلب في السنة المحددة.';
          } else accept();
        }, error: e => { this.details = undefined; this.detailOpen = false; this.fail(e); }
      });
      else accept();
    }, error: e => { this.detailOpen = false; this.fail(e); } });
  }
  closeDetails() {
    this.deleteConfirmed = false;
    this.detailSelectedId = undefined;
    this.detailPreviewFile = undefined; this.detailPreviewName = '';
    this.detailTab = 'info'; this.detailEvidenceVisited = false; this.detailModifyVisited = false;
    if (!this.route.snapshot.queryParamMap.has('file')) return;
    const returnTo = this.route.snapshot.queryParamMap.get('returnTo');
    if (returnTo && /^\/school-manager\/storage\/(readiness|gaps|tracker|reports|standards|digital-index|manual)(\/|\?|$)/.test(returnTo)) {
      this.router.navigateByUrl(returnTo, {replaceUrl:true});
      return;
    }
    this.router.navigate([], { relativeTo: this.route, replaceUrl: true, queryParamsHandling: 'merge',
      queryParams: this.own || this.canReviewEvidence ? { file: null } : { file: null, link: null, requirement: null, view: null } });
  }
  selectDetailTab(tab: 'info' | 'evidence' | 'modify') {
    this.detailTab = tab;
    if (tab === 'evidence') this.detailEvidenceVisited = true;
    if (tab === 'modify') this.detailModifyVisited = true;
  }
  rename() { if (this.details) this.api.rename(this.details.file, this.renameName).pipe(takeUntil(this.destroyed)).subscribe({ next: () => { this.api.invalidateDetails(); this.detailOpen = false; this.reload(); }, error: e => this.fail(e) }); }
  deleteConfirmed = false;
  delete() { if (this.details && this.deleteConfirmed) this.api.delete(this.details.file).pipe(takeUntil(this.destroyed)).subscribe({ next: () => { this.api.invalidateDetails(); this.detailOpen = false; this.deleteConfirmed = false; this.reload(); }, error: e => this.fail(e) }); }
  preview(file?: StorageFile) {
    const selected = file ?? this.detailFile;
    if (!selected || !this.canPreview(selected)) return;
    this.closePreview();
    this.previewFile = selected;
    this.previewOpen = true;
    this.previewBusy = true;
    const requestId = this.previewRequestId;
    this.contentLoad = this.api.content(selected.storedFileId, true).pipe(takeUntil(this.destroyed)).subscribe({
      next: blob => {
        if (selected.mimeType === 'application/pdf') {
          void this.loadPdfPreview(blob, requestId);
          return;
        }
        this.objectUrl = URL.createObjectURL(blob);
        this.rawPreviewUrl = this.objectUrl;
      },
      error: e => {
        this.previewBusy = false;
        this.previewError = e?.error?.message || 'تعذر تحميل المعاينة. يمكنك تنزيل الملف وفتحه على جهازك.';
      }
    });
  }
  private async loadPdfPreview(blob: Blob, requestId: number) {
    try {
      const { GlobalWorkerOptions, getDocument } = await import('pdfjs-dist');
      if (requestId !== this.previewRequestId) return;
      GlobalWorkerOptions.workerSrc = new URL('assets/pdfjs/pdf.worker.min.mjs', document.baseURI).toString();
      const task = getDocument({ data: new Uint8Array(await blob.arrayBuffer()) });
      this.pdfLoadingTask = task;
      const pdf = await task.promise;
      if (requestId !== this.previewRequestId) { await pdf.destroy(); return; }
      this.pdfDocument = pdf;
      this.pdfPageCount = pdf.numPages;
      await this.renderPdfPage(1, requestId);
    } catch {
      if (requestId !== this.previewRequestId) return;
      this.previewBusy = false;
      this.previewError = 'تعذر قراءة صفحات PDF. يمكنك تنزيل الملف وفتحه على جهازك.';
    }
  }
  async changePdfPage(delta: number) {
    const next = this.pdfPageNumber + delta;
    if (!this.pdfDocument || this.previewBusy || next < 1 || next > this.pdfPageCount) return;
    await this.renderPdfPage(next, this.previewRequestId);
  }
  togglePreviewSize() {
    this.previewMaximized = !this.previewMaximized;
    requestAnimationFrame(() => this.updatePreviewFit());
  }
  changePreviewZoom(delta: number) {
    this.previewZoom = Math.max(25, Math.min(300, this.previewZoom + delta));
  }
  fitPreview() { this.previewZoom = 100; }
  previewImageLoaded(event: Event) {
    const image = event.target as HTMLImageElement;
    this.previewNaturalWidth = image.naturalWidth;
    this.previewNaturalHeight = image.naturalHeight;
    const stage = this.previewStage?.nativeElement;
    if (stage && !this.previewResizeObserver) {
      this.previewResizeObserver = new ResizeObserver(() => this.updatePreviewFit());
      this.previewResizeObserver.observe(stage);
    }
    this.updatePreviewFit();
    this.previewLoaded();
  }
  private updatePreviewFit() {
    const stage = this.previewStage?.nativeElement;
    if (!stage || !this.previewNaturalWidth || !this.previewNaturalHeight) return;
    const availableWidth = Math.max(1, stage.clientWidth - 34);
    const availableHeight = Math.max(1, stage.clientHeight - 34);
    const scale = Math.min(1, availableWidth / this.previewNaturalWidth, availableHeight / this.previewNaturalHeight);
    this.previewFitWidth = Math.max(1, Math.floor(this.previewNaturalWidth * scale));
    this.previewFitHeight = Math.max(1, Math.floor(this.previewNaturalHeight * scale));
  }
  private async renderPdfPage(pageNumber: number, requestId: number) {
    const pdf = this.pdfDocument;
    if (!pdf) return;
    this.previewBusy = true;
    try {
      const page = await pdf.getPage(pageNumber);
      const natural = page.getViewport({ scale: 1 });
      const displayWidth = this.previewMaximized ? window.innerWidth - 64 : Math.min(900, window.innerWidth - 90);
      const viewport = page.getViewport({ scale: Math.min(this.previewMaximized ? 2 : 1.6,
        Math.max(300, displayWidth) / natural.width) });
      const pixelRatio = Math.min(window.devicePixelRatio || 1, 2,
        Math.sqrt(8_000_000 / (viewport.width * viewport.height)));
      const canvas = document.createElement('canvas');
      canvas.width = Math.ceil(viewport.width * pixelRatio);
      canvas.height = Math.ceil(viewport.height * pixelRatio);
      const context = canvas.getContext('2d');
      if (!context) throw new Error('Canvas unavailable');
      await page.render({ canvasContext: context, viewport,
        transform: [pixelRatio, 0, 0, pixelRatio, 0, 0] }).promise;
      const image = await new Promise<Blob>((resolve, reject) => canvas.toBlob(blob =>
        blob ? resolve(blob) : reject(new Error('Canvas export failed')), 'image/png'));
      if (requestId !== this.previewRequestId) return;
      if (this.pdfPageObjectUrl) URL.revokeObjectURL(this.pdfPageObjectUrl);
      this.pdfPageObjectUrl = URL.createObjectURL(image);
      this.pdfPageUrl = this.pdfPageObjectUrl;
      this.pdfPageNumber = pageNumber;
      this.previewBusy = false;
    } catch {
      if (requestId !== this.previewRequestId) return;
      this.previewBusy = false;
      this.previewError = 'تعذر عرض صفحة PDF. يمكنك تنزيل الملف وفتحه على جهازك.';
    }
  }
  previewLoaded() { this.previewBusy = false; }
  previewFailed() {
    this.previewBusy = false;
    this.previewError = 'تعذر عرض المعاينة. يمكنك تنزيل الملف وفتحه على جهازك.';
  }
  download(file: StorageFile) {
    this.api.content(file.storedFileId).pipe(takeUntil(this.destroyed)).subscribe({ next: blob => {
      const url = URL.createObjectURL(blob); const anchor = document.createElement('a'); anchor.href = url; anchor.download = file.displayName; anchor.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
    }, error: e => this.fail(e) });
  }
  downloadVersion(version: number) {
    if (!this.details) return;
    const file = this.details.file;
    this.evidenceApi.versionContent(file.storedFileId, version).pipe(takeUntil(this.destroyed)).subscribe({next: blob => {
      const url = URL.createObjectURL(blob); const anchor = document.createElement('a'); anchor.href = url; anchor.download = file.displayName; anchor.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
    }, error: e => this.fail(e)});
  }
  copy(file: StorageFile) {
    const url = new URL(this.router.url, location.origin);
    url.searchParams.set('folder', String(file.folderId));
    url.searchParams.set('file', String(file.storedFileId));
    navigator.clipboard.writeText(url.toString()).then(() => this.notice = this.translate.instant('STORAGE.LINK_COPIED')).catch(() => this.error = this.translate.instant('STORAGE.COPY_ERROR'));
  }
  copyCurrentLink() {
    const url = new URL(this.router.url, location.origin);
    navigator.clipboard.writeText(url.toString()).then(() => this.notice = this.translate.instant('STORAGE.LINK_COPIED'))
      .catch(() => this.error = this.translate.instant('STORAGE.COPY_ERROR'));
  }
  closePreview() {
    this.previewRequestId++;
    this.contentLoad?.unsubscribe(); if (this.objectUrl) URL.revokeObjectURL(this.objectUrl);
    if (this.pdfPageObjectUrl) URL.revokeObjectURL(this.pdfPageObjectUrl);
    if (this.pdfDocument) void this.pdfDocument.destroy();
    else if (this.pdfLoadingTask) void this.pdfLoadingTask.destroy();
    this.objectUrl = undefined; this.rawPreviewUrl = undefined;
    this.pdfPageObjectUrl = undefined; this.pdfPageUrl = undefined;
    this.pdfDocument = undefined; this.pdfLoadingTask = undefined;
    this.pdfPageNumber = 1; this.pdfPageCount = 0;
    this.previewResizeObserver?.disconnect(); this.previewResizeObserver = undefined;
    this.previewNaturalWidth = 0; this.previewNaturalHeight = 0;
    this.previewFitWidth = 0; this.previewFitHeight = 0; this.previewZoom = 100;
    this.previewMaximized = false;
    this.previewFile = undefined; this.previewBusy = false; this.previewError = '';
  }
  private fail(error: any) {
    this.error = error?.error?.message || error?.message || this.translate.instant('STORAGE.ERROR');
    if (error?.status === 403) { this.api.invalidateContext(); this.files = []; this.folders = []; this.tree = []; this.details = undefined; this.detailOpen = false; this.previewOpen = false; this.closePreview(); this.context = undefined; }
  }
  evidenceChanged() {
    this.api.invalidateDetails();
    if (this.details) this.loadDetails(this.details.file.storedFileId);
    if (this.activeView === 'library') this.reload();
  }
  evidenceDenied() { this.fail({status:403, message:this.translate.instant('STORAGE.ERROR')}); }
  ngOnDestroy() { clearTimeout(this.searchTimer); this.destroyed.next(); this.destroyed.complete(); this.load?.unsubscribe(); this.pathLoad?.unsubscribe(); this.detailsLoad?.unsubscribe(); this.folderLoad?.unsubscribe(); this.moveLoad?.unsubscribe(); this.closePreview(); }
}
