import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ButtonModule } from 'primeng/button';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { Subject, catchError, filter, fromEvent, map, merge, of, switchMap, tap, timer } from 'rxjs';
import { SecurityGatePassQueueItemDto, SecurityStudentAffairsDashboardDto } from '../../../core/models/student-affairs-dashboard.models';
import { StudentAffairsDashboardService } from '../../../core/services/student-affairs-dashboard.service';

type QueueResult =
  | { readonly ok: true; readonly value: SecurityStudentAffairsDashboardDto }
  | { readonly ok: false; readonly error: HttpErrorResponse };

@Component({
  selector: 'app-security-gate-queue',
  standalone: true,
  imports: [CommonModule, ButtonModule, ProgressSpinnerModule, TableModule, TagModule],
  templateUrl: './security-gate-queue.component.html',
  styleUrl: './security-gate-queue.component.css'
})
export class SecurityGateQueueComponent implements OnInit {
  private readonly api = inject(StudentAffairsDashboardService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly manualRefresh = new Subject<void>();

  readonly loading = signal(true);
  readonly dashboard = signal<SecurityStudentAffairsDashboardDto | null>(null);
  readonly errorMessage = signal('');

  ngOnInit(): void {
    const focus$ = typeof window === 'undefined' ? of(undefined) : fromEvent(window, 'focus');
    const visibility$ = typeof document === 'undefined' ? of(undefined) : fromEvent(document, 'visibilitychange');

    merge(timer(0, 30_000), focus$, visibility$, this.manualRefresh).pipe(
      filter(() => typeof document === 'undefined' || document.visibilityState === 'visible'),
      tap(() => {
        this.loading.set(true);
        this.errorMessage.set('');
        this.dashboard.set(null);
      }),
      switchMap(() => this.api.getSecurityDashboard().pipe(
        map(response => response.isSuccess && response.data
          ? ({ ok: true, value: response.data } as const)
          : ({ ok: false, error: new HttpErrorResponse({ status: 200, error: response }) } as const)),
        catchError((error: HttpErrorResponse) => of({ ok: false, error } as const))
      )),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(result => this.applyResult(result));
  }

  refresh(): void {
    this.manualRefresh.next();
  }

  sortedPasses(): readonly SecurityGatePassQueueItemDto[] {
    return [...(this.dashboard()?.approvedGatePasses ?? [])]
      .sort((left, right) => new Date(left.approvedWindowStartsAt).getTime() - new Date(right.approvedWindowStartsAt).getTime());
  }

  windowLabel(item: SecurityGatePassQueueItemDto): string {
    const now = Date.now();
    const starts = new Date(item.approvedWindowStartsAt).getTime();
    const ends = new Date(item.approvedWindowEndsAt).getTime();
    if (now > ends) return 'انتهت المهلة';
    if (now >= starts) return 'الآن';
    return 'قريبًا';
  }

  windowSeverity(item: SecurityGatePassQueueItemDto): 'success' | 'warning' | 'danger' {
    const label = this.windowLabel(item);
    return label === 'الآن' ? 'success' : label === 'قريبًا' ? 'warning' : 'danger';
  }

  countSeverity(severity: string): 'success' | 'info' | 'warning' | 'danger' {
    const value = severity.toLocaleLowerCase('en');
    if (value.includes('critical') || value.includes('danger') || value.includes('high')) return 'danger';
    if (value.includes('warn') || value.includes('medium')) return 'warning';
    if (value.includes('success') || value.includes('low')) return 'success';
    return 'info';
  }

  formatDateTime(value: string): string {
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('ar-SA', { dateStyle: 'short', timeStyle: 'short' }).format(date);
  }

  initials(name: string): string {
    return name.split(/\s+/).filter(Boolean).slice(0, 2).map(part => part[0]).join('');
  }

  private applyResult(result: QueueResult): void {
    this.loading.set(false);
    if (result.ok) {
      this.dashboard.set(result.value);
      return;
    }
    const response = result.error.error as { message?: unknown; errors?: unknown } | null;
    const apiMessage = typeof response?.message === 'string' ? response.message : '';
    this.errorMessage.set(result.error.status === 403
      ? 'لا تملك صلاحية عرض قائمة بوابة المدرسة.'
      : apiMessage || 'تعذر تحميل قائمة الاستئذانات المعتمدة.');
  }
}
