import { CommonModule } from '@angular/common';
import { HttpEventType } from '@angular/common/http';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { ProgressBarModule } from 'primeng/progressbar';
import { PaginatorModule } from 'primeng/paginator';
import { TreeModule } from 'primeng/tree';
import { TreeNode } from 'primeng/api';
import { Subject, Subscription, forkJoin, takeUntil } from 'rxjs';
import { StorageEvidenceApiService } from './evidence-api.service';
import { EvidenceLink, Requirement } from './evidence.models';
import { StorageApiService } from './storage-api.service';
import { StorageContext, StorageDetails, StorageFile, StorageFolder, StorageUpload, StorageDiscovery } from './storage.models';
import { EvidenceWorkspaceComponent } from './evidence-workspace.component';

@Component({
  selector: 'app-storage-page', standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, TranslateModule, ButtonModule, DialogModule, ProgressBarModule, PaginatorModule, TreeModule, EvidenceWorkspaceComponent],
  templateUrl: './storage-page.component.html', styleUrls: ['./storage-page.component.css']
})
export class StoragePageComponent implements OnInit, OnDestroy {
  private readonly api = inject(StorageApiService);
  private readonly evidenceApi = inject(StorageEvidenceApiService);
  readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly translate = inject(TranslateService);
  private readonly destroyed = new Subject<void>();
  private load?: Subscription;
  private pathLoad?: Subscription;
  private detailsLoad?: Subscription;
  private folderLoad?: Subscription;
  private contentLoad?: Subscription;
  private objectUrl?: string;
  private readonly folderPages = new Map<number, number>();
  private listKey = '';
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
  total = 0; page = 1; search = ''; global = false; sort = 'name'; descending = false; filter = 'all'; cards = false;
  readonly fileFilters = [{value:'all',label:'الكل'},{value:'pdf',label:'PDF'},{value:'image',label:'صور'},
    {value:'video',label:'فيديو'},{value:'unlinked',label:'غير مربوط'},{value:'protected',label:'محمي'}];
  activeView: 'library' | 'review' = 'library';
  busy = false; uploading = false; previewBusy = false; error = ''; notice = ''; disabled = false;
  selectedFile?: File;
  uploadKey = ''; progress = 0; operation?: StorageUpload;
  details?: StorageDetails;
  previewUrl?: SafeResourceUrl;
  rawPreviewUrl?: string;
  detailOpen = false; previewOpen = false;
  renameName = ''; folderName = ''; folderDialog = false; moveDialog = false;
  moveSource?: StorageFolder; moveDestination?: number;
  get currentFolder() { return this.crumbs.at(-1); }
  get connected() { return this.context?.connectionState === 'Connected'; }
  get canReviewEvidence() { return !!(this.context?.canReviewEvidence ?? this.context?.canManage) && !this.historicalYear; }
  get historicalYear() { return !!this.evaluationYear && !!this.context?.academicYearId && this.evaluationYear !== this.context.academicYearId; }
  get previewable() {
    return !!this.details && this.details.file.size <= 20 * 1024 * 1024 &&
      ['application/pdf', 'image/jpeg', 'image/png', 'image/webp', 'video/mp4'].includes(this.details.file.mimeType || '');
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
    this.global = params.get('global') === 'true';
    this.sort = ['name', 'size', 'date'].includes(params.get('sort') || '') ? params.get('sort')! : 'name';
    this.descending = params.get('descending') === 'true';
    this.filter = this.fileFilters.some(item => item.value === params.get('filter')) ? params.get('filter')! : 'all';
    this.page = Math.max(1, Number(params.get('page')) || 1);
    const folder = Number(params.get('folder')) || this.context.rootFolderId;
    const key = [folder, this.search, this.global, this.sort, this.descending, this.filter, this.page, this.activeView].join('|');
    if (this.activeView === 'library' && !this.historicalYear && key !== this.listKey) {
      this.listKey = key;
      if (folder !== this.currentFolder?.id) this.navigateFolder(folder);
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
          this.crumbs = [root]; this.tree = [{ key: String(root.id), label: root.displayName, data: root, expanded: true, leaf: false }];
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
    if (this.busy || folder.parentFolderId !== this.currentFolder?.id || this.crumbs.some(item => item.id === folder.id)) return;
    this.selectFolder(folder.id);
  }
  private selectFolder(id: number) {
    this.router.navigate([], { relativeTo: this.route, queryParamsHandling: 'merge', queryParams: { folder: id, page: 1, file: null } });
  }
  openMoveDialog(folder: StorageFolder) { this.moveSource = folder; this.moveDialog = true; this.loadChildren(this.tree[0]); }
  private navigateFolder(id: number) {
    this.pathLoad?.unsubscribe(); this.load?.unsubscribe(); this.busy = true; this.error = '';
    this.pathLoad = this.api.folderPath(this.own, id).pipe(takeUntil(this.destroyed)).subscribe({
      next: path => { this.crumbs = path; this.reload(); },
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
    this.busy = true; this.error = ''; this.files = []; this.folders = []; this.folderTotal = 0; this.discovery = undefined;
    this.load = forkJoin({ files: this.api.files(this.own, this.currentFolder.id, this.search, this.global, this.sort, this.descending, this.page, this.filter),
      folders: this.api.folders(this.own, this.currentFolder.id) }).pipe(takeUntil(this.destroyed)).subscribe({
      next: result => { this.busy = false; this.files = result.files.items; this.total = result.files.total; this.folders = result.folders.items; this.folderTotal = result.folders.total; },
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
  searchSubmit() { const key = [this.currentFolder?.id, this.search, this.global, this.sort, this.descending, this.filter, 1, this.activeView].join('|');
    const refreshCurrent = key === this.listKey;
    this.router.navigate([], { relativeTo: this.route, queryParamsHandling: 'merge', queryParams: {
    search: this.search || null, global: this.global || null, sort: this.sort, descending: this.descending || null, page: 1
  } }).then(() => { if (refreshCurrent) this.reload(); }); }
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
    if (!file.size || file.size > 262144000) { this.error = this.translate.instant('STORAGE.SIZE_ERROR'); return; }
    this.teacherCreatedLink = undefined; this.teacherCreatedFileId = undefined;
    this.selectedFile = file; this.operation = undefined; this.progress = 0;
    const fingerprint = `${this.context?.schoolId}:${this.currentFolder?.id}:${file.name}:${file.size}:${file.lastModified}`;
    const saved = sessionStorage.getItem('alfalah-storage-upload');
    let pending: { fingerprint: string; key: string } | undefined;
    try { pending = saved ? JSON.parse(saved) : undefined; } catch { /* stale local state */ }
    this.uploadKey = pending?.fingerprint === fingerprint ? pending.key : crypto.randomUUID();
    sessionStorage.setItem('alfalah-storage-upload', JSON.stringify({ fingerprint, key: this.uploadKey }));
  }
  cancelQueued() { if (!this.uploading) this.selectedFile = undefined; }
  upload() {
    if (!this.selectedFile || !this.currentFolder || this.uploading) return;
    this.uploading = true; this.error = ''; this.notice = '';
    this.api.upload(this.own, this.currentFolder.id, this.selectedFile, this.uploadKey).pipe(takeUntil(this.destroyed)).subscribe({
      next: event => {
        if (event.type === HttpEventType.UploadProgress) this.progress = Math.min(99, Math.round(100 * event.loaded / (event.total || this.selectedFile!.size)));
        if (event.type === HttpEventType.Response) {
          this.uploading = false;
          if (event.body?.data) this.uploadResult(event.body.data);
        }
      }, error: error => { this.uploading = false; this.fail(error); }
    });
  }
  private uploadResult(result: StorageUpload) {
    this.operation = result;
    if (result.status === 'Completed') {
      this.progress = 100; this.selectedFile = undefined; sessionStorage.removeItem('alfalah-storage-upload');
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
    if (!this.moveSource || !this.moveDestination) return;
    this.api.moveFolder(this.moveSource, this.moveDestination).pipe(takeUntil(this.destroyed)).subscribe({
      next: () => { this.moveDialog = false; this.refreshContext(); }, error: e => this.fail(e)
    });
  }
  openDetails(item: number | EvidenceLink) {
    const link = typeof item === 'number' ? undefined : item;
    const id = link?.storedFileId ?? item as number;
    this.router.navigate([], { relativeTo: this.route, queryParamsHandling: 'merge', queryParams: {
      file: id, link: link?.id ?? null, requirement: link?.requirementId ?? this.initialRequirement ?? null,
      folder: this.activeView === 'review' ? null : this.currentFolder?.id
    } });
  }
  private loadDetails(id: number) {
    this.details = undefined; this.detailOpen = true;
    const requestedFolder = Number(this.route.snapshot.queryParamMap.get('folder')) || undefined;
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
    if (!this.route.snapshot.queryParamMap.has('file')) return;
    const returnTo = this.route.snapshot.queryParamMap.get('returnTo');
    if (returnTo && /^\/school-manager\/storage\/(readiness|gaps|tracker|reports|standards|digital-index|manual)(\/|\?|$)/.test(returnTo)) {
      this.router.navigateByUrl(returnTo, {replaceUrl:true});
      return;
    }
    this.router.navigate([], { relativeTo: this.route, replaceUrl: true, queryParamsHandling: 'merge',
      queryParams: this.own || this.canReviewEvidence ? { file: null } : { file: null, link: null, requirement: null, view: null } });
  }
  rename() { if (this.details) this.api.rename(this.details.file, this.renameName).pipe(takeUntil(this.destroyed)).subscribe({ next: () => { this.detailOpen = false; this.reload(); }, error: e => this.fail(e) }); }
  deleteConfirmed = false;
  delete() { if (this.details && this.deleteConfirmed) this.api.delete(this.details.file).pipe(takeUntil(this.destroyed)).subscribe({ next: () => { this.detailOpen = false; this.deleteConfirmed = false; this.reload(); }, error: e => this.fail(e) }); }
  preview() {
    if (!this.details || !this.previewable) return;
    this.closePreview(); this.previewOpen = true; this.previewBusy = true;
    this.contentLoad = this.api.content(this.details.file.storedFileId, true).pipe(takeUntil(this.destroyed)).subscribe({
      next: blob => { this.previewBusy = false; this.objectUrl = URL.createObjectURL(blob); this.rawPreviewUrl = this.objectUrl; this.previewUrl = this.sanitizer.bypassSecurityTrustResourceUrl(this.objectUrl); },
      error: e => { this.previewBusy = false; this.previewOpen = false; this.fail(e); }
    });
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
    this.contentLoad?.unsubscribe(); if (this.objectUrl) URL.revokeObjectURL(this.objectUrl);
    this.objectUrl = undefined; this.previewUrl = undefined; this.rawPreviewUrl = undefined;
  }
  private fail(error: any) {
    this.error = error?.error?.message || error?.message || this.translate.instant('STORAGE.ERROR');
    if (error?.status === 403) { this.files = []; this.folders = []; this.tree = []; this.details = undefined; this.detailOpen = false; this.previewOpen = false; this.closePreview(); this.context = undefined; }
  }
  evidenceChanged() {
    if (this.details) this.loadDetails(this.details.file.storedFileId);
    if (this.activeView === 'library') this.reload();
  }
  evidenceDenied() { this.fail({status:403, message:this.translate.instant('STORAGE.ERROR')}); }
  ngOnDestroy() { this.destroyed.next(); this.destroyed.complete(); this.load?.unsubscribe(); this.pathLoad?.unsubscribe(); this.detailsLoad?.unsubscribe(); this.folderLoad?.unsubscribe(); this.closePreview(); }
}
