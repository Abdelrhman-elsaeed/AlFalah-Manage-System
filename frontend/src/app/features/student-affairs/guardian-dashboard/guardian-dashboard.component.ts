import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TagModule } from 'primeng/tag';
import { forkJoin } from 'rxjs';
import {
  DashboardCountDto,
  GuardianStudentCard,
  GuardianStudentDto,
  GuardianStudentSummaryDto,
  MetricBadgeDto,
  StudentTermMetricCode
} from '../../../core/models/student-affairs-dashboard.models';
import { StudentAffairsDashboardService } from '../../../core/services/student-affairs-dashboard.service';

@Component({
  selector: 'app-guardian-dashboard',
  standalone: true,
  imports: [CommonModule, ButtonModule, CardModule, ProgressSpinnerModule, TagModule],
  templateUrl: './guardian-dashboard.component.html',
  styleUrl: './guardian-dashboard.component.css'
})
export class GuardianDashboardComponent {
  private readonly api = inject(StudentAffairsDashboardService);

  readonly loading = signal(true);
  readonly errorMessage = signal('');
  readonly cards = signal<readonly GuardianStudentCard[]>([]);
  readonly actions = signal<readonly DashboardCountDto[]>([]);
  readonly expandedStudentId = signal<number | null>(null);
  readonly summaryLoading = signal(false);
  readonly summaries = signal<ReadonlyMap<number, GuardianStudentSummaryDto>>(new Map());

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.cards.set([]);
    this.actions.set([]);
    this.expandedStudentId.set(null);
    this.summaries.set(new Map());

    forkJoin({ dashboard: this.api.getGuardianDashboard(), links: this.api.getGuardianStudents() }).subscribe({
      next: ({ dashboard, links }) => {
        this.loading.set(false);
        if (!dashboard.isSuccess || !dashboard.data || !links.isSuccess || !links.data) {
          this.errorMessage.set(dashboard.errors[0] ?? links.errors[0] ?? dashboard.message ?? links.message ?? 'تعذر تحميل بيانات الأبناء.');
          return;
        }
        const linkByStudentId = new Map<number, GuardianStudentDto>(links.data.map(link => [link.student.id, link]));
        this.cards.set(dashboard.data.students
          .filter(context => linkByStudentId.has(context.student.id))
          .map(context => {
            const link = linkByStudentId.get(context.student.id)!;
            return {
              context,
              canSubmitExcuses: link.canSubmitExcuses,
              canRequestGatePass: link.canRequestGatePass,
              receivesNotifications: link.receivesNotifications
            };
          }));
        this.actions.set(dashboard.data.actions);
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.errorMessage.set(error.status === 403
          ? 'لا تملك صلاحية عرض الطلاب المرتبطين بهذا الحساب.'
          : 'تعذر تحميل بيانات الأبناء. حاول مرة أخرى.');
      }
    });
  }

  toggleSummary(card: GuardianStudentCard): void {
    const studentId = card.context.student.id;
    if (this.expandedStudentId() === studentId) {
      this.expandedStudentId.set(null);
      return;
    }
    this.expandedStudentId.set(studentId);
    if (this.summaries().has(studentId)) return;

    this.summaryLoading.set(true);
    this.api.getGuardianStudentSummary(studentId).subscribe({
      next: response => {
        this.summaryLoading.set(false);
        if (!response.isSuccess || !response.data) {
          this.errorMessage.set(response.errors[0] ?? response.message ?? 'تعذر تحميل ملخص الطالب.');
          return;
        }
        const next = new Map(this.summaries());
        next.set(studentId, response.data);
        this.summaries.set(next);
      },
      error: (error: HttpErrorResponse) => {
        this.summaryLoading.set(false);
        if (error.status === 403 || error.status === 404) {
          this.load();
          return;
        }
        this.errorMessage.set('تعذر تحميل ملخص الطالب.');
      }
    });
  }

  summaryFor(studentId: number): GuardianStudentSummaryDto | null {
    return this.summaries().get(studentId) ?? null;
  }

  metricLabel(code: StudentTermMetricCode): string {
    const labels: Record<StudentTermMetricCode, string> = {
      MorningArrivalDelay: 'التأخر الصباحي',
      PenaltyAbsenceDay: 'أيام الغياب المحتسبة',
      SessionDelay: 'التأخر عن الحصص',
      AcademicConcern: 'الملاحظات الأكاديمية',
      CountableBehaviorIncident: 'المخالفات السلوكية المحتسبة',
      ClassroomEntryPermit: 'تصاريح دخول الفصل'
    };
    return labels[code];
  }

  severity(metric: MetricBadgeDto): 'success' | 'info' | 'warning' | 'danger' {
    const value = metric.severity.toLocaleLowerCase('en');
    if (value.includes('critical') || value.includes('high') || value.includes('danger')) return 'danger';
    if (value.includes('medium') || value.includes('warn')) return 'warning';
    if (value.includes('low') || value.includes('success')) return 'success';
    return 'info';
  }

  actionSeverity(action: DashboardCountDto): 'success' | 'info' | 'warning' | 'danger' {
    const value = action.severity.toLocaleLowerCase('en');
    if (value.includes('critical') || value.includes('high')) return 'danger';
    if (value.includes('medium') || value.includes('warn')) return 'warning';
    if (value.includes('low')) return 'success';
    return 'info';
  }

  initials(name: string): string {
    return name.split(/\s+/).filter(Boolean).slice(0, 2).map(part => part[0]).join('');
  }
}
