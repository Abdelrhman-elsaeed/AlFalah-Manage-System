import { CommonModule } from '@angular/common';
import { Component, Input, OnInit, computed, inject, signal } from '@angular/core';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ChartData } from 'chart.js';
import { ButtonModule } from 'primeng/button';
import { ChartModule } from 'primeng/chart';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { Observable } from 'rxjs';
import {
  DashboardRole,
  DashboardRoleCode,
  ImprovementPlanAnalytics,
  InstructorDashboard,
  MainManagerDashboard,
  ModeratorDashboard,
  SchoolManagerDashboard
} from '../../../core/models/dashboard.models';
import { ApiResponse } from '../../../core/models/api-response.model';
import { DashboardService, downloadDashboardBlob } from '../../../core/services/dashboard.service';
import { ToastService } from '../../../core/services/toast.service';

type DashboardRoleName = 'main-manager' | 'school-manager' | 'moderator' | 'instructor';
type DashboardData = MainManagerDashboard | SchoolManagerDashboard | ModeratorDashboard | InstructorDashboard;
type MetricTone = 'brand' | 'gold' | 'success' | 'danger';

interface DashboardMetric {
  labelKey: string;
  value: number | string;
  icon: string;
  tone: MetricTone;
}

interface RankingRow {
  name: string;
  visits: number;
  approved: number;
  average: number | null;
}

@Component({
  selector: 'app-dashboard-live',
  standalone: true,
  imports: [CommonModule, TranslateModule, ButtonModule, ChartModule, TableModule, TagModule],
  templateUrl: './dashboard-live.component.html',
  styleUrls: ['./dashboard-live.component.css']
})
export class DashboardLiveComponent implements OnInit {
  @Input({ required: true }) role!: DashboardRoleName;

