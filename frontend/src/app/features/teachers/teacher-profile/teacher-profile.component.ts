import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { ChipsModule } from 'primeng/chips';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { ChartModule } from 'primeng/chart';
import { ToastService } from '../../../core/services/toast.service';
import { TeachersService } from '../../../core/services/teachers.service';
import { AuthService } from '../../../core/services/auth.service';
import {
  TeacherProfile,
  TeacherProgress,
  TeacherVisitProgress,
  TeacherVisitSummary
} from '../../../core/models/teacher.models';

@Component({
  selector: 'app-teacher-profile',
  standalone: true,
  imports: [
    CommonModule, FormsModule, TranslateModule,
    ButtonModule, ChipsModule, TableModule, TagModule, TooltipModule, ChartModule
  ],
  templateUrl: './teacher-profile.component.html',
  styleUrls: ['./teacher-profile.component.css']
})
export class TeacherProfileComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly teachersService = inject(TeachersService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);

  readonly userId = signal<string | null>(null);
  readonly profile = signal<TeacherProfile | null>(null);
  readonly visits = signal<TeacherVisitSummary[]>([]);
  readonly progress = signal<TeacherProgress | null>(null);
  readonly loading = signal(false);
  readonly editingClasses = signal(false);
  readonly savingClasses = signal(false);
  readonly editableClasses = signal<string[]>([]);

  // The radar chart payload — built reactively from `progress()`.
  readonly radarData = computed(() => {
    const p = this.progress();
    if (!p || p.axisLabels.length === 0 || p.visits.length === 0) return null;

    const labels = p.axisLabels.map(a => a.domainNameAr || a.domainCode);

    // Saudi-theme-friendly palette: brand-green primary, then 4 distinct
    // hues that work on the light theme without clashing.
    const palette = [
      '#1E8E4E', // brand green
      '#0F7132', // deep brand
      '#D4AF37', // gold
      '#0EA5E9', // sky
      '#A855F7', // violet
      '#F97316'  // orange
    ];

    const datasets = p.visits.map((v, idx) => {
      const color = palette[idx % palette.length];
      return {
        label: v.legendLabel,
        data: v.domainAverages.map(d => Number(d.averageScore)),
        borderColor: color,
        backgroundColor: hexToRgba(color, 0.18),
        borderWidth: 2,
        pointBackgroundColor: color,
        pointBorderColor: color,
        pointRadius: 3,
        pointHoverRadius: 5
      };
    });

    return { labels, datasets };
  });

  readonly radarOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        position: 'bottom',
        rtl: true,
        textDirection: 'rtl',
        labels: { color: '#0F172A', font: { family: 'Tajawal, Cairo, sans-serif', size: 12 } }
      },
      tooltip: {
        rtl: true,
        textDirection: 'rtl'
      }
    },
    scales: {
      r: {
        angleLines: { color: 'rgba(15, 23, 42, 0.12)' },
        grid: { color: 'rgba(15, 23, 42, 0.10)' },
        pointLabels: {
          color: '#0F172A',
          font: { family: 'Tajawal, Cairo, sans-serif', size: 12, weight: '600' as any }
        },
        ticks: {
          stepSize: 1,
          backdropColor: 'transparent',
          color: 'rgba(15, 23, 42, 0.55)',
          font: { size: 10 }
        },
        suggestedMin: 0,
        suggestedMax: 4
      }
    }
  };

  // "+ زيارة جديدة" is for Moderator + School Manager only.
  readonly canCreateVisit = computed(() =>
    this.auth.hasPermission('Visit.Create')
    && (this.auth.hasRole('Moderator') || this.auth.hasRole('SchoolManager') || this.auth.hasRole('SuperAdmin'))
  );

  readonly visitCount = computed(() => this.visits().length);
  readonly canManageTeaching = computed(() =>
    this.auth.hasPermission('User.Edit')
    && !this.auth.hasRole('MainManager')
    && !this.auth.hasRole('Moderator'));

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('userId');
    if (!id) {
      this.router.navigate(['/teachers']);
      return;
    }
    this.userId.set(id);
    this.loadAll(id);
  }

  loadAll(userId: string): void {
    this.loading.set(true);

    this.teachersService.getProfile(userId).subscribe({
      next: (resp) => {
        if (resp.isSuccess && resp.data) {
          this.profile.set(resp.data);
        } else {
          this.toast.error('TEACHERS.PROFILE_LOAD_FAILED', resp.message || '');
        }
      }
    });

    this.teachersService.getVisits(userId).subscribe({
      next: (resp) => {
        if (resp.isSuccess && resp.data) {
          this.visits.set(resp.data);
        } else {
          this.toast.error('TEACHERS.VISITS_LOAD_FAILED', resp.message || '');
        }
      }
    });

    this.teachersService.getProgress(userId).subscribe({
      next: (resp) => {
        if (resp.isSuccess && resp.data) {
          this.progress.set(resp.data);
        } else {
          this.toast.error('TEACHERS.PROGRESS_LOAD_FAILED', resp.message || '');
        }
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  goBack(): void { this.router.navigate(['/teachers']); }

  beginClassesEdit(): void {
    this.editableClasses.set([...(this.profile()?.classes ?? [])]);
    this.editingClasses.set(true);
  }

  cancelClassesEdit(): void {
    this.editingClasses.set(false);
    this.editableClasses.set([]);
  }

  saveClasses(): void {
    const profile = this.profile();
    if (!profile) return;

    const classes = [...new Set(this.editableClasses()
      .map(classLabel => classLabel.trim())
      .filter(Boolean))];

    this.savingClasses.set(true);
    this.teachersService.updateTeaching(profile.userId, {
      subject: profile.subject,
      stage: profile.stage,
      classes
    }).subscribe({
      next: response => {
        this.savingClasses.set(false);
        if (!response.isSuccess || !response.data) {
          this.toast.error('TEACHERS.CLASSES_SAVE_FAILED', response.message || '');
          return;
        }

        this.profile.update(current => current
          ? { ...current, subject: response.data!.subject, stage: response.data!.stage, classes: response.data!.classes }
          : current);
        this.editingClasses.set(false);
        this.toast.success('TEACHERS.CLASSES_SAVE_SUCCESS', response.message || '');
      },
      error: () => {
        this.savingClasses.set(false);
        this.toast.error('TEACHERS.CLASSES_SAVE_FAILED');
      }
    });
  }

  startNewVisitForTeacher(): void {
    const id = this.userId();
    if (!id) return;
    // Reuse the existing visit-create flow with the instructor preselected
    // (visit-form already reads `instructorId` from queryParam if present).
    this.router.navigate(['/visits/new'], { queryParams: { instructorId: id } });
  }

  statusSeverity(status: number): 'success' | 'warning' | 'danger' | 'info' | 'secondary' {
    if (status === 1) return 'warning';   // Draft
    if (status === 3) return 'info';      // PendingApproval
    if (status === 4) return 'success';   // Approved
    if (status === 5) return 'danger';    // Rejected
    if (status === 6) return 'warning';   // Reopened
    return 'secondary';
  }

  readonly canViewVisit = computed(() => this.auth.hasPermission('Visit.View'));

  openVisit(visitId: number): void {
    if (!this.canViewVisit()) return;
    this.router.navigate(['/visits', visitId]);
  }

  trackVisit(_idx: number, v: TeacherVisitSummary): number { return v.id; }
  trackSeries(_idx: number, v: TeacherVisitProgress): number { return v.visitId; }
}

/** Converts #RRGGBB + alpha into an rgba() string for chart fill tints. */
function hexToRgba(hex: string, alpha: number): string {
  const h = hex.replace('#', '');
  const r = parseInt(h.substring(0, 2), 16);
  const g = parseInt(h.substring(2, 4), 16);
  const b = parseInt(h.substring(4, 6), 16);
  return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}
