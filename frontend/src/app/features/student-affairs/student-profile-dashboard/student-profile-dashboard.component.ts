import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { ChartModule } from 'primeng/chart';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TagModule } from 'primeng/tag';
import { TimelineModule } from 'primeng/timeline';
import { TooltipModule } from 'primeng/tooltip';
import { finalize } from 'rxjs';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import { StudentAnalyticsEventDto, StudentAnalyticsProfileDto } from '../../../core/models/daily-operations.models';
import { DailyOperationsService } from '../../../core/services/daily-operations.service';

type TimelineFilter = 'ALL' | 'Absence' | 'Delay' | 'Excuse' | 'Referral' | 'Behavior' | 'Recognition';

@Component({
  selector: 'app-student-profile-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    ButtonModule,
    CardModule,
    ChartModule,
    ProgressSpinnerModule,
    TagModule,
    TimelineModule,
    TooltipModule
  ],
  templateUrl: './student-profile-dashboard.component.html',
  styleUrl: './student-profile-dashboard.component.css'
})
export class StudentProfileDashboardComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(DailyOperationsService);

  readonly studentId = signal<number | null>(null);
  readonly profile = signal<StudentAnalyticsProfileDto | null>(null);
  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly activeTimelineFilter = signal<TimelineFilter>('ALL');

  readonly monthlyBarChartData = computed(() => {
    const p = this.profile();
    if (!p || !p.monthlyTrends || p.monthlyTrends.length === 0) {
      return {
        labels: [],
        datasets: []
      };
    }

    const labels = p.monthlyTrends.map(m => m.monthLabel);
    const absences = p.monthlyTrends.map(m => m.absences);
    const delays = p.monthlyTrends.map(m => m.delays);
    const excuses = p.monthlyTrends.map(m => m.excuses);

    return {
      labels,
      datasets: [
        {
          label: 'أيام الغياب',
          backgroundColor: '#ef4444',
          borderColor: '#dc2626',
          borderRadius: 6,
          data: absences
        },
        {
          label: 'حالات التأخر',
          backgroundColor: '#f59e0b',
          borderColor: '#d97706',
          borderRadius: 6,
          data: delays
        },
        {
          label: 'الأعذار المقبولة',
          backgroundColor: '#10b981',
          borderColor: '#059669',
          borderRadius: 6,
          data: excuses
        }
      ]
    };
  });

  readonly monthlyBarChartOptions = {
    maintainAspectRatio: false,
    aspectRatio: 0.8,
    plugins: {
      legend: {
        position: 'top',
        labels: {
          font: {
            family: 'inherit',
            weight: '600'
          },
          usePointStyle: true,
          padding: 16
        }
      },
      tooltip: {
        rtl: true,
        textDirection: 'rtl',
        padding: 12,
        cornerRadius: 8
      }
    },
    scales: {
      x: {
        grid: {
          display: false
        },
        ticks: {
          font: {
            family: 'inherit'
          }
        }
      },
      y: {
        beginAtZero: true,
        ticks: {
          stepSize: 1,
          font: {
            family: 'inherit'
          }
        },
        grid: {
          color: 'rgba(0, 0, 0, 0.05)'
        }
      }
    }
  };

  readonly distributionDoughnutData = computed(() => {
    const p = this.profile();
    if (!p) {
      return { labels: [], datasets: [] };
    }

    const absences = p.totalAbsences;
    const delays = p.totalDelays;
    const excuses = p.totalExcuses;

    return {
      labels: ['أيام الغياب', 'حالات التأخر', 'الأعذار المقدمة'],
      datasets: [
        {
          data: [absences, delays, excuses],
          backgroundColor: ['#ef4444', '#f59e0b', '#10b981'],
          hoverBackgroundColor: ['#dc2626', '#d97706', '#059669'],
          borderWidth: 2
        }
      ]
    };
  });

  readonly distributionDoughnutOptions = {
    maintainAspectRatio: false,
    aspectRatio: 1,
    cutout: '65%',
    plugins: {
      legend: {
        position: 'bottom',
        labels: {
          font: {
            family: 'inherit',
            weight: '600'
          },
          usePointStyle: true,
          padding: 14
        }
      },
      tooltip: {
        rtl: true,
        textDirection: 'rtl',
        padding: 12,
        cornerRadius: 8
      }
    }
  };

  readonly filteredEvents = computed(() => {
    const p = this.profile();
    if (!p || !p.recentEvents) return [];

    const filter = this.activeTimelineFilter();
    if (filter === 'ALL') return p.recentEvents;

    return p.recentEvents.filter(e => e.eventType === filter);
  });

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const idStr = params.get('id');
      const id = idStr ? parseInt(idStr, 10) : null;
      if (id && !isNaN(id)) {
        this.studentId.set(id);
        this.loadProfile(id);
      } else {
        this.errorMessage.set('معرّف الطالب غير صحيح');
        this.loading.set(false);
      }
    });
  }

  loadProfile(id: number): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.api.getStudentAnalyticsProfile(id)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: res => {
          if (res.isSuccess && res.data) {
            this.profile.set(res.data);
          } else {
            this.errorMessage.set(res.message || 'تعذر تحميل الملف التحليلي للطالب');
          }
        },
        error: err => {
          this.errorMessage.set(extractHttpErrorMessage(err) ?? 'حدث خطأ أثناء استرداد الملف التحليلي للطالب');
        }
      });
  }

  setTimelineFilter(filter: TimelineFilter): void {
    this.activeTimelineFilter.set(filter);
  }

  goBack(): void {
    this.router.navigate(['/student-affairs/records']);
  }

  printDashboard(): void {
    window.print();
  }

  getEventIcon(event: StudentAnalyticsEventDto): string {
    if (event.icon) return event.icon;
    switch (event.eventType) {
      case 'Absence': return 'pi pi-calendar-times';
      case 'Delay': return 'pi pi-clock';
      case 'Excuse': return 'pi pi-file-check';
      case 'Referral': return 'pi pi-briefcase';
      case 'Behavior': return 'pi pi-exclamation-triangle';
      case 'Recognition': return 'pi pi-star-fill';
      case 'GatePass': return 'pi pi-sign-out';
      default: return 'pi pi-info-circle';
    }
  }

  getEventColor(event: StudentAnalyticsEventDto): string {
    switch (event.severity) {
      case 'danger': return '#ef4444';
      case 'warning': return '#f59e0b';
      case 'success': return '#10b981';
      case 'info': return '#3b82f6';
      default: return '#64748b';
    }
  }
}
