import { Component, Input, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { TableModule } from 'primeng/table';
import { DashboardRole, DashboardRoleCode } from '../../../core/models/dashboard.models';
import { DashboardService, downloadDashboardBlob } from '../../../core/services/dashboard.service';
import { ToastService } from '../../../core/services/toast.service';
import { ApiResponse } from '../../../core/models/api-response.model';
import { Observable } from 'rxjs';

type Role = 'main-manager' | 'school-manager' | 'moderator' | 'instructor';

@Component({
  selector: 'app-dashboard-live', standalone: true,
  imports: [CommonModule, TranslateModule, ButtonModule, TagModule, TableModule],
  template: `
    <div class="dashboard-page" dir="rtl">
      <header class="page-header">
        <div><h1>{{ titleKey() | translate }}</h1><p>{{ 'DASHBOARD.LIVE_SUBTITLE' | translate }}</p></div>
        <div class="actions">
          <button pButton type="button" icon="pi pi-refresh" class="p-button-outlined" [label]="'DASHBOARD.REFRESH' | translate" [loading]="loading()" (click)="load()"></button>
          <button pButton type="button" icon="pi pi-file-excel" class="p-button-outlined p-button-success" [label]="'DASHBOARD.EXCEL' | translate" [loading]="exporting() === 'excel'" (click)="export('excel')"></button>
          <button pButton type="button" icon="pi pi-file-pdf" class="p-button-outlined p-button-danger" [label]="'DASHBOARD.PDF' | translate" [loading]="exporting() === 'pdf'" (click)="export('pdf')"></button>
        </div>
      </header>
      <section *ngIf="loading()" class="surface-card loading">{{ 'COMMON.LOADING' | translate }}</section>
      <ng-container *ngIf="data() as d">
        <section class="metrics">
          <article *ngFor="let metric of metrics()" class="surface-card metric"><span>{{ metric.label | translate }}</span><strong>{{ metric.value }}</strong></article>
        </section>
        <section class="surface-card" *ngIf="statusRows().length"><h2>{{ 'DASHBOARD.VISITS_BY_STATUS' | translate }}</h2><div class="status-row"><p-tag *ngFor="let row of statusRows()" [value]="row.statusLabelAr + ': ' + row.count" severity="info"></p-tag></div></section>
        <section class="surface-card" *ngIf="rankingRows().length"><h2>{{ rankingTitleKey() | translate }}</h2><p-table [value]="rankingRows()" [paginator]="rankingRows().length > 8" [rows]="8"><ng-template pTemplate="header"><tr><th>{{ 'DASHBOARD.NAME' | translate }}</th><th>{{ 'DASHBOARD.VISITS' | translate }}</th><th>{{ 'DASHBOARD.APPROVED' | translate }}</th><th>{{ 'DASHBOARD.AVERAGE' | translate }}</th></tr></ng-template><ng-template pTemplate="body" let-row><tr><td>{{ row.schoolName || row.instructorFullName || row.moderatorFullName || row.subject }}</td><td>{{ row.visitsCount ?? row.approvedVisitsCount ?? '—' }}</td><td>{{ row.approvedVisitsCount ?? '—' }}</td><td>{{ row.averageOverallScore ?? '—' }}</td></tr></ng-template></p-table></section>
        <section class="surface-card" *ngIf="latestEvaluation() as latest"><h2>{{ 'DASHBOARD.LATEST_EVALUATION' | translate }}</h2><p>{{ latest.visitCategoryLabelAr }} — {{ latest.overallScore }} — {{ latest.performanceLevelAr }}</p></section>
      </ng-container>
    </div>`,
  styles: [`:host{display:block}.dashboard-page{display:flex;flex-direction:column;gap:var(--space-4)}.page-header{display:flex;justify-content:space-between;gap:var(--space-3);align-items:flex-start;flex-wrap:wrap}.page-header h1{margin:0;color:var(--text-strong)}.page-header p{margin:4px 0;color:var(--text-muted)}.actions,.status-row{display:flex;gap:var(--space-2);flex-wrap:wrap}.metrics{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:var(--space-3)}.metric{padding:var(--space-3);display:flex;flex-direction:column;gap:var(--space-2)}.metric span{color:var(--text-muted);font-size:.85rem}.metric strong{color:var(--brand-700);font-size:1.7rem}.surface-card{padding:var(--space-4)}h2{margin:0 0 var(--space-3);font-size:1.05rem}.loading{text-align:center}`]
})
export class DashboardLiveComponent implements OnInit {
  @Input({ required: true }) role!: Role;
  private readonly dashboard = inject(DashboardService); private readonly toast = inject(ToastService);
  readonly data = signal<any>(null); readonly loading = signal(false); readonly exporting = signal<'excel'|'pdf'|null>(null);
  readonly titleKey = computed(() => `DASHBOARD.${this.role.replace('-', '_').toUpperCase()}`);
  readonly rankingTitleKey = computed(() => this.role === 'main-manager' ? 'DASHBOARD.SCHOOL_COMPARISON' : this.role === 'school-manager' ? 'DASHBOARD.MODERATOR_PERFORMANCE' : 'DASHBOARD.TOP_INSTRUCTORS');
  readonly metrics = computed(() => { const d=this.data(); if(!d)return []; const keys: Record<Role,[string,string][]>={
    'main-manager':[['DASHBOARD.METRIC.SCHOOLS','schoolsCount'],['DASHBOARD.METRIC.INSTRUCTORS','instructorsCount'],['DASHBOARD.METRIC.VISITS','visitsCount'],['DASHBOARD.METRIC.APPROVED','approvedEvaluationsCount']],
    'school-manager':[['DASHBOARD.METRIC.INSTRUCTORS','instructorsCount'],['DASHBOARD.METRIC.MODERATORS','moderatorsCount'],['DASHBOARD.METRIC.PENDING','evaluationsPendingApprovalCount'],['DASHBOARD.METRIC.COMPLAINTS','complaintsCount']],
    'moderator':[['DASHBOARD.METRIC.TODAY','todaysVisitsCount'],['DASHBOARD.METRIC.DRAFTS','draftVisitsCount'],['DASHBOARD.METRIC.PENDING','evaluationsPendingApprovalCount'],['DASHBOARD.METRIC.APPROVED','approvedVisitsCount']],
    'instructor':[['DASHBOARD.METRIC.APPROVED','approvedVisitsCount'],['DASHBOARD.METRIC.PLANS','openImprovementPlansCount'],['DASHBOARD.METRIC.FOLLOWUPS','totalFollowUpsCount'],['DASHBOARD.METRIC.VIEWS','reportViewedCount']]}; return keys[this.role].map(([label,key])=>({label,value:d[key] ?? 0})); });
  readonly statusRows = computed(() => this.data()?.visitsByStatus ?? []);
  readonly rankingRows = computed(() => { const d=this.data(); if(!d)return []; return d.schoolComparison ?? d.moderatorPerformance ?? d.topInstructors ?? []; });
  readonly latestEvaluation = computed(() => this.data()?.latestEvaluation ?? null);
  ngOnInit(): void { this.load(); }
  load(): void { this.loading.set(true); const request: Observable<ApiResponse<any>> = this.role==='main-manager'?this.dashboard.getMainManager():this.role==='school-manager'?this.dashboard.getSchoolManager():this.role==='moderator'?this.dashboard.getModerator():this.dashboard.getInstructor(); request.subscribe({next:r=>{if(r.isSuccess)this.data.set(r.data);else this.toast.error('COMMON.ERROR',r.message||'');this.loading.set(false)},error:()=>this.loading.set(false)}); }
  export(kind:'excel'|'pdf'): void { this.exporting.set(kind); const code:Record<Role,DashboardRoleCode>={'main-manager':DashboardRole.MainManager,'school-manager':DashboardRole.SchoolManager,moderator:DashboardRole.Moderator,instructor:DashboardRole.Instructor}; const request=kind==='excel'?this.dashboard.exportExcel(code[this.role]):this.dashboard.exportPdf(code[this.role]); request.subscribe({next:r=>{const result=downloadDashboardBlob(r,`dashboard.${kind==='excel'?'xlsx':'pdf'}`);if(!result.ok)this.toast.error('COMMON.ERROR',result.message);this.exporting.set(null)},error:()=>this.exporting.set(null)}); }
}
