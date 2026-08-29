import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { ImprovementPlanListItem } from '../../../core/models/improvement-plan.models';
import { ImprovementPlansService } from '../../../core/services/improvement-plans.service';
import { ToastService } from '../../../core/services/toast.service';
import { ClearableSelectComponent } from '../../../shared/components/clearable-select/clearable-select.component';

@Component({
  selector: 'app-plan-overview',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    TranslateModule,
    ButtonModule,
    TableModule,
    TagModule,
    TooltipModule,
    ClearableSelectComponent
  ],
  templateUrl: './plan-overview.component.html',
  styleUrls: ['./plan-overview.component.css']
})
export class PlanOverviewComponent implements OnInit {
  private readonly plansService = inject(ImprovementPlansService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  readonly plans = signal<ImprovementPlanListItem[]>([]);
  readonly loading = signal(false);
  readonly statusFilter = signal<string>('all');
  readonly statusOptions = [
    { labelKey: 'PLANS.STATUS_ALL', value: 'all' },
    { labelKey: 'PLANS.STATUS_ACTIVE', value: 'active' },
    { labelKey: 'PLANS.STATUS_COMPLETED', value: 'completed' },
    { labelKey: 'PLANS.STATUS_CANCELLED', value: 'cancelled' }
  ];

  readonly filteredPlans = computed(() => {
    const status = this.statusFilter();
    return status === 'all' ? this.plans() : this.plans().filter(plan => plan.status === status);
  });

  readonly activeCount = computed(() => this.plans().filter(plan => plan.status === 'active').length);
  readonly completedCount = computed(() => this.plans().filter(plan => plan.status === 'completed').length);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.plansService.getPlans().subscribe({
      next: response => {
        this.loading.set(false);
        if (response.isSuccess && response.data) {
          this.plans.set(response.data);
          return;
        }
        this.toast.error(this.t('COMMON.ERROR'), response.message || this.t('PLANS.LOAD_FAILED'));
      },
      error: () => this.loading.set(false)
    });
  }

  statusSeverity(status: string): 'success' | 'warning' | 'danger' | 'info' {
    if (status === 'completed') return 'success';
    if (status === 'cancelled') return 'danger';
    return 'info';
  }

  statusLabelKey(status: string): string {
    if (status === 'completed') return 'PLANS.STATUS_COMPLETED';
    if (status === 'cancelled') return 'PLANS.STATUS_CANCELLED';
    return 'PLANS.STATUS_ACTIVE';
  }

  private t(key: string): string {
    return this.translate.instant(key);
  }
}
