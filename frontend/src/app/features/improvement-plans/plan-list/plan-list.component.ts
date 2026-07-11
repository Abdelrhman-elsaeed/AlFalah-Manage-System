import { Component, OnInit, signal, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';

// PrimeNG
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { CalendarModule } from 'primeng/calendar';
import { TagModule } from 'primeng/tag';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';

// Services & Models
import { ImprovementPlansService } from '../../../core/services/improvement-plans.service';
import { VisitsService } from '../../../core/services/visits.service';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { ImprovementPlan, WeakDomainSuggestion } from '../../../core/models/improvement-plan.models';
import { VisitDetail } from '../../../core/models/visit.models';

@Component({
  selector: 'app-plan-list',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, RouterModule,
    TableModule, ButtonModule, DialogModule, DropdownModule, InputTextModule,
    InputTextareaModule, CalendarModule, TagModule, ConfirmDialogModule
  ],
  providers: [ConfirmationService],
  templateUrl: './plan-list.component.html',
  styleUrls: ['./plan-list.component.css']
})
export class PlanListComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly plansService = inject(ImprovementPlansService);
  private readonly visitsService = inject(VisitsService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmationService);

  readonly visitId = signal<number>(0);
  readonly visit = signal<VisitDetail | null>(null);
  readonly plans = signal<ImprovementPlan[]>([]);
  readonly suggestions = signal<WeakDomainSuggestion[]>([]);
  readonly loading = signal(false);

  // Filter
  readonly statusFilter = signal<string>('all');

  // Plan Dialog
  readonly planDialogVisible = signal(false);
  readonly dialogMode = signal<'create' | 'edit'>('create');
  readonly planForm: FormGroup;
  readonly submitting = signal(false);
  readonly selectedPlanId = signal<number | null>(null);

  // Role permissions
  readonly isInstructor = computed(() => this.auth.hasRole('Instructor'));
  readonly canCreate = computed(() => this.auth.hasPermission('Plan.Create'));
  readonly canEdit = computed(() => this.auth.hasPermission('Plan.Edit'));
  readonly canDelete = computed(() => this.auth.hasPermission('Plan.Delete'));

  // Get domains from visit scores to prefill selection dropdown
  readonly visitDomains = computed(() => {
    const v = this.visit();
    if (!v) return [];
    const map = new Map<number, { id: number; nameAr: string }>();
    for (const s of v.scores) {
      if (!map.has(s.rubricDomainId)) {
        map.set(s.rubricDomainId, { id: s.rubricDomainId, nameAr: `${s.domainCode} — ${s.domainNameAr}` });
      }
    }
    return Array.from(map.values());
  });

  // Filtered plans based on dropdown choice
  readonly filteredPlans = computed(() => {
    const filter = this.statusFilter();
    const list = this.plans();
    if (filter === 'all') return list;
    return list.filter(p => p.status === filter);
  });

  // Check if EndDate < StartDate to show alert
  readonly isDateRangeInvalid = computed(() => {
    const val = this.planForm.value;
    if (!val.startDate || !val.endDate) return false;
    const start = new Date(val.startDate);
    const end = new Date(val.endDate);
    return end < start;
  });

  constructor() {
    this.planForm = this.fb.group({
      domainId: [null],
      goal: ['', [Validators.required, Validators.maxLength(2000)]],
      actions: ['', [Validators.required, Validators.maxLength(4000)]],
      startDate: [new Date(), Validators.required],
      endDate: [this.addMonths(new Date(), 2), Validators.required],
      successIndicators: ['', [Validators.required, Validators.maxLength(2000)]],
      status: ['active']
    });
  }

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('visitId'));
    if (!id) {
      this.router.navigate(['/visits']);
      return;
    }
    this.visitId.set(id);
    this.loadAll();
  }

  loadAll(): void {
    this.loading.set(true);

    // Load visit detail first
    const pathInstructor = this.isInstructor();
    const visitRequest: any = pathInstructor 
      ? this.visitsService.getInstructorReport(this.visitId()) 
      : this.visitsService.getById(this.visitId());

    visitRequest.subscribe({
      next: (resp: any) => {
        if (resp.isSuccess && resp.data) {
          // If instructor path, Map details properly (instructorReport has similar structure or we cast)
          this.visit.set(resp.data as VisitDetail);
          this.loadPlans();
          this.loadSuggestions();
        } else {
          this.toast.error('خطأ', resp.message || 'تعذر تحميل بيانات الزيارة.');
          this.loading.set(false);
        }
      },
      error: () => this.loading.set(false)
    });
  }

  loadPlans(): void {
    this.plansService.getPlansForVisit(this.visitId()).subscribe({
      next: (resp) => {
        if (resp.isSuccess && resp.data) {
          this.plans.set(resp.data);
        }
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  loadSuggestions(): void {
    this.plansService.getWeakDomainSuggestions(this.visitId()).subscribe({
      next: (resp) => {
        if (resp.isSuccess && resp.data) {
          this.suggestions.set(resp.data);
        }
      }
    });
  }

  openCreateDialog(): void {
    this.dialogMode.set('create');
    this.selectedPlanId.set(null);
    this.planForm.reset({
      domainId: null,
      goal: '',
      actions: '',
      startDate: new Date(),
      endDate: this.addMonths(new Date(), 2),
      successIndicators: '',
      status: 'active'
    });
    this.planDialogVisible.set(true);
  }

  onSuggestionClick(sug: WeakDomainSuggestion): void {
    this.dialogMode.set('create');
    this.selectedPlanId.set(null);
    this.planForm.reset({
      domainId: sug.domainId,
      goal: sug.prefilledGoal,
      actions: sug.prefilledActions,
      startDate: new Date(),
      endDate: this.addMonths(new Date(), 2),
      successIndicators: sug.prefilledSuccessIndicators,
      status: 'active'
    });
    this.planDialogVisible.set(true);
  }

  openEditDialog(plan: ImprovementPlan): void {
    this.dialogMode.set('edit');
    this.selectedPlanId.set(plan.id);
    this.planForm.reset({
      domainId: plan.domainId,
      goal: plan.goal,
      actions: plan.actions,
      startDate: new Date(plan.startDate),
      endDate: new Date(plan.endDate),
      successIndicators: plan.successIndicators,
      status: plan.status
    });
    this.planDialogVisible.set(true);
  }

  savePlan(): void {
    if (this.planForm.invalid) {
      this.planForm.markAllAsTouched();
      return;
    }

    const formVal = this.planForm.value;
    const startDateIso = new Date(formVal.startDate).toISOString();
    const endDateIso = new Date(formVal.endDate).toISOString();

    this.submitting.set(true);

    if (this.dialogMode() === 'create') {
      const body = {
        instructorId: this.visit()?.instructorId || '',
        visitId: this.visitId(),
        domainId: formVal.domainId,
        goal: formVal.goal,
        actions: formVal.actions,
        startDate: startDateIso,
        endDate: endDateIso,
        successIndicators: formVal.successIndicators
      };

      this.plansService.createPlan(body).subscribe({
        next: (resp) => {
          this.submitting.set(false);
          if (resp.isSuccess) {
            this.toast.success('نجاح', 'تم حفظ خطة التحسين بنجاح.');
            this.planDialogVisible.set(false);
            this.loadPlans();
          } else {
            this.toast.error('خطأ', resp.message || 'تعذر حفظ الخطة.');
          }
        },
        error: () => this.submitting.set(false)
      });
    } else {
      const body = {
        goal: formVal.goal,
        actions: formVal.actions,
        startDate: startDateIso,
        endDate: endDateIso,
        successIndicators: formVal.successIndicators,
        status: formVal.status
      };

      this.plansService.updatePlan(this.selectedPlanId()!, body).subscribe({
        next: (resp) => {
          this.submitting.set(false);
          if (resp.isSuccess) {
            this.toast.success('نجاح', 'تم تحديث خطة التحسين بنجاح.');
            this.planDialogVisible.set(false);
            this.loadPlans();
          } else {
            this.toast.error('خطأ', resp.message || 'تعذر تحديث الخطة.');
          }
        },
        error: () => this.submitting.set(false)
      });
    }
  }

  confirmDelete(plan: ImprovementPlan): void {
    this.confirm.confirm({
      message: 'هل أنت متأكد من حذف خطة التحسين هذه؟ سيتم إخفاء جميع المتابعات الخاصة بها.',
      header: 'تأكيد الحذف',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'نعم، احذف',
      rejectLabel: 'إلغاء',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.plansService.deletePlan(plan.id).subscribe({
          next: (resp) => {
            if (resp.isSuccess) {
              this.toast.success('نجاح', 'تم حذف الخطة بنجاح.');
              this.loadPlans();
            } else {
              this.toast.error('خطأ', resp.message || 'تعذر حذف الخطة.');
            }
          }
        });
      }
    });
  }

  statusSeverity(status: string): 'success' | 'warning' | 'danger' | 'info' {
    if (status === 'completed') return 'success';
    if (status === 'cancelled') return 'danger';
    return 'info'; // active
  }

  statusLabel(status: string): string {
    if (status === 'completed') return 'مكتملة';
    if (status === 'cancelled') return 'ملغاة';
    return 'نشطة';
  }

  goBack(): void {
    this.router.navigate(['/visits', this.visitId()]);
  }

  private addMonths(date: Date, months: number): Date {
    const d = new Date(date);
    d.setMonth(d.getMonth() + months);
    return d;
  }
}
