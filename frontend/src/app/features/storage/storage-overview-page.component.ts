import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { StorageEvidenceApiService } from './evidence-api.service';
import { EvidenceCounts } from './evidence.models';
import { StorageApiService } from './storage-api.service';
import { StorageContext } from './storage.models';
import { VisitArchiveApiService } from './visit-archive-api.service';

@Component({
  selector: 'app-storage-overview', standalone: true, imports: [CommonModule, RouterLink],
  template: `<section class="overview" dir="rtl">
    <header class="intro"><div><span class="eyebrow">مساحة الملفات · {{ context?.academicYearName || 'العام الدراسي' }}</span>
      <h1>صباح الخير، {{ auth.currentUser()?.fullName || 'مرحباً' }}</h1>
      <p>ملفات المدرسة وشواهدها وتقويمها في مكان واحد.</p></div></header>
    <p *ngIf="contextError" class="error" role="alert">{{ contextError }} <button type="button" (click)="loadContext()">إعادة المحاولة</button></p>
    <div *ngIf="!context && !contextError" class="skeleton" aria-label="تحميل مساحة الملفات"></div>
    <div *ngIf="context" class="columns">
      <section class="card actions"><div class="card-title">يحتاج إجراء الآن</div>
        <a *ngIf="canReview" routerLink="/school-manager/storage/evidence"><span class="step">1</span><span class="action-icon"><i class="pi pi-inbox"></i></span><span class="action-copy"><strong>مراجعة الشواهد</strong><small>افتح قائمة الشواهد واتخذ القرار في سياق كل رابط.</small></span><i class="pi pi-arrow-left"></i></a>
        <a routerLink="/school-manager/storage/gaps" [queryParams]="{academicYearId:context.academicYearId}"><span class="step">{{ canReview ? 2 : 1 }}</span><span class="action-icon alert"><i class="pi pi-exclamation-triangle"></i></span><span class="action-copy"><strong>متطلبات تحتاج متابعة</strong><small>اعرض النواقص وقاعدة استيفاء كل متطلب.</small></span><i class="pi pi-arrow-left"></i></a>
        <a *ngIf="archiveAvailable" routerLink="/school-manager/storage/visits"><span class="step">{{ canReview ? 3 : 2 }}</span><span class="action-icon"><i class="pi pi-archive"></i></span><span class="action-copy"><strong>تقارير الزيارات</strong><small>{{ archiveReady ? 'الأرشفة تعمل؛ راجع حالة التقارير.' : 'الأرشفة تنتظر تفعيل العامل أو الكتابة الخارجية.' }}</small></span><i class="pi pi-arrow-left"></i></a>
        <a routerLink="/school-manager/storage"><span class="step">{{ (archiveAvailable ? 3 : 2) + (canReview ? 1 : 0) }}</span><span class="action-icon"><i class="pi pi-folder"></i></span><span class="action-copy"><strong>مكتبة المدرسة</strong><small>{{ context.connectionState === 'Connected' ? 'تصفح المجلدات والملفات الحالية.' : 'تحتاج المكتبة إلى إعداد أو اتصال.' }}</small></span><i class="pi pi-arrow-left"></i></a>
      </section>
      <section class="card summary"><div class="card-title">الجاهزية</div>
        <div *ngIf="counts" class="ready"><span class="ring" [style.--progress]="(counts.requirements ? 100 * counts.fulfilledRequirements / counts.requirements : 0) + '%'">{{ counts.requirements ? ((100 * counts.fulfilledRequirements / counts.requirements) | number:'1.0-0') : 0 }}%</span>
          <div><strong>{{ counts.fulfilledRequirements }} من {{ counts.requirements }}</strong> متطلباً مستوفياً.<p>{{ counts.links }} رابط شاهد مسجل و{{ counts.files }} ملف حي.</p><a routerLink="/school-manager/storage/readiness" [queryParams]="{academicYearId:context.academicYearId}">طريقة الحساب <i class="pi pi-arrow-left"></i></a></div></div>
        <div *ngIf="countsLoading" class="skeleton" aria-label="تحميل الجاهزية"></div>
        <p *ngIf="countsError" class="error" role="alert">تعذّر تحميل الجاهزية. <button type="button" (click)="loadCounts()">إعادة المحاولة</button></p>
        <div class="library-shortcuts"><div class="card-title">الملفات</div><a routerLink="/school-manager/storage">مكتبة المدرسة <i class="pi pi-arrow-left"></i></a><p>مجلدات المعلمين وأرشيف الزيارات مساحتان مستقلتان عن مكتبة المدرسة.</p></div>
      </section>
    </div>
  </section>`,
  styles: [`:host{display:block;background:var(--bg-page);min-height:100%;color:var(--text-strong);font-family:var(--font-app)}.overview{padding:30px clamp(16px,3vw,36px) 70px}.intro{margin-bottom:24px}.eyebrow,.card-title{font-size:12px;font-weight:700;color:var(--gold-700)}.intro h1{font-family:var(--font-app);font-size:clamp(22px,2vw,28px);line-height:1.3;margin:4px 0}.intro p{color:var(--text-muted);margin:0}.columns{display:grid;grid-template-columns:minmax(0,1.5fr) minmax(310px,1fr);gap:20px}.card{background:var(--bg-surface);border:1px solid var(--border);border-radius:17px;box-shadow:0 10px 28px -24px var(--text-strong);overflow:hidden}.card-title{padding:14px 18px;border-bottom:1px solid var(--border)}.actions a{display:flex;align-items:center;gap:14px;padding:18px;border-bottom:1px solid var(--border);text-decoration:none;color:var(--text-strong)}.actions a:last-child{border:0}.actions a:hover{background:#ecf5ef}.step{color:#d3cab5;font-size:21px;font-weight:700}.action-icon{display:grid;place-items:center;width:44px;height:44px;flex:none;border-radius:12px;background:#ecf5ef;color:var(--brand-700)}.action-icon.alert{background:#fbecea;color:#8f1f1a}.action-copy{flex:1;min-width:0}.action-copy strong,.action-copy small{display:block}.action-copy small{color:var(--text-muted);font-size:12px;margin-top:3px}.actions a>.pi-arrow-left{font-size:13px;color:var(--text-muted)}.summary{padding-bottom:18px}.summary .card-title{border:0}.ready{display:flex;align-items:center;gap:20px;padding:10px 20px 25px}.ready p{font-size:13px;color:var(--text-muted)}.ready a,.library-shortcuts a{color:var(--brand-700);text-decoration:none}.ring{width:112px;height:112px;flex:none;border-radius:50%;display:grid;place-items:center;background:radial-gradient(closest-side,var(--bg-surface) 80%,transparent 82%),conic-gradient(#187a44 var(--progress),var(--border) 0);color:var(--brand-700);font-size:24px}.library-shortcuts{border-top:1px solid var(--border);margin:0 18px;padding-top:14px}.library-shortcuts .card-title{padding:0 0 12px}.library-shortcuts p{font-size:12px;color:var(--text-muted)}.error{margin:12px;padding:12px;background:#fbecea;color:#8f1f1a;border-radius:10px}.error button{border:0;background:transparent;color:inherit;text-decoration:underline;cursor:pointer}.skeleton{height:90px;margin:14px;border-radius:10px;background:linear-gradient(90deg,#ebe5d8,#f5f1e8,#ebe5d8);background-size:200% 100%;animation:shimmer 1.4s infinite}@keyframes shimmer{to{background-position:-200% 0}}@media(max-width:800px){.columns{grid-template-columns:1fr}.overview{padding:22px 16px 85px}.intro h1{font-size:24px}}`]
})
export class StorageOverviewPageComponent implements OnInit, OnDestroy {
  readonly auth = inject(AuthService);
  private readonly storage = inject(StorageApiService);
  private readonly evidence = inject(StorageEvidenceApiService);
  private readonly archive = inject(VisitArchiveApiService);
  private readonly destroyed = new Subject<void>();
  context?: StorageContext; counts?: EvidenceCounts;
  get canReview() { return !!(this.context?.canReviewEvidence ?? this.context?.canManage); }
  contextError = ''; countsError = ''; countsLoading = false;
  archiveAvailable = false; archiveReady = false;
  ngOnInit() { this.loadContext(); }
  loadContext() {
    this.contextError = '';
    this.storage.contextInfo(false).pipe(takeUntil(this.destroyed)).subscribe({next: context => {
      this.context = context; if (context.academicYearId) this.loadCounts();
      this.archive.operationsStatus().pipe(takeUntil(this.destroyed)).subscribe({next: status => {this.archiveAvailable = true; this.archiveReady = status.ready;},error: () => this.archiveAvailable = false});
    },error: () => this.contextError = 'تعذّر تحميل مساحة الملفات.'});
  }
  loadCounts() {
    if (!this.context?.academicYearId) return;
    this.countsLoading = true; this.countsError = '';
    this.evidence.counts(this.context.academicYearId, false).pipe(takeUntil(this.destroyed)).subscribe({next: counts => {this.counts = counts; this.countsLoading = false;},error: () => {this.countsLoading = false; this.countsError = 'تعذّر تحميل الجاهزية.';}});
  }
  ngOnDestroy() { this.destroyed.next(); this.destroyed.complete(); }
}
