import { CommonModule } from '@angular/common';
import { Component, DestroyRef, Input, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ChartData } from 'chart.js';
import { ButtonModule } from 'primeng/button';
import { ChartModule } from 'primeng/chart';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { Observable, interval } from 'rxjs';
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
import { extractHttpErrorMessage, readHttpErrorBody } from '../../../core/http/http-error-message';
import {
  PUBLISHED_MAXIMUM,
  formatPublishedScore,
  publishedScorePercent,
  toPublishedDelta,
  toPublishedScore
} from '../../score-scale';
import { SchoolMapComponent, SchoolMapMarker } from '../school-map/school-map.component';
import { PublishedScorePipe } from '../../score-scale';

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

interface ToolCard {
  id: string;
  label: string;
  icon: string;
  route: string;
  stat: string | number;
  statLabel: string;
  badge: number;
  badgeLabel: string;
  color: 'brand' | 'gold' | 'danger' | 'success' | 'info' | 'purple';
}

interface UrgentAction {
  icon: string;
  label: string;
  count: number;
  route: string;
  tone: 'danger' | 'gold';
}

interface DashboardHighlight {
  labelKey: string;
  value: string;
  icon: string;
}

@Component({
  selector: 'app-dashboard-live',
  standalone: true,
  imports: [CommonModule, TranslateModule, ButtonModule, ChartModule, TableModule, TagModule, TooltipModule, SchoolMapComponent,
    PublishedScorePipe
  ],
  templateUrl: './dashboard-live.component.html',
  styleUrls: ['./dashboard-live.component.css']
})
export class DashboardLiveComponent implements OnInit {
  @Input({ required: true }) role!: DashboardRoleName;

  private readonly dashboard = inject(DashboardService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);

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

  readonly highlights = computed<DashboardHighlight[]>(() => {
    const data = this.data();
    if (!data) return [];
    const total = this.statusRows().reduce((sum, row) => sum + row.count, 0);
    const approved = this.statusRows().find(row => row.status === 4)?.count ?? 0;
    const approvalRate = total === 0 ? 0 : Math.round((approved / total) * 100);

    if (this.role === 'main-manager') {
      const d = data as MainManagerDashboard;
      const best = [...d.schoolComparison]
        .filter(row => row.averageOverallScore !== null)
        .sort((a, b) => (b.averageOverallScore ?? 0) - (a.averageOverallScore ?? 0))[0];
      return [
        { labelKey: 'DASHBOARD.HIGHLIGHT.APPROVAL_RATE', value: `${approvalRate}%`, icon: 'pi-verified' },
        { labelKey: 'DASHBOARD.HIGHLIGHT.TOP_SCHOOL', value: best?.schoolName ?? '—', icon: 'pi-trophy' },
        { labelKey: 'DASHBOARD.HIGHLIGHT.ACTIVE_COVERAGE', value: `${d.activeSchoolsCount}/${d.schoolsCount}`, icon: 'pi-map-marker' }
      ];
    }
    if (this.role === 'school-manager') {
      const d = data as SchoolManagerDashboard;
      return [
        { labelKey: 'DASHBOARD.HIGHLIGHT.APPROVAL_RATE', value: `${approvalRate}%`, icon: 'pi-verified' },
        { labelKey: 'DASHBOARD.HIGHLIGHT.PENDING_REVIEW', value: String(d.evaluationsPendingApprovalCount), icon: 'pi-clock' },
        { labelKey: 'DASHBOARD.HIGHLIGHT.SUPPORT_LOAD', value: String(d.instructorsNeedingImprovementCount), icon: 'pi-bolt' }
      ];
    }
    if (this.role === 'moderator') {
      const d = data as ModeratorDashboard;
      return [
        { labelKey: 'DASHBOARD.HIGHLIGHT.APPROVAL_RATE', value: `${approvalRate}%`, icon: 'pi-verified' },
        { labelKey: 'DASHBOARD.HIGHLIGHT.TODAY_FOCUS', value: String(d.todaysVisitsCount), icon: 'pi-calendar' },
        { labelKey: 'DASHBOARD.HIGHLIGHT.WORK_IN_PROGRESS', value: String(d.draftVisitsCount + d.evaluationsPendingApprovalCount), icon: 'pi-spinner' }
      ];
    }

    const d = data as InstructorDashboard;
    const trend = d.performanceTrend;
    const delta = trend.length < 2 ? null : trend[trend.length - 1].overallScore - trend[trend.length - 2].overallScore;
    return [
      { labelKey: 'DASHBOARD.HIGHLIGHT.LATEST_SCORE', value: this.score(d.latestEvaluation?.overallScore ?? null), icon: 'pi-star' },
      {
        labelKey: 'DASHBOARD.HIGHLIGHT.TREND_CHANGE',
        // Published on the same 0–100 scale as the score beside it, otherwise a
        // "+0.34" sat next to a "78" and read as a different quantity.
        value: delta === null ? '—' : `${delta >= 0 ? '+' : ''}${toPublishedDelta(delta)}`,
        icon: 'pi-chart-line'
      },
      { labelKey: 'DASHBOARD.HIGHLIGHT.FOLLOWUP_ACTIVITY', value: String(d.totalFollowUpsCount), icon: 'pi-history' }
    ];
  });

