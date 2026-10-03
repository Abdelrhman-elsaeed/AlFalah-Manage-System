import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize, fromEvent, interval } from 'rxjs';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import { GuardianDashboardStudentDto, GuardianStudentAffairsDashboardDto } from '../../../core/models/student-affairs-dashboard.models';
import { AuthService } from '../../../core/services/auth.service';
import { StudentAffairsDashboardService } from '../../../core/services/student-affairs-dashboard.service';

const SERVICES = [
  { code: 'UnreadThreads', label: 'الرسائل', description: 'تواصل مع المدرسة وتابع الردود', icon: 'pi-comments', route: '/student-affairs/messages', permissions: ['Messaging.ViewOwn'], tone: 'new' },
  { code: 'UnreadNotifications', label: 'الإشعارات', description: 'آخر التنبيهات والتحديثات من المدرسة', icon: 'pi-bell', route: '/student-affairs/guardian/notifications', permissions: ['Notification.ViewOwn'], tone: 'new' },
  { code: 'UpcomingSummons', label: 'الاستدعاءات', description: 'راجع المواعيد وتعليمات الحضور', icon: 'pi-calendar', route: '/student-affairs/guardian/activity', permissions: ['Guardian.ViewLinkedStudents', 'ClassroomEntryPermit.View'], tone: 'attention' },
  { code: 'PendingExcuses', label: 'أعذار الغياب', description: 'ارفع العذر وتابع قرار المراجعة', icon: 'pi-file-edit', route: '/student-affairs/guardian/excuses', permissions: ['Attendance.SubmitExcuse'], tone: 'pending' },
  { code: 'ActiveGatePasses', label: 'استئذانات الخروج', description: 'قدّم الطلب وتابع حالة الاعتماد', icon: 'pi-send', route: '/student-affairs/gate-passes/mine/new', permissions: ['GatePass.Request'], tone: 'pending' },
  { code: 'ActiveEntryPermits', label: 'تصاريح دخول الفصل', description: 'راجع التصاريح النشطة وصلاحيتها', icon: 'pi-id-card', route: '/student-affairs/guardian/activity', permissions: ['Guardian.ViewLinkedStudents', 'ClassroomEntryPermit.View'], tone: 'active' }
] as const;

@Component({
  selector: 'app-guardian-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './guardian-dashboard.component.html',
  styleUrls: ['../manager-workspace.css', './guardian-dashboard.component.css']
})
export class GuardianDashboardComponent {
  private readonly api = inject(StudentAffairsDashboardService);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  readonly view = inject(ActivatedRoute).snapshot.data['guardianView'] === 'overview' ? 'overview' : 'children';
  readonly loading = signal(true);
  readonly refreshing = signal(false);
  readonly errorMessage = signal('');
  readonly dashboard = signal<GuardianStudentAffairsDashboardDto | null>(null);
  readonly search = signal('');
  readonly attentionOnly = signal(false);
  readonly expandedStudentIds = signal<ReadonlySet<number>>(new Set());
  readonly cards = computed(() => this.dashboard()?.students ?? []);
  readonly pendingFollowUps = computed(() => this.cards().reduce((total, card) =>
    total + card.attendance.pendingExcuses + card.activeGatePasses + card.pendingOrUpcomingSummons, 0));
  readonly studentsNeedingAttention = computed(() => this.cards().filter(card => this.needsAttention(card)).length);
  readonly filteredCards = computed(() => {
    const query = this.search().trim().replace(/\s+/g, ' ').toLocaleLowerCase();
    return this.cards().filter(card => (!this.attentionOnly() || this.needsAttention(card)) &&
      `${card.context.student.displayName} ${card.context.student.studentNumber} ${this.classLabel(card)}`.replace(/\s+/g, ' ').toLocaleLowerCase().includes(query));
  });
  readonly previewStudents = computed(() => [...this.cards()]
    .sort((a, b) => Number(this.needsAttention(b)) - Number(this.needsAttention(a))).slice(0, 4));
  readonly services = computed(() => SERVICES.filter(service => this.auth.hasAllPermissions(service.permissions))
    .map(service => ({ ...service, count: this.dashboard()?.actions.find(action => action.code === service.code)?.count ?? 0 }))
    .sort((a, b) => Number(b.count > 0) - Number(a.count > 0)));

  constructor() {
    this.load();
    interval(60_000).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      if (!document.hidden) this.load();
    });
    fromEvent(document, 'visibilitychange').pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      if (!document.hidden) this.load();
    });
  }

  load(): void {
    if (this.refreshing()) return;
    this.refreshing.set(true);
    this.errorMessage.set('');
    this.api.getGuardianDashboard().pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => { this.loading.set(false); this.refreshing.set(false); })
    ).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) {
          this.errorMessage.set(response.errors[0] ?? response.message ?? 'تعذر تحميل بيانات الأبناء.');
          return;
        }
        this.dashboard.set(response.data);
      },
      error: (error: HttpErrorResponse) => {
        this.errorMessage.set(error.status === 403
          ? 'لا تملك صلاحية عرض الطلاب المرتبطين بهذا الحساب.'
          : extractHttpErrorMessage(error) ?? 'تعذر تحديث البيانات. حاول مرة أخرى.');
      }
    });
  }

  toggleDetails(studentId: number): void {
    const expanded = new Set(this.expandedStudentIds());
    expanded.has(studentId) ? expanded.delete(studentId) : expanded.add(studentId);
    this.expandedStudentIds.set(expanded);
  }

  needsAttention(card: GuardianDashboardStudentDto): boolean {
    return card.attendance.pendingExcuses + card.pendingOrUpcomingSummons + card.unreadNotifications + card.unreadThreads > 0;
  }

  classLabel(card: GuardianDashboardStudentDto): string {
    return card.context.student.classLabel || card.context.classroom?.label || 'لم يُحدد الفصل';
  }

  serviceState(code: string, count: number): string {
    if (!count) return 'لا توجد عناصر حاليًا';
    if (code.startsWith('Unread')) return 'جديد';
    if (code === 'UpcomingSummons') return 'بانتظار الحضور';
    if (code === 'PendingExcuses') return 'قيد المراجعة';
    return 'نشط';
  }

  canUse(permission: string): boolean {
    return this.auth.hasPermission(permission);
  }

  trackStudent(_: number, card: GuardianDashboardStudentDto): number {
    return card.context.student.id;
  }

  initials(name: string): string {
    return name.split(/\s+/).filter(Boolean).slice(0, 2).map(part => part[0]).join('');
  }
}
