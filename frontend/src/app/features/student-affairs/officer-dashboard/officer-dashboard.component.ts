import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
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
  imports: [CommonModule, ButtonModule, CardModule, ProgressSpinnerModule, TableModule, TagModule],
  templateUrl: './officer-dashboard.component.html',
  styleUrl: './officer-dashboard.component.css'
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

  countSeverity(count: DashboardCountDto): 'success' | 'info' | 'warning' | 'danger' {
    const value = count.severity.toLocaleLowerCase('en');
    if (value.includes('critical') || value.includes('danger') || value.includes('high')) return 'danger';
    if (value.includes('warn') || value.includes('medium')) return 'warning';
    if (value.includes('success') || value.includes('low')) return 'success';
    return 'info';
  }

  formatGeneratedAt(value: string): string {
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('ar-SA', { dateStyle: 'medium', timeStyle: 'short' }).format(date);
  }
}