  readonly schoolMapPoints = computed<SchoolMapMarker[]>(() => {
    if (this.role !== 'main-manager') return [];
    const rows = (this.data() as MainManagerDashboard | null)?.schoolComparison ?? [];
    return rows
      .filter(row => row.latitude !== null && row.longitude !== null)
      .map(row => ({
        id: row.schoolId,
        name: row.schoolName,
        city: row.schoolLocationName || row.city || this.translate.instant('DASHBOARD.MAP.UNKNOWN_CITY'),
        region: row.regionName,
        locationDetails: row.locationDetails,
        latitude: row.latitude!,
        longitude: row.longitude!,
        average: row.averageOverallScore,
      }));
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

  /* ────── School Manager: Urgent Actions ────── */
  readonly urgentActions = computed<UrgentAction[]>(() => {
    if (this.role !== 'school-manager') return [];
    const d = this.data() as SchoolManagerDashboard | null;
    if (!d) return [];
    const actions: UrgentAction[] = [];
    if (d.evaluationsPendingApprovalCount > 0) {
      actions.push({
        icon: 'pi-verified',
        label: 'زيارات بانتظار اعتمادك',
        count: d.evaluationsPendingApprovalCount,
        route: '/visits',
        tone: 'gold'
      });
    }
    if (d.openComplaintsCount > 0) {
      actions.push({
        icon: 'pi-flag',
        label: 'شكاوى تحتاج متابعة',
        count: d.openComplaintsCount,
        route: '/complaints',
        tone: 'danger'
      });
    }
    if (d.instructorsNeedingImprovementCount > 0) {
      actions.push({
        icon: 'pi-exclamation-triangle',
        label: 'معلمون يحتاجون دعماً مهنياً',
        count: d.instructorsNeedingImprovementCount,
        route: '/teachers',
        tone: 'danger'
      });
    }
    return actions;
  });

  /* ────── School Manager: Interactive Tool Cards ────── */
  readonly schoolManagerTools = computed<ToolCard[]>(() => {
    if (this.role !== 'school-manager') return [];
    const d = this.data() as SchoolManagerDashboard | null;
    if (!d) return [];
    const totalVisits = d.visitsByStatus.reduce((sum, r) => sum + r.count, 0);
    return [
      {
        id: 'visits',
        label: 'الزيارات الصفية',
        icon: 'pi-clipboard',
        route: '/visits',
        stat: totalVisits,
        statLabel: 'إجمالي الزيارات',
        badge: d.evaluationsPendingApprovalCount,
        badgeLabel: 'بانتظار الاعتماد',
        color: 'brand'
      },
      {
        id: 'teachers',
        label: 'الكادر التعليمي',
        icon: 'pi-users',
        route: '/teachers',
        stat: d.instructorsCount,
        statLabel: 'معلم مسجل',
        badge: d.instructorsNeedingImprovementCount,
        badgeLabel: 'يحتاجون دعماً',
        color: 'info'
      },
      {
        id: 'moderators',
        label: 'المشرفون التربويون',
        icon: 'pi-user-edit',
        route: '/users/moderators',
        stat: d.moderatorsCount,
        statLabel: 'مشرف نشط',
        badge: 0,
        badgeLabel: '',
        color: 'purple'
      },
      {
        id: 'plans',
        label: 'خطط التحسين',
        icon: 'pi-list-check',
        route: '/improvement-plans',
        stat: d.improvementPlans.totalActive,
        statLabel: 'خطة نشطة',
        badge: 0,
        badgeLabel: '',
        color: 'success'
      },
      {
        id: 'complaints',
        label: 'الشكاوى والمقترحات',
        icon: 'pi-flag',
        route: '/complaints',
        stat: d.complaintsCount,
        statLabel: 'شكوى مسجلة',
        badge: d.openComplaintsCount,
        badgeLabel: 'بحاجة متابعة',
        color: 'danger'
      },
      {
        id: 'evidence',
        label: 'مصفوفة متابعة الأدلة',
        icon: 'pi-table',
        route: '/school-manager/evidence-matrix',
        stat: d.instructorsCount,
        statLabel: 'ملف إنجاز',
        badge: 0,
        badgeLabel: '',
        color: 'gold'
      },
      {
        id: 'timetable',
        label: 'الجدول المدرسي',
        icon: 'pi-calendar-plus',
        route: '/timetable',
        stat: '—',
        statLabel: 'إدارة الحصص',
        badge: 0,
        badgeLabel: '',
        color: 'info'
      },
      {
        id: 'attendance',
        label: 'الحضور والانصراف',
        icon: 'pi-calendar',
        route: '/attendance',
        stat: '—',
        statLabel: 'رصد الانضباط',
        badge: 0,
        badgeLabel: '',
        color: 'brand'
      },
      {
        id: 'surveys',
        label: 'استبيانات أولياء الأمور',
        icon: 'pi-file-edit',
        route: '/parent-surveys',
        stat: '—',
        statLabel: 'استطلاعات الرأي',
        badge: 0,
        badgeLabel: '',
        color: 'purple'
      },
      {
        id: 'analyzer',
        label: 'محلل تقارير الطلاب',
        icon: 'pi-sparkles',
        route: '/student-analyzer',
        stat: 'AI',
        statLabel: 'تحليل ذكي',
        badge: 0,
        badgeLabel: '',
        color: 'gold'
      }
    ];
  });

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
      values = rows.map(row => toPublishedScore(row.averageOverallScore) ?? 0);
    } else if (this.role === 'school-manager') {
      const rows = (data as SchoolManagerDashboard).subjectPerformance.filter(row => row.averageOverallScore !== null);
      labels = rows.map(row => row.subject);
      values = rows.map(row => toPublishedScore(row.averageOverallScore) ?? 0);
    } else if (this.role === 'moderator') {
      const rows = (data as ModeratorDashboard).topInstructors.filter(row => row.averageOverallScore !== null);
      labels = rows.map(row => row.instructorFullName);
      values = rows.map(row => toPublishedScore(row.averageOverallScore) ?? 0);
    } else {
      const rows = (data as InstructorDashboard).performanceTrend;
      labels = rows.map(row => this.formatDate(row.visitDate));
      values = rows.map(row => toPublishedScore(row.overallScore) ?? 0);
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
        pointRadius: isLine ? 4 : 0,
        borderRadius: isLine ? 0 : 8,
        borderSkipped: false
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
      // The template renders a p-tag per status below the chart, so chart.js's
      // own legend was a second copy of the same list — and with
      // maintainAspectRatio:false inside a fixed-height wrap it overlapped the
      // tag row. One legend, the accessible one, is kept.
      legend: { display: false },
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
      y: {
        beginAtZero: true,
        // D-UI-1: the axis matches the one published scale. It was capped at 4
        // while the cards beside it counted to 100.
        max: PUBLISHED_MAXIMUM,
        ticks: { stepSize: 20, color: '#64748B' },
        grid: { color: 'rgba(100, 116, 139, 0.12)' }
      }
    }
  };

