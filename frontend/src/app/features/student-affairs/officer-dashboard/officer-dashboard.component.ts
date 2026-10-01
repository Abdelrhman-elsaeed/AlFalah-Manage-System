import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { Observable } from 'rxjs';
import { ApiResponse } from '../../../core/models/api-response.model';
import {
  DashboardCountDto,
  OfficerStudentAffairsDashboardDto,
  SchoolOversightDashboardDto
} from '../../../core/models/student-affairs-dashboard.models';
import { StudentAffairsDashboardService } from '../../../core/services/student-affairs-dashboard.service';

type DashboardKind = 'officer' | 'oversight';

@Component({
  selector: 'app-officer-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, ButtonModule, CardModule, ProgressSpinnerModule, TableModule, TagModule],
  templateUrl: './officer-dashboard.component.html',
  styleUrls: ['../manager-workspace.css', './officer-dashboard.component.css']
})
export class OfficerDashboardComponent {
  private readonly api = inject(StudentAffairsDashboardService);
  private readonly route = inject(ActivatedRoute);

  readonly kind = (this.route.snapshot.data['dashboardKind'] ?? 'officer') as DashboardKind;
  readonly loading = signal(true);
  readonly errorMessage = signal('');
  readonly officerDashboard = signal<OfficerStudentAffairsDashboardDto | null>(null);
  readonly oversightDashboard = signal<SchoolOversightDashboardDto | null>(null);

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.officerDashboard.set(null);
    this.oversightDashboard.set(null);

    const request$: Observable<ApiResponse<OfficerStudentAffairsDashboardDto | SchoolOversightDashboardDto>> = this.kind === 'oversight'
      ? this.api.getSchoolOversightDashboard()
      : this.api.getOfficerDashboard();

    request$.subscribe({
      next: response => {
        this.loading.set(false);
        if (!response.isSuccess || !response.data) {
          this.errorMessage.set(response.errors[0] ?? response.message ?? 'تعذر تحميل لوحة المعلومات.');
          return;
        }
        if (this.kind === 'oversight') {
          this.oversightDashboard.set(response.data as SchoolOversightDashboardDto);
        } else {
          this.officerDashboard.set(response.data as OfficerStudentAffairsDashboardDto);
        }
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.errorMessage.set(error.status === 403
          ? 'لا تملك صلاحية عرض لوحة المعلومات لهذه المدرسة.'
          : 'تعذر تحميل لوحة المعلومات. حاول مرة أخرى.');
      }
    });
  }

  percentage(value: number, dashboard: SchoolOversightDashboardDto): string {
    const total = dashboard.present + dashboard.absent + dashboard.absentExcused;
    return total === 0 ? '—' : `${((value / total) * 100).toFixed(1)}٪`;
  }

  attendanceTotal(dashboard: SchoolOversightDashboardDto): number {
    return dashboard.present + dashboard.absent + dashboard.absentExcused;
  }

  attendanceRate(dashboard: SchoolOversightDashboardDto): number {
    const total = this.attendanceTotal(dashboard);
    return total === 0 ? 0 : Math.round((dashboard.present / total) * 100);
  }

  totalCounts(counts: readonly DashboardCountDto[]): number {
    return counts.reduce((sum, item) => sum + item.count, 0);
  }

  activeQueueCount(counts: readonly DashboardCountDto[]): number {
    return counts.filter(item => item.count > 0).length;
  }

  clearedQueueCount(counts: readonly DashboardCountDto[]): number {
    return counts.filter(item => item.count === 0).length;
  }

  queueCount(counts: readonly DashboardCountDto[], code: string): number {
    return counts.find(item => item.code === code)?.count ?? 0;
  }

  prioritizedQueues(counts: readonly DashboardCountDto[]): readonly DashboardCountDto[] {
    const rank = (item: DashboardCountDto): number => {
      if (item.count === 0) return 4;
      if (item.code === 'UnreadOfficerThreads') return 0;
      if (item.code === 'UnassignedReferrals') return 1;
      return 2;
    };
    return [...counts].sort((left, right) => rank(left) - rank(right) || right.count - left.count);
  }

  queueState(count: DashboardCountDto): 'new' | 'incomplete' | 'attention' | 'complete' {
    if (count.count === 0) return 'complete';
    if (count.code === 'UnreadOfficerThreads') return 'new';
    if (count.code === 'UnassignedReferrals') return 'incomplete';
    return 'attention';
  }

  queueStateLabel(count: DashboardCountDto): string {
    return ({ new: 'جديد', incomplete: 'غير مكتمل', attention: 'ينتظر الإجراء', complete: 'مكتمل' })[this.queueState(count)];
  }

  queueIcon(code: string): string {
    return ({
      PendingExcuses: 'pi-file-edit',
      RequestedGatePasses: 'pi-ticket',
      ActiveEntryPermits: 'pi-id-card',
      PendingBehaviorNotices: 'pi-exclamation-circle',
      PendingAcademicNotices: 'pi-book',
      OpenReferrals: 'pi-share-alt',
      UnassignedReferrals: 'pi-user-plus',
      AutomationReviews: 'pi-history',
      UnreadOfficerThreads: 'pi-comments'
    } as Record<string, string>)[code] ?? 'pi-inbox';
  }

  classroomRate(row: { present: number; absent: number; absentExcused: number }): number {
    const total = row.present + row.absent + row.absentExcused;
    return total === 0 ? 0 : Math.round((row.present / total) * 100);
  }

  countSeverity(count: DashboardCountDto): 'success' | 'info' | 'warning' | 'danger' {
    const value = count.severity.toLocaleLowerCase('en');
    if (value.includes('critical') || value.includes('danger') || value.includes('high')) return 'danger';
    if (value.includes('warn') || value.includes('medium')) return 'warning';
    if (value.includes('success') || value.includes('low')) return 'success';
    return 'info';
  }

  queueRoute(code: string): string {
    return ({
      PendingExcuses: '/student-affairs/officer/excuses',
      RequestedGatePasses: '/student-affairs/gate-passes',
      ActiveEntryPermits: '/student-affairs/officer/entry-permits',
      PendingBehaviorNotices: '/student-affairs/notification-approvals',
      PendingAcademicNotices: '/student-affairs/notification-approvals',
      OpenReferrals: '/student-affairs/officer/referrals',
      UnassignedReferrals: '/student-affairs/officer/referrals',
      AutomationReviews: '/student-affairs/officer/automation-reviews',
      UnreadOfficerThreads: '/student-affairs/messages'
    } as Record<string, string>)[code] ?? '/student-affairs/officer/operations';
  }

  formatGeneratedAt(value: string): string {
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('ar-SA', { dateStyle: 'medium', timeStyle: 'short' }).format(date);
  }
}
