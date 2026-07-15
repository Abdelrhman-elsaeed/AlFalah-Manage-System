import { Component, OnInit, signal, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';

// PrimeNG
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { CalendarModule } from 'primeng/calendar';
import { TagModule } from 'primeng/tag';
import { InputNumberModule } from 'primeng/inputnumber';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';
import { ChartModule } from 'primeng/chart';
import { TooltipModule } from 'primeng/tooltip';

// Services & Models
import { ImprovementPlansService } from '../../../core/services/improvement-plans.service';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { ImprovementPlan, PlanFollowUp, PlanProgress } from '../../../core/models/improvement-plan.models';

@Component({
  selector: 'app-plan-detail',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, RouterModule,
    ButtonModule, DialogModule, InputTextModule, InputTextareaModule, CalendarModule,
    TagModule, InputNumberModule, ConfirmDialogModule, ChartModule, TooltipModule
  ],
  providers: [ConfirmationService],
  templateUrl: './plan-detail.component.html',
  styleUrls: ['./plan-detail.component.css']
})
export class PlanDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly plansService = inject(ImprovementPlansService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmationService);
  private readonly translate = inject(TranslateService);

  readonly planId = signal<number>(0);
  readonly plan = signal<ImprovementPlan | null>(null);
  readonly progress = signal<PlanProgress | null>(null);
  readonly loading = signal(false);

  // Follow-up Dialog
  readonly followUpDialogVisible = signal(false);
  readonly dialogMode = signal<'create' | 'edit'>('create');
  readonly followUpForm: FormGroup;
  readonly submitting = signal(false);
  readonly selectedFollowUpId = signal<number | null>(null);

  // Chart configuration
  readonly chartData = computed(() => {
    const prog = this.progress();
    if (!prog || prog.chartData.length < 2) return null;

    // Dates in readable format
    const labels = prog.chartData.map(pt => {
      const date = new Date(pt.followDate);
      return `${date.getFullYear()}/${date.getMonth() + 1}/${date.getDate()}`;
    });
    const data = prog.chartData.map(pt => pt.progressScore);

    return {
      labels: labels,
      datasets: [
        {
          label: this.translate.instant('FOLLOWUPS.CHART_LABEL'),
          data: data,
          fill: false,
          borderColor: '#10b981',
          tension: 0.2
        }
      ]
    };
  });

  readonly chartOptions = {
    plugins: {
      legend: {
        labels: {
          font: { family: 'Cairo, sans-serif' }
        }
      }
    },
    scales: {
      y: {
        min: 0,
        max: 100,
        ticks: {
          callback: (value: any) => value + '%'
        }
      }
    }
  };

  // Role checks
  readonly isInstructor = computed(() => this.auth.hasRole('Instructor'));
  readonly canEdit = computed(() => this.auth.hasPermission('Plan.Edit'));
  readonly canDelete = computed(() => this.auth.hasPermission('Plan.Delete'));

  constructor() {
    this.followUpForm = this.fb.group({
      followDate: [new Date(), Validators.required],
      progressNote: ['', [Validators.required, Validators.maxLength(2000)]],
      evidenceNote: ['', [Validators.maxLength(2000)]],
      progressScore: [null, [Validators.min(0), Validators.max(100)]]
    });
  }

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!id) {
      this.router.navigate(['/visits']);
      return;
    }
    this.planId.set(id);
    this.loadAll();
  }

  loadAll(): void {
    this.loading.set(true);
    this.plansService.getPlanById(this.planId()).subscribe({
      next: (resp) => {
        if (resp.isSuccess && resp.data) {
          this.plan.set(resp.data);
          this.loadProgress();
        } else {
          this.toast.error(this.t('COMMON.ERROR'), resp.message || this.t('FOLLOWUPS.LOAD_FAILED'));
          this.loading.set(false);
        }
      },
      error: () => this.loading.set(false)
    });
  }

  loadProgress(): void {
    this.plansService.getPlanProgress(this.planId()).subscribe({
      next: (resp) => {
        if (resp.isSuccess && resp.data) {
          this.progress.set(resp.data);
        }
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  openCreateDialog(): void {
    this.dialogMode.set('create');
    this.selectedFollowUpId.set(null);
    this.followUpForm.reset({
      followDate: new Date(),
      progressNote: '',
      evidenceNote: '',
      progressScore: null
    });
    this.followUpDialogVisible.set(true);
  }

  openEditDialog(fu: PlanFollowUp): void {
    this.dialogMode.set('edit');
    this.selectedFollowUpId.set(fu.id);
    this.followUpForm.reset({
      followDate: new Date(fu.followDate),
      progressNote: fu.progressNote,
      evidenceNote: fu.evidenceNote || '',
      progressScore: fu.progressScore
    });
    this.followUpDialogVisible.set(true);
  }

  saveFollowUp(): void {
    if (this.followUpForm.invalid) {
      this.followUpForm.markAllAsTouched();
      return;
    }

    const val = this.followUpForm.value;
    const body = {
      followDate: new Date(val.followDate).toISOString(),
      progressNote: val.progressNote,
      evidenceNote: val.evidenceNote || null,
      progressScore: val.progressScore !== null ? Number(val.progressScore) : null
    };

    this.submitting.set(true);

    if (this.dialogMode() === 'create') {
      this.plansService.addFollowUp(this.planId(), body).subscribe({
        next: (resp) => {
          this.submitting.set(false);
          if (resp.isSuccess) {
            this.toast.success(this.t('COMMON.SUCCESS'), this.t('FOLLOWUPS.ADD_SUCCESS'));
            this.followUpDialogVisible.set(false);
            this.loadAll();
          } else {
            this.toast.error(this.t('COMMON.ERROR'), resp.message || this.t('FOLLOWUPS.ADD_FAILED'));
          }
        },
        error: (error) => {
          this.submitting.set(false);
          this.toast.error(this.t('COMMON.ERROR'), error?.error?.message || this.t('FOLLOWUPS.ADD_FAILED'));
        }
      });
    } else {
      this.plansService.updateFollowUp(this.selectedFollowUpId()!, body).subscribe({
        next: (resp) => {
          this.submitting.set(false);
          if (resp.isSuccess) {
            this.toast.success(this.t('COMMON.SUCCESS'), this.t('FOLLOWUPS.UPDATE_SUCCESS'));
            this.followUpDialogVisible.set(false);
            this.loadAll();
          } else {
            this.toast.error(this.t('COMMON.ERROR'), resp.message || this.t('FOLLOWUPS.UPDATE_FAILED'));
          }
        },
        error: (error) => {
          this.submitting.set(false);
          this.toast.error(this.t('COMMON.ERROR'), error?.error?.message || this.t('FOLLOWUPS.UPDATE_FAILED'));
        }
      });
    }
  }

  confirmDelete(fu: PlanFollowUp): void {
    this.confirm.confirm({
      message: this.t('FOLLOWUPS.DELETE_CONFIRM'),
      header: this.t('FOLLOWUPS.DELETE_CONFIRM_TITLE'),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.t('FOLLOWUPS.DELETE_ACCEPT'),
      rejectLabel: this.t('COMMON.CANCEL'),
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.plansService.deleteFollowUp(fu.id).subscribe({
          next: (resp) => {
            if (resp.isSuccess) {
              this.toast.success(this.t('COMMON.SUCCESS'), this.t('FOLLOWUPS.DELETE_SUCCESS'));
              this.loadAll();
            } else {
              this.toast.error(this.t('COMMON.ERROR'), resp.message || this.t('FOLLOWUPS.DELETE_FAILED'));
            }
          },
          error: (error) => this.toast.error(
            this.t('COMMON.ERROR'),
            error?.error?.message || this.t('FOLLOWUPS.DELETE_FAILED'))
        });
      }
    });
  }

  badgeColor(score: number): 'success' | 'warning' | 'danger' {
    if (score >= 75) return 'success';
    if (score >= 50) return 'warning';
    return 'danger';
  }

  progressColorSeverity(color: string | null | undefined): 'success' | 'warning' | 'danger' | 'info' {
    if (color === 'success') return 'success';
    if (color === 'warning') return 'warning';
    if (color === 'danger') return 'danger';
    return 'info';
  }

  statusSeverity(status: string): 'success' | 'warning' | 'danger' | 'info' {
    if (status === 'completed') return 'success';
    if (status === 'cancelled') return 'danger';
    return 'info'; // active
  }

  statusLabelKey(status: string): string {
    if (status === 'completed') return 'PLANS.STATUS_COMPLETED';
    if (status === 'cancelled') return 'PLANS.STATUS_CANCELLED';
    return 'PLANS.STATUS_ACTIVE';
  }

  progressStatusKey(score: number): string {
    if (score >= 75) return 'FOLLOWUPS.PROGRESS_EXCELLENT';
    if (score >= 50) return 'FOLLOWUPS.PROGRESS_IN_PROGRESS';
    return 'FOLLOWUPS.PROGRESS_DELAYED';
  }

  goBack(): void {
    const p = this.plan();
    if (p) {
      this.router.navigate(['/visits', p.visitId, 'improvement-plans']);
    } else {
      this.router.navigate(['/visits']);
    }
  }

  private t(key: string): string {
    return this.translate.instant(key);
  }
}
