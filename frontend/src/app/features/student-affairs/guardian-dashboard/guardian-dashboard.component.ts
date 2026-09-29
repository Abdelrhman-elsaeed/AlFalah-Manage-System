import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TagModule } from 'primeng/tag';
import { DashboardCountDto, GuardianDashboardStudentDto } from '../../../core/models/student-affairs-dashboard.models';
import { StudentAffairsDashboardService } from '../../../core/services/student-affairs-dashboard.service';

@Component({
  selector: 'app-guardian-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, ButtonModule, CardModule, ProgressSpinnerModule, TagModule],
  templateUrl: './guardian-dashboard.component.html',
  styleUrl: './guardian-dashboard.component.css'
})
export class GuardianDashboardComponent {
  private readonly api = inject(StudentAffairsDashboardService);
  readonly loading = signal(true);
  readonly errorMessage = signal('');
  readonly cards = signal<readonly GuardianDashboardStudentDto[]>([]);
  readonly actions = signal<readonly DashboardCountDto[]>([]);
  readonly unreadNotifications = signal(0);
  readonly unreadThreads = signal(0);

  constructor() { this.load(); }

  load(): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.api.getGuardianDashboard().subscribe({
      next: response => {
        this.loading.set(false);
        if (!response.isSuccess || !response.data) {
          this.errorMessage.set(response.errors[0] ?? response.message ?? 'تعذر تحميل بيانات الأبناء.');
          return;
        }
        this.cards.set(response.data.students);
        this.actions.set(response.data.actions);
        this.unreadNotifications.set(response.data.unreadNotifications);
        this.unreadThreads.set(response.data.unreadThreads);
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.errorMessage.set(error.status === 403
          ? 'لا تملك صلاحية عرض الطلاب المرتبطين بهذا الحساب.'
          : 'تعذر تحميل بيانات الأبناء. حاول مرة أخرى.');
      }
    });
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
