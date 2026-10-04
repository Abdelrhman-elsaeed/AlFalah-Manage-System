import { CommonModule } from '@angular/common';
import { HttpEventType } from '@angular/common/http';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { ProgressBarModule } from 'primeng/progressbar';
import { PaginatorModule } from 'primeng/paginator';
import { TreeModule } from 'primeng/tree';
import { TreeNode } from 'primeng/api';
import { Subject, Subscription, forkJoin, takeUntil, catchError, throwError } from 'rxjs';
import { StorageEvidenceApiService } from './evidence-api.service';
import { StorageApiService } from './storage-api.service';
import { StorageContext, StorageDetails, StorageFile, StorageFolder, StorageUpload, StorageDiscovery } from './storage.models';
import { EvidenceWorkspaceComponent } from './evidence-workspace.component';

@Component({
  selector: 'app-storage-page', standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule, ButtonModule, DialogModule, ProgressBarModule, PaginatorModule, TreeModule, EvidenceWorkspaceComponent],
  templateUrl: './storage-page.component.html', styleUrls: ['./storage-page.component.css']
})
export class StoragePageComponent implements OnInit, OnDestroy {
  private readonly api = inject(StorageApiService);
  private readonly evidenceApi = inject(StorageEvidenceApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly translate = inject(TranslateService);
  private readonly destroyed = new Subject<void>();
  private load?: Subscription;
  private contentLoad?: Subscription;
  private objectUrl?: string;
  private readonly folderPages = new Map<number, number>();
  readonly own = this.route.snapshot.data['own'] === true;
  context?: StorageContext;
  discovery?: StorageDiscovery;
  folders: StorageFolder[] = [];
  tree: TreeNode<StorageFolder>[] = [];
  crumbs: StorageFolder[] = [];
  files: StorageFile[] = [];
  total = 0; page = 1; search = ''; global = false; sort = 'name'; descending = false; cards = false;
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
  get previewable() {
    return !!this.details && this.details.file.size <= 20 * 1024 * 1024 &&
      ['application/pdf', 'image/jpeg', 'image/png', 'image/webp', 'video/mp4'].includes(this.details.file.mimeType || '');
  }
  ngOnInit() { this.refreshContext(); }
  refreshContext() {
    this.busy = true; this.error = ''; this.disabled = false;
    this.api.contextInfo(this.own).pipe(takeUntil(this.destroyed)).subscribe({
      next: context => {
        this.context = context; this.busy = false;
        if (context.connectionState === 'Connected' && context.rootFolderId != null) {
          const root: StorageFolder = { id: context.rootFolderId, displayName: this.translate.instant(this.own ? 'STORAGE.OWN_TITLE' : 'STORAGE.TITLE'), kind: '', rowVersion: '' };
          this.crumbs = [root]; this.tree = [{ key: String(root.id), label: root.displayName, data: root, expanded: true, leaf: false }];
          this.loadChildren(this.tree[0]);
          const params = this.route.snapshot.queryParamMap;
          this.search = params.get('search') || ''; this.global = params.get('global') === 'true';
          this.sort = ['name', 'size', 'date'].includes(params.get('sort') || '') ? params.get('sort')! : 'name';
          this.page = Math.max(1, Number(params.get('page')) || 1);
          const folder = Number(params.get('folder'));
          if (folder && folder !== root.id) this.crumbs.push({ ...root, id: folder, displayName: this.translate.instant('STORAGE.FOLDER') });
          this.reload();
          const file = Number(params.get('file'));
          if (file) this.openDetails(file);
        }
      }, error: error => { this.busy = false; this.disabled = error.status === 404; this.fail(error); }
    });
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
    walk(this.tree); this.crumbs = path; this.page = 1; this.reload();
  }
  private findNode(id: number): TreeNode<StorageFolder> | undefined {
    const find = (nodes: TreeNode<StorageFolder>[]): TreeNode<StorageFolder> | undefined => {
      for (const n of nodes) { if (n.data?.id === id && n.type !== 'more') return n; const nested = find(n.children || []); if (nested) return nested; }
      return undefined;
    }; return find(this.tree);
  }
  breadcrumb(index: number) { this.crumbs = this.crumbs.slice(0, index + 1); this.page = 1; this.reload(); }
  reload() {
    if (!this.currentFolder) return;
    this.load?.unsubscribe(); this.busy = true; this.error = ''; this.files = []; this.discovery = undefined;
    this.router.navigate([], { relativeTo: this.route, replaceUrl: true, queryParams: {
      folder: this.currentFolder.id, search: this.search || null, global: this.global || null, sort: this.sort, page: this.page
    }});
    this.load = forkJoin({ files: this.api.files(this.own, this.currentFolder.id, this.search, this.global, this.sort, this.descending, this.page),
      folders: this.api.folders(this.own, this.currentFolder.id) }).pipe(takeUntil(this.destroyed)).subscribe({
      next: result => { this.busy = false; this.files = result.files.items; this.total = result.files.total; this.folders = result.folders.items; },
      error: error => { this.busy = false; this.fail(error); }
    });
  }
  searchSubmit() { this.page = 1; this.reload(); }
  discover(next = false) {
    if (!this.currentFolder) return;
    this.api.discover(this.own, this.currentFolder.id, next ? this.discovery?.nextPageToken : undefined).pipe(takeUntil(this.destroyed)).subscribe({
      next: data => { this.discovery = data; const node = this.findNode(this.currentFolder!.id); if (node) this.loadChildren(node); }, error: e => this.fail(e)
    });
  }
  paginate(first: number) { this.page = Math.floor(first / 25) + 1; this.reload(); }
  choose(event: Event) { const input = event.target as HTMLInputElement; if (input.files?.[0]) this.queue(input.files[0]); input.value = ''; }
  drop(event: DragEvent) { event.preventDefault(); if (event.dataTransfer?.files[0] && this.connected) this.queue(event.dataTransfer.files[0]); }
  queue(file: File) {
    if (this.uploading) return;
    if (!file.size || file.size > 262144000) { this.error = this.translate.instant('STORAGE.SIZE_ERROR'); return; }
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
      this.notice = this.translate.instant('STORAGE.UPLOADED'); this.reload();
    } else this.notice = this.translate.instant('STORAGE.RECONCILIATION');
  }
  reconcile() { if (this.operation) this.api.reconcile(this.operation.operationId).pipe(takeUntil(this.destroyed)).subscribe({ next: result => this.uploadResult(result), error: e => this.fail(e) }); }
  initialize() { this.api.createFolder(undefined, '').pipe(takeUntil(this.destroyed)).subscribe({ next: () => this.refreshContext(), error: e => this.fail(e) }); }
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
  openDetails(id: number) {
    this.details = undefined; this.detailOpen = true;
    this.api.details(id).pipe(catchError(e => e.status === 404 ? this.evidenceApi.history(id) : throwError(() => e)),takeUntil(this.destroyed)).subscribe({ next: data => { this.details = data; this.renameName = data.file.displayName; }, error: e => { this.detailOpen = false; this.fail(e); } });
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
    const url = `${location.origin}${this.router.url.split('?')[0]}?file=${file.storedFileId}`;
    navigator.clipboard.writeText(url).then(() => this.notice = this.translate.instant('STORAGE.LINK_COPIED')).catch(() => this.error = this.translate.instant('STORAGE.COPY_ERROR'));
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
    if (this.details) this.openDetails(this.details.file.storedFileId);
    this.reload();
  }
  evidenceDenied() { this.fail({status:403, message:this.translate.instant('STORAGE.ERROR')}); }
  ngOnDestroy() { this.destroyed.next(); this.destroyed.complete(); this.load?.unsubscribe(); this.closePreview(); }
}