  private readonly dashboard = inject(DashboardService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  readonly data = signal<DashboardData | null>(null);
  readonly loading = signal(false);
  readonly exporting = signal<'excel' | 'pdf' | null>(null);

  readonly titleKey = computed(() => `DASHBOARD.${this.role.replace('-', '_').toUpperCase()}`);
  readonly rankingTitleKey = computed(() => {
    if (this.role === 'main-manager') return 'DASHBOARD.SCHOOL_COMPARISON';
    if (this.role === 'school-manager') return 'DASHBOARD.MODERATOR_PERFORMANCE';
    return 'DASHBOARD.TOP_INSTRUCTORS';
  });

  readonly metrics = computed<DashboardMetric[]>(() => {
    const data = this.data();
    if (!data) return [];

    switch (this.role) {
      case 'main-manager': {
        const d = data as MainManagerDashboard;
        return [
          this.metric('DASHBOARD.METRIC.SCHOOLS', d.schoolsCount, 'pi-building', 'brand'),
          this.metric('DASHBOARD.METRIC.ACTIVE_SCHOOLS', d.activeSchoolsCount, 'pi-check-circle', 'success'),
          this.metric('DASHBOARD.METRIC.SCHOOL_MANAGERS', d.schoolManagersCount, 'pi-id-card', 'gold'),
          this.metric('DASHBOARD.METRIC.MODERATORS', d.moderatorsCount, 'pi-users', 'brand'),
          this.metric('DASHBOARD.METRIC.INSTRUCTORS', d.instructorsCount, 'pi-user', 'brand'),
          this.metric('DASHBOARD.METRIC.VISITS', d.visitsCount, 'pi-clipboard', 'gold'),
          this.metric('DASHBOARD.METRIC.APPROVED', d.approvedEvaluationsCount, 'pi-verified', 'success'),
          this.metric('DASHBOARD.METRIC.AVERAGE', this.score(d.averageOverallScore), 'pi-chart-line', 'brand')
        ];
      }
      case 'school-manager': {
        const d = data as SchoolManagerDashboard;
        return [
          this.metric('DASHBOARD.METRIC.INSTRUCTORS', d.instructorsCount, 'pi-user', 'brand'),
          this.metric('DASHBOARD.METRIC.MODERATORS', d.moderatorsCount, 'pi-users', 'brand'),
          this.metric('DASHBOARD.METRIC.MONTH_VISITS', d.visitsThisMonthCount, 'pi-calendar', 'gold'),
          this.metric('DASHBOARD.METRIC.PENDING', d.evaluationsPendingApprovalCount, 'pi-clock', 'gold'),
          this.metric('DASHBOARD.METRIC.NEEDS_IMPROVEMENT', d.instructorsNeedingImprovementCount, 'pi-exclamation-triangle', 'danger'),
          this.metric('DASHBOARD.METRIC.COMPLAINTS', d.complaintsCount, 'pi-comment', 'danger'),
          this.metric('DASHBOARD.METRIC.PLANS', d.improvementPlans.totalActive, 'pi-list-check', 'success')
        ];
      }
      case 'moderator': {
        const d = data as ModeratorDashboard;
        return [
          this.metric('DASHBOARD.METRIC.TODAY', d.todaysVisitsCount, 'pi-calendar', 'brand'),
          this.metric('DASHBOARD.METRIC.DRAFTS', d.draftVisitsCount, 'pi-file-edit', 'gold'),
          this.metric('DASHBOARD.METRIC.PENDING', d.evaluationsPendingApprovalCount, 'pi-clock', 'gold'),
          this.metric('DASHBOARD.METRIC.APPROVED', d.approvedVisitsCount, 'pi-verified', 'success'),
          this.metric('DASHBOARD.METRIC.EVALUATED_INSTRUCTORS', d.instructorsEvaluatedCount, 'pi-users', 'brand'),
          this.metric('DASHBOARD.METRIC.AVERAGE', this.score(d.averageOverallScore), 'pi-chart-line', 'brand'),
          this.metric('DASHBOARD.METRIC.PLANS', d.openImprovementPlansCount, 'pi-list-check', 'success')
        ];
      }
      case 'instructor': {
        const d = data as InstructorDashboard;
        return [
          this.metric('DASHBOARD.METRIC.APPROVED', d.approvedVisitsCount, 'pi-verified', 'success'),
          this.metric('DASHBOARD.METRIC.LATEST_SCORE', this.score(d.latestEvaluation?.overallScore ?? null), 'pi-star', 'gold'),
          this.metric('DASHBOARD.METRIC.PLANS', d.openImprovementPlansCount, 'pi-list-check', 'brand'),
          this.metric('DASHBOARD.METRIC.FOLLOWUPS', d.totalFollowUpsCount, 'pi-history', 'brand'),
          this.metric('DASHBOARD.METRIC.VIEWS', d.reportViewedCount, 'pi-eye', 'success')
        ];
      }
    }
  });

  readonly statusRows = computed(() => {
    const data = this.data();
    return data && 'visitsByStatus' in data ? data.visitsByStatus : [];
  });

  readonly rankingRows = computed<RankingRow[]>(() => {
    const data = this.data();
    if (!data) return [];

    if (this.role === 'main-manager') {
      return (data as MainManagerDashboard).schoolComparison.map(row => ({
        name: row.schoolName,
        visits: row.visitsCount,
        approved: row.approvedVisitsCount,
        average: row.averageOverallScore
      }));
    }
    if (this.role === 'school-manager') {
      return (data as SchoolManagerDashboard).moderatorPerformance.map(row => ({
        name: row.moderatorFullName,
        visits: row.visitsCount,
        approved: row.approvedVisitsCount,
        average: row.averageOverallScore
      }));
    }
    if (this.role === 'moderator') {
      return (data as ModeratorDashboard).topInstructors.map(row => ({
        name: row.instructorFullName,
        visits: row.approvedVisitsCount,
        approved: row.approvedVisitsCount,
        average: row.averageOverallScore
      }));
    }
    return [];
  });

  readonly planAnalytics = computed<ImprovementPlanAnalytics | null>(() => {
    const data = this.data();
    if (this.role === 'main-manager') return (data as MainManagerDashboard | null)?.improvementPlans ?? null;
    if (this.role === 'school-manager') return (data as SchoolManagerDashboard | null)?.improvementPlans ?? null;
    return null;
  });

  readonly instructorData = computed(() => this.role === 'instructor'
    ? this.data() as InstructorDashboard | null
    : null);

  readonly statusChartData = computed<ChartData<'doughnut'> | null>(() => {
    const rows = this.statusRows();
    if (rows.length === 0) return null;
    return {
      labels: rows.map(row => row.statusLabelAr),
      datasets: [{
        data: rows.map(row => row.count),
        backgroundColor: ['#0F7132', '#D4AF37', '#2563EB', '#22C55E', '#DC2626', '#7C3AED', '#EA580C', '#64748B'],
        borderColor: '#FFFFFF',
        borderWidth: 2
      }]
    };
  });

  readonly performanceChartData = computed<ChartData<'bar' | 'line'> | null>(() => {
    const data = this.data();
    if (!data) return null;

    let labels: string[] = [];
    let values: number[] = [];
    if (this.role === 'main-manager') {
      const rows = (data as MainManagerDashboard).schoolComparison.filter(row => row.averageOverallScore !== null);
      labels = rows.map(row => row.schoolName);
      values = rows.map(row => row.averageOverallScore ?? 0);
    } else if (this.role === 'school-manager') {
      const rows = (data as SchoolManagerDashboard).subjectPerformance.filter(row => row.averageOverallScore !== null);
      labels = rows.map(row => row.subject);
      values = rows.map(row => row.averageOverallScore ?? 0);
    } else if (this.role === 'moderator') {
      const rows = (data as ModeratorDashboard).topInstructors.filter(row => row.averageOverallScore !== null);
      labels = rows.map(row => row.instructorFullName);
      values = rows.map(row => row.averageOverallScore ?? 0);
    } else {
      const rows = (data as InstructorDashboard).performanceTrend;
      labels = rows.map(row => this.formatDate(row.visitDate));
      values = rows.map(row => row.overallScore);
    }

    if (labels.length === 0) return null;
    const isLine = this.role === 'instructor';
    return {
      labels,
      datasets: [{
        label: this.translate.instant('DASHBOARD.AVERAGE'),
        data: values,
        backgroundColor: isLine ? 'rgba(15, 113, 50, 0.16)' : 'rgba(15, 113, 50, 0.78)',
        borderColor: '#0F7132',
        borderWidth: 2,
        fill: isLine,
        tension: 0.32,
        pointBackgroundColor: '#D4AF37',
        pointBorderColor: '#0F7132',
        pointRadius: isLine ? 4 : 0
      }]
    } as ChartData<'bar' | 'line'>;
  });

  readonly performanceChartType = computed<'bar' | 'line'>(() => this.role === 'instructor' ? 'line' : 'bar');
  readonly performanceChartTitleKey = computed(() => this.role === 'instructor'
    ? 'DASHBOARD.PERFORMANCE_TREND'
    : 'DASHBOARD.PERFORMANCE_COMPARISON');

  readonly doughnutOptions = {
    maintainAspectRatio: false,
    cutout: '62%',
    plugins: {
      legend: { position: 'bottom', rtl: true, labels: { usePointStyle: true, padding: 16 } },
      tooltip: { rtl: true, textDirection: 'rtl' }
    }
  };

  readonly cartesianOptions = {
    maintainAspectRatio: false,
    plugins: {
      legend: { display: false },
      tooltip: { rtl: true, textDirection: 'rtl' }
    },
    scales: {
      x: { ticks: { color: '#64748B' }, grid: { display: false } },
      y: { beginAtZero: true, max: 4, ticks: { stepSize: 1, color: '#64748B' }, grid: { color: 'rgba(100, 116, 139, 0.12)' } }
    }
  };

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    if (this.loading()) return;
    this.loading.set(true);
    this.requestForRole().subscribe({
      next: response => {
        if (response.isSuccess && response.data) {
          this.data.set(response.data);
        } else {
          this.toast.error(
            this.translate.instant('COMMON.ERROR'),
            response.message || this.translate.instant('DASHBOARD.LOAD_FAILED')
          );
        }
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  export(kind: 'excel' | 'pdf'): void {
    if (this.exporting()) return;
    this.exporting.set(kind);
    const request = kind === 'excel'
      ? this.dashboard.exportExcel(this.roleCode())
      : this.dashboard.exportPdf(this.roleCode());

    request.subscribe({
      next: response => {
        const extension = kind === 'excel' ? 'xlsx' : 'pdf';
        const result = downloadDashboardBlob(response, `dashboard.${extension}`);
        if (result.ok) {
          this.toast.success(this.translate.instant('DASHBOARD.EXPORT_SUCCESS'));
        } else {
          this.toast.error(this.translate.instant('COMMON.ERROR'), this.translate.instant(result.message));
        }
        this.exporting.set(null);
      },
      error: () => this.exporting.set(null)
    });
  }

  statusSeverity(status: number): 'success' | 'info' | 'warning' | 'danger' | 'secondary' {
    if (status === 4) return 'success';
    if (status === 3 || status === 6 || status === 7) return 'warning';
    if (status === 5 || status === 8) return 'danger';
    if (status === 1) return 'secondary';
    return 'info';
  }

  private requestForRole(): Observable<ApiResponse<DashboardData>> {
    if (this.role === 'main-manager') {
      return this.dashboard.getMainManager() as Observable<ApiResponse<DashboardData>>;
    }
    if (this.role === 'school-manager') {
      return this.dashboard.getSchoolManager() as Observable<ApiResponse<DashboardData>>;
    }
    if (this.role === 'moderator') {
      return this.dashboard.getModerator() as Observable<ApiResponse<DashboardData>>;
    }
    return this.dashboard.getInstructor() as Observable<ApiResponse<DashboardData>>;
  }

  private roleCode(): DashboardRoleCode {
    const roles: Record<DashboardRoleName, DashboardRoleCode> = {
      'main-manager': DashboardRole.MainManager,
      'school-manager': DashboardRole.SchoolManager,
      moderator: DashboardRole.Moderator,
      instructor: DashboardRole.Instructor
    };
    return roles[this.role];
  }

  private metric(labelKey: string, value: number | string, icon: string, tone: MetricTone): DashboardMetric {
    return { labelKey, value, icon, tone };
  }

  private score(value: number | null): string {
    return value === null ? '—' : value.toFixed(2);
  }

  private formatDate(value: string): string {
    const locale = this.translate.currentLang === 'en' ? 'en-SA' : 'ar-SA';
    return new Intl.DateTimeFormat(locale, { month: 'short', year: 'numeric' }).format(new Date(value));
  }
}
