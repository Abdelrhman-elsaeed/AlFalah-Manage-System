import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { InstructorDashboard } from '../../../core/models/dashboard.models';
import { TeacherTopPriorityDto } from '../../../core/models/student-affairs-dashboard.models';
import { DashboardService } from '../../../core/services/dashboard.service';
import { StudentAffairsDashboardService } from '../../../core/services/student-affairs-dashboard.service';
import { formatPublishedScore } from '../../../shared/score-scale';
import {
  currentLessonReasonLabel,
  teacherAlertLabel
} from '../../../core/utils/current-lesson-labels';

interface TeacherShortcut {
  readonly label: string;
  readonly description: string;
  readonly icon: string;
  readonly route: string;
  readonly tone: 'green' | 'gold' | 'blue' | 'purple' | 'red' | 'slate';
}

@Component({
  selector: 'app-instructor-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './instructor-dashboard.component.html',
  styleUrls: [
    '../../student-affairs/manager-workspace.css',
    './instructor-dashboard.component.css'
  ]
})
export class InstructorDashboardComponent {
  private readonly studentAffairs = inject(StudentAffairsDashboardService);
  private readonly dashboards = inject(DashboardService);

  readonly loading = signal(true);
  readonly errorMessage = signal('');
  readonly priority = signal<TeacherTopPriorityDto | null>(null);
  readonly performance = signal<InstructorDashboard | null>(null);

  readonly pendingTotal = computed(() => {
    const value = this.priority();
    return value ? value.pendingGatePassAcknowledgements + value.pendingEntryPermitAcknowledgements : 0;
  });

  readonly attentionCount = computed(() => this.pendingTotal() + (this.priority()?.alerts.length ?? 0));

  readonly shortcuts: readonly TeacherShortcut[] = [
    { label: 'إدارة الفصل', description: 'الاستئذانات والتصاريح وفصولي', icon: 'pi-users', route: '/student-affairs/teacher', tone: 'green' },
    { label: 'الجدول المدرسي', description: 'جدولي وجدول المدرسة الكامل', icon: 'pi-calendar', route: '/timetable', tone: 'gold' },
    { label: 'الرسائل', description: 'محادثات أولياء الأمور والفريق', icon: 'pi-comments', route: '/student-affairs/messages', tone: 'blue' },
    { label: 'تقاريري', description: 'الزيارات والتقييمات المعتمدة', icon: 'pi-chart-line', route: '/instructor/reports', tone: 'purple' },
    { label: 'ملفات الإنجاز', description: 'الأدلة والملفات المهنية', icon: 'pi-folder-open', route: '/instructor/evidence-files', tone: 'red' },
    { label: 'الساعات المكتبية', description: 'إدارة أوقات التواصل المتاحة', icon: 'pi-clock', route: '/student-affairs/office-hours', tone: 'slate' }
  ];

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    forkJoin({
      priority: this.studentAffairs.getTeacherTopPriority(),
      performance: this.dashboards.getInstructor()
    }).subscribe({
      next: ({ priority, performance }) => {
        this.loading.set(false);
        if (!priority.isSuccess || !priority.data || !performance.isSuccess || !performance.data) {
          this.errorMessage.set(
            priority.errors[0]
            ?? performance.errors[0]
            ?? priority.message
            ?? performance.message
            ?? 'تعذر تحميل لوحة المعلم.');
          return;
        }
        this.priority.set(priority.data);
        this.performance.set(performance.data);
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.errorMessage.set(error.status === 403
          ? 'لا تملك صلاحية عرض لوحة المعلم في المدرسة النشطة.'
          : 'تعذر تحميل لوحة المعلم. حاول مرة أخرى.');
      }
    });
  }

  latestScore(): string {
    return formatPublishedScore(this.performance()?.latestEvaluation?.overallScore ?? null);
  }

  lessonTime(): string {
    const period = this.priority()?.context.currentPeriod;
    if (!period) return 'لا توجد حصة نشطة الآن';
    const format = (value: string) => new Intl.DateTimeFormat('ar-SA', {
      hour: '2-digit',
      minute: '2-digit',
      timeZone: this.priority()?.context.schoolTimeZone
    }).format(new Date(value));
    return `${format(period.startsAt)} – ${format(period.endsAt)}`;
  }

  formatDate(value: string): string {
    const date = new Date(value);
    return Number.isNaN(date.getTime())
      ? value
      : new Intl.DateTimeFormat('ar-SA', { dateStyle: 'medium' }).format(date);
  }

  shortcutBadge(shortcut: TeacherShortcut): number {
    return shortcut.route === '/student-affairs/teacher' ? this.pendingTotal() : 0;
  }

  lessonReason(context: TeacherTopPriorityDto['context']): string {
    return currentLessonReasonLabel(context.resolutionKind, context.resolutionReason);
  }

  alertLabel(alert: string, context: TeacherTopPriorityDto['context']): string {
    return teacherAlertLabel(alert, context.resolutionKind, context.resolutionReason);
  }
}
