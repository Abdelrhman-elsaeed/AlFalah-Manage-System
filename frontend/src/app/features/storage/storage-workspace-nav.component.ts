import { CommonModule } from '@angular/common';
import { Component, Input, OnInit, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { StorageApiService } from './storage-api.service';
import { StorageContext } from './storage.models';
import { VisitArchiveApiService } from './visit-archive-api.service';

@Component({
  selector: 'app-storage-workspace-nav', standalone: true, imports: [CommonModule, RouterLink],
  template: `<nav *ngIf="context" class="workspace-nav" aria-label="أقسام مساحة الملفات">
    <a routerLink="/school-manager/storage" [queryParams]="{academicYearId:year}" [attr.aria-current]="active('files') ? 'page' : null">الملفات</a>
    <a routerLink="/school-manager/storage" [queryParams]="{view:'review',academicYearId:year}" [attr.aria-current]="active('review') ? 'page' : null">الشواهد</a>
    <a routerLink="/school-manager/storage/readiness" [queryParams]="{academicYearId:year}" [attr.aria-current]="active('calendar') ? 'page' : null">التقويم</a>
    <a *ngIf="canViewArchive" routerLink="/school-manager/storage/visits" [attr.aria-current]="active('visits') ? 'page' : null">الزيارات</a>
    <a *ngIf="context.canManage" routerLink="/school-manager/storage/imports" [attr.aria-current]="active('imports') ? 'page' : null">الإدارة</a>
  </nav>`,
  styles: [`.workspace-nav{display:flex;flex-wrap:wrap;gap:6px;margin:0 0 20px;border-bottom:1px solid #dce9e1}
    a{padding:12px 15px;color:#496458;text-decoration:none;border-bottom:3px solid transparent;font-weight:600}
    a:hover{background:#f3f9f5}a[aria-current=page]{color:#087a51;border-color:#0bbd82}
    a:focus-visible{outline:3px solid #b9a04e;outline-offset:2px}`]
})
export class StorageWorkspaceNavComponent implements OnInit {
  private readonly api = inject(StorageApiService);
  private readonly archive = inject(VisitArchiveApiService);
  private readonly router = inject(Router);
  @Input() year?: number;
  context?: StorageContext;
  canViewArchive = false;
  ngOnInit() { this.api.contextInfo(false).subscribe({next: context => {
    this.context = context;
    this.archive.operationsStatus().subscribe({next: () => this.canViewArchive = true, error: () => this.canViewArchive = false});
  }, error: () => {this.context = undefined; this.canViewArchive = false;}}); }
  active(section: 'files' | 'review' | 'calendar' | 'visits' | 'imports') {
    const path = this.router.url.split('?')[0];
    if (section === 'files') return path === '/school-manager/storage' && !this.router.url.includes('view=review') && !this.router.url.includes('requirement=');
    if (section === 'review') return path === '/school-manager/storage' && (this.router.url.includes('view=review') || this.router.url.includes('requirement='));
    if (section === 'calendar') return ['/readiness','/gaps','/tracker','/reports','/digital-index','/manual','/standards'].some(part => path.includes('/storage' + part));
    return path.endsWith('/' + section);
  }
}
