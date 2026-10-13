import { CommonModule } from '@angular/common';
import { Component, DestroyRef, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslateService } from '@ngx-translate/core';
import { ChartModule } from 'primeng/chart';
import { ChartData, ChartOptions } from 'chart.js';
import { interval } from 'rxjs';
import { SchoolManagerDashboard } from '../../../core/models/dashboard.models';
import { AttendanceCounts, AttendanceTrend, ManagerOperationsCard, ManagerToolCard, SchoolManagerOperations } from '../../../core/models/school-manager-operations.models';
import { AuthService } from '../../../core/services/auth.service';
import { PublishedScorePipe } from '../../../shared/score-scale';
import { DismissalStatisticsComponent } from './dismissal-statistics.component';

@Component({
  selector: 'app-school-manager-overview',
  standalone: true,
  imports: [CommonModule, ChartModule, PublishedScorePipe, DismissalStatisticsComponent],
  templateUrl: './school-manager-overview.component.html',
  styleUrl: './school-manager-overview.component.css'
})
export class SchoolManagerOverviewComponent {
  readonly data = input<SchoolManagerDashboard | null>(null);
  readonly operations = input<SchoolManagerOperations | null>(null);
  readonly tools = input<ManagerToolCard[]>([]);
  readonly operationTools = input<ManagerOperationsCard[]>([]);
  readonly refreshing = input(false);
  readonly failed = input(false);
  readonly refresh = output<void>();
  readonly navigate = output<string>();
  private readonly translate = inject(TranslateService);
  private readonly auth = inject(AuthService);
  private readonly now = signal(Date.now());
  readonly selectedZone = signal('followup');
  readonly expandedTools = signal(false);
  readonly zones = [
    { id: 'students', ar: 'الطلاب', en: 'Students', hintAr: 'الحضور والانضباط', hintEn: 'Attendance', color: '#55B978', target: 'manager-attendance' },
    { id: 'staff', ar: 'الكادر', en: 'Staff', hintAr: 'الحضور والمتابعة', hintEn: 'Staff attendance', color: '#F7D667', target: 'manager-staff' },
    { id: 'quality', ar: 'الجودة التعليمية', en: 'Educational quality', hintAr: 'الزيارات والتقييم', hintEn: 'Visits and evaluation', color: '#D8EEE1', target: 'manager-recent-visits' },
    { id: 'followup', ar: 'المتابعة', en: 'Follow-up', hintAr: 'النشاطات والاستدعاءات', hintEn: 'Activity and summons', color: '#E0B31B', target: 'manager-school-pulse' }
  ];
  readonly priorities = computed(() => {
    const d = this.data();
    return d ? [
      { count: d.evaluationsPendingApprovalCount, ar: 'زيارات بانتظار اعتمادك', en: 'Visits awaiting approval', route: '/visits', icon: 'pi-verified', tone: 'gold' },
      { count: d.openComplaintsCount, ar: 'شكاوى تحتاج متابعة', en: 'Complaints to follow up', route: '/complaints', icon: 'pi-flag', tone: 'danger' },
      { count: d.instructorsNeedingImprovementCount, ar: 'معلمون يحتاجون دعمًا مهنيًا', en: 'Teachers needing support', route: '/teachers', icon: 'pi-users', tone: 'green' }
    ].filter(item => item.count > 0) : [];
  });
  readonly studentChart = computed(() => this.trendChart(this.operations()?.students?.trend ?? []));
  readonly staffChart = computed(() => this.trendChart(this.operations()?.staff.trend ?? []));
  readonly schoolClock = computed(() => {
    const snapshot = this.operations();
    const zone = snapshot?.timeZone ?? 'Asia/Riyadh';
    const parts = new Intl.DateTimeFormat('en-GB', { timeZone: zone, hour: '2-digit', minute: '2-digit', second: '2-digit', hourCycle: 'h23' }).formatToParts(this.now());
    const number = (type: string) => Number(parts.find(part => part.type === type)?.value ?? 0);
    const localDate = new Intl.DateTimeFormat('en-CA', { timeZone: zone, year: 'numeric', month: '2-digit', day: '2-digit' }).format(this.now());
    return { seconds: number('hour') * 3600 + number('minute') * 60 + number('second'), date: localDate };
  });
  readonly currentSlot = computed(() => {
    const snapshot = this.operations();
    if (!snapshot?.schoolDay || snapshot.date !== this.schoolClock().date) return null;
    const seconds = this.schoolClock().seconds;
    return snapshot.schoolDay.slots.find(slot => this.seconds(slot.startsAt) <= seconds && seconds < this.seconds(slot.endsAt)) ?? null;
  });
  readonly remaining = computed(() => {
    const slot = this.currentSlot();
    if (!slot) return '—';
    const seconds = Math.max(0, this.seconds(slot.endsAt) - this.schoolClock().seconds);
    return `${Math.floor(seconds / 60).toString().padStart(2, '0')}:${(seconds % 60).toString().padStart(2, '0')}`;
  });
  readonly slotProgress = computed(() => {
    const slot = this.currentSlot();
    return slot ? 100 * (this.schoolClock().seconds - this.seconds(slot.startsAt)) / Math.max(1, this.seconds(slot.endsAt) - this.seconds(slot.startsAt)) : 0;
  });
  readonly chartOptions: ChartOptions<'line'> = {
    responsive: true, maintainAspectRatio: false,
    animation: typeof matchMedia !== 'undefined' && matchMedia('(prefers-reduced-motion: reduce)').matches ? false : { duration: 400 },
    plugins: { legend: { display: false }, tooltip: { rtl: true } },
    scales: {
      x: { grid: { display: false }, ticks: { maxTicksLimit: 5, font: { family: 'Tajawal', size: 10 }, color: '#67756D' } },
      y: { min: 0, max: 100, grid: { color: '#E2E5DF' }, ticks: { stepSize: 25, callback: value => `${value}%`, font: { family: 'Tajawal', size: 10 }, color: '#67756D' } }
    }
  };
  constructor() {
    interval(1000).pipe(takeUntilDestroyed(inject(DestroyRef))).subscribe(() => this.now.set(Date.now()));
  }
  t(ar: string, en: string): string { return this.translate.currentLang === 'en' ? en : ar; }
  get locale(): string { return this.translate.currentLang === 'en' ? 'en-GB' : 'ar-SA-u-ca-gregory-nu-latn'; }
  selectZone(id: string): void {
    this.selectedZone.set(id);
    const target = this.zones.find(zone => zone.id === id)?.target;
    if (target) document.getElementById(target)?.scrollIntoView({ behavior: matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth', block: 'start' });
  }
  time(date: string): string {
    return new Intl.DateTimeFormat(this.locale, { timeZone: this.operations()?.timeZone ?? 'Asia/Riyadh', hour: '2-digit', minute: '2-digit' }).format(new Date(date));
  }
  date(date: string): string {
    return new Intl.DateTimeFormat(this.locale, { timeZone: this.operations()?.timeZone ?? 'Asia/Riyadh', day: 'numeric', month: 'short' }).format(new Date(date));
  }
  breakdown(counts: AttendanceCounts) {
    return [
      { label: this.t('حاضر', 'Present'), value: counts.present, tone: 'green' },
      { label: this.t('غائب بدون عذر', 'Absent'), value: counts.absent, tone: 'danger' },
      { label: this.t('غائب بعذر', 'Excused'), value: counts.excused, tone: 'blue' },
      { label: this.t('لم يُرصد', 'Unrecorded'), value: counts.unrecorded, tone: 'muted' }
    ];
  }
  share(value: number, total: number): number { return total ? 100 * value / total : 0; }
  roleLabel(role: string): string {
    return ({ Instructor: this.t('معلم', 'Teacher'), Moderator: this.t('مشرف', 'Supervisor'), SchoolManager: this.t('مدير المدرسة', 'School manager') } as Record<string, string>)[role] ?? role;
  }
  activityLabel(category: string): string {
    return ({ visits: this.t('الزيارات', 'Visits'), students: this.t('الطلاب', 'Students'), staff: this.t('الموظفون', 'Staff'), summons: this.t('الاستدعاءات', 'Summons'), timetable: this.t('الجدول', 'Timetable') } as Record<string, string>)[category] ?? category;
  }
  visitStatus(status: number): string {
    return ({ 1: this.t('مسودة', 'Draft'), 2: this.t('مرسلة', 'Submitted'), 3: this.t('بانتظار الاعتماد', 'Pending approval'), 4: this.t('معتمدة', 'Approved'), 5: this.t('معادة للتعديل', 'Returned'), 6: this.t('أعيد فتحها', 'Reopened'), 7: this.t('قيد المراجعة', 'Under review'), 8: this.t('ملغاة', 'Cancelled') } as Record<number, string>)[status] ?? '—';
  }
  canOpenStaff(): boolean { return this.auth.hasPermission('Attendance.Manage'); }
  canOpenDuty(): boolean { return this.auth.hasPermission('Duty.Manage'); }
  canOpenSummons(): boolean { return this.auth.roles().includes('SocialWorker') && this.auth.hasPermission('Summon.View'); }
  private seconds(time: string): number {
    const parts = time.split(':').map(Number);
    return (parts[0] ?? 0) * 3600 + (parts[1] ?? 0) * 60 + (parts[2] ?? 0);
  }
  private trendChart(trend: AttendanceTrend[]): ChartData<'line'> | null {
    return trend.length > 1 ? {
      labels: trend.map(point => this.date(point.date + 'T12:00:00Z')),
      datasets: [{ label: this.t('الحضور من السجلات المرصودة (%)', 'Attendance among recorded entries (%)'),
        data: trend.map(point => point.recorded ? 100 * point.present / point.recorded : 0),
        borderColor: '#1E8E4E', backgroundColor: 'rgba(30,142,78,.09)', fill: true, tension: .3, borderWidth: 2, pointRadius: 2 }]
    } : null;
  }
}
