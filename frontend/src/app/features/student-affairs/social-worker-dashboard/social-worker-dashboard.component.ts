import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ReferralDto, ReferralPriority, SummonDto } from '../../../core/models/phase5.models';
import { DashboardCountDto, SocialWorkerStudentAffairsDashboardDto } from '../../../core/models/student-affairs-dashboard.models';
import { Phase5Service } from '../../../core/services/phase5.service';

@Component({
  selector: 'app-social-worker-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './social-worker-dashboard.component.html',
  styleUrls: ['../manager-workspace.css', './social-worker-dashboard.component.css']
})
export class SocialWorkerDashboardComponent {
  private readonly api = inject(Phase5Service);

  readonly loading = signal(true);
  readonly errorMessage = signal('');
  readonly dashboard = signal<SocialWorkerStudentAffairsDashboardDto | null>(null);
  readonly recentReferrals = signal<readonly ReferralDto[]>([]);
  readonly upcomingSummons = signal<readonly SummonDto[]>([]);
  readonly refreshedAt = signal<Date | null>(null);

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    forkJoin({
      dashboard: this.api.getSocialWorkerDashboard(),
      referrals: this.api.listReferrals({ pageNumber: 1, pageSize: 6, sortDirection: 'desc' }),
      summons: this.api.listSummons({ pageNumber: 1, pageSize: 6, sortDirection: 'asc' })
    }).subscribe({
      next: response => {
        this.loading.set(false);
        if (!response.dashboard.isSuccess || !response.dashboard.data) {
          this.errorMessage.set(response.dashboard.errors[0] ?? response.dashboard.message ?? 'تعذر تحميل لوحة الموجه الطلابي.');
          return;
        }

        this.dashboard.set(response.dashboard.data);
        const referrals = response.referrals.isSuccess && response.referrals.data ? response.referrals.data.items : [];
        const summons = response.summons.isSuccess && response.summons.data ? response.summons.data.items : [];
        this.recentReferrals.set(this.prioritizeReferrals(referrals));
        this.upcomingSummons.set(this.prioritizeSummons(summons));
        this.refreshedAt.set(new Date());
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.errorMessage.set(error.status === 403
          ? 'لا تملك صلاحية عرض لوحة الموجه الطلابي لهذه المدرسة.'
          : 'تعذر تحميل لوحة الموجه الطلابي. تحقق من الاتصال ثم حاول مرة أخرى.');
      }
    });
  }

  count(counts: readonly DashboardCountDto[], code: string): number {
    return counts.find(item => item.code === code)?.count ?? 0;
  }

  total(counts: readonly DashboardCountDto[]): number {
    return counts.reduce((sum, item) => sum + item.count, 0);
  }

  attentionTotal(dashboard: SocialWorkerStudentAffairsDashboardDto): number {
    return this.total(dashboard.cases)
      + this.count(dashboard.summons, 'Pending')
      + this.count(dashboard.summons, 'UnderObservation');
  }

  criticalCases(): number {
    return this.recentReferrals().filter(item => item.priority === 'Critical' || item.priority === 'High').length;
  }

  scheduledToday(): number {
    const today = new Date();
    return this.upcomingSummons().filter(item => {
      if (!item.scheduledAt) return false;
      const scheduled = new Date(item.scheduledAt);
      return scheduled.getFullYear() === today.getFullYear()
        && scheduled.getMonth() === today.getMonth()
        && scheduled.getDate() === today.getDate();
    }).length;
  }

  priorityLabel(priority: ReferralPriority): string {
    return ({ Normal: 'عادية', High: 'عالية', Critical: 'حرجة' })[priority];
  }

  referralStatusLabel(status: ReferralDto['status']): string {
    return ({ Open: 'مفتوحة', Assigned: 'تم الإسناد', InProgress: 'قيد المتابعة', Resolved: 'تم الحل', Closed: 'مغلقة' })[status];
  }

  summonStatusLabel(status: SummonDto['status']): string {
    return ({ Pending: 'بانتظار الموعد', Attended: 'تم الحضور', UnderObservation: 'تحت الملاحظة', Improved: 'تحسّن' })[status];
  }

  formatDate(value: string | null): string {
    if (!value) return 'لم يُحدد بعد';
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('ar-EG', {
      weekday: 'short', day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit'
    }).format(date);
  }

  formatTime(value: Date | null): string {
    return value ? new Intl.DateTimeFormat('ar-EG', { hour: 'numeric', minute: '2-digit' }).format(value) : '—';
  }

  private prioritizeReferrals(items: readonly ReferralDto[]): readonly ReferralDto[] {
    const statusRank: Record<ReferralDto['status'], number> = { Assigned: 0, InProgress: 1, Open: 2, Resolved: 3, Closed: 4 };
    const priorityRank: Record<ReferralPriority, number> = { Critical: 0, High: 1, Normal: 2 };
    return [...items].sort((left, right) => statusRank[left.status] - statusRank[right.status]
      || priorityRank[left.priority] - priorityRank[right.priority]);
  }

  private prioritizeSummons(items: readonly SummonDto[]): readonly SummonDto[] {
    const statusRank: Record<SummonDto['status'], number> = { Pending: 0, UnderObservation: 1, Attended: 2, Improved: 3 };
    return [...items].sort((left, right) => statusRank[left.status] - statusRank[right.status]
      || this.dateValue(left.scheduledAt) - this.dateValue(right.scheduledAt));
  }

  private dateValue(value: string | null): number {
    if (!value) return Number.MAX_SAFE_INTEGER;
    const date = new Date(value).getTime();
    return Number.isNaN(date) ? Number.MAX_SAFE_INTEGER : date;
  }
}