  ngOnInit(): void {
    this.load();
    interval(60_000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.load());
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
      // The response is a blob, so the server's Arabic reason must be read out
      // of the body. Previously this branch set no message at all and a failed
      // export looked to the user like nothing had happened.
      error: async err => {
        this.exporting.set(null);
        await readHttpErrorBody(err);
        this.toast.error(
          this.translate.instant('COMMON.ERROR'),
          extractHttpErrorMessage(err) ?? this.translate.instant('DASHBOARD.EXPORT_FAILED'));
      }
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

  /** Navigate to the given route (for tool card clicks). */
  navigateTo(route: string): void {
    this.router.navigate([route]);
  }

  /** Meter width as a share of the published maximum (0–100). */
  scorePercent(rawOutOfFour: number | null): number {
    return publishedScorePercent(rawOutOfFour);
  }

  private metric(labelKey: string, value: number | string, icon: string, tone: MetricTone): DashboardMetric {
    return { labelKey, value, icon, tone };
  }

  /**
   * D-UI-1 — every score the dashboard shows is published on 0–100. The API
   * still returns the rubric's internal 0–4 average.
   */
  private score(value: number | null): string {
    return formatPublishedScore(value);
  }

  private formatDate(value: string): string {
    const locale = this.translate.currentLang === 'en' ? 'en-SA' : 'ar-SA';
    return new Intl.DateTimeFormat(locale, { month: 'short', year: 'numeric' }).format(new Date(value));
  }

}
