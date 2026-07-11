import { Component, OnInit, signal, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { FormBuilder, FormGroup, Validators, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputGroupModule } from 'primeng/inputgroup';
import { InputGroupAddonModule } from 'primeng/inputgroupaddon';
import { InputTextModule } from 'primeng/inputtext';
import { DropdownModule } from 'primeng/dropdown';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { ConfirmationService } from 'primeng/api';
import { ToastService } from '../../../core/services/toast.service';
import { SchoolsService } from '../../../core/services/schools.service';
import { UsersService } from '../../../core/services/users.service';
import { ListPageHeaderComponent } from '../../../shared/components/list-toolbar/list-page-header.component';
import { ListToolbarComponent } from '../../../shared/components/list-toolbar/list-toolbar.component';
import { ListToolbarFieldComponent } from '../../../shared/components/list-toolbar/list-toolbar-field.component';
import { SchoolListItem, SchoolStage } from '../../../core/models/phase2.models';

@Component({
  selector: 'app-schools-list',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ReactiveFormsModule, TranslateModule,
    TableModule, ButtonModule, InputTextModule, InputGroupModule, InputGroupAddonModule, DropdownModule,
    TagModule, TooltipModule, ConfirmDialogModule, DialogModule,
    ListPageHeaderComponent, ListToolbarComponent, ListToolbarFieldComponent
  ],
  providers: [ConfirmationService],
  templateUrl: './schools-list.component.html',
  styleUrls: ['./schools-list.component.css']
})
export class SchoolsListComponent implements OnInit {
  private readonly schoolsService = inject(SchoolsService);
  private readonly usersService = inject(UsersService);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmationService);
  private readonly translate = inject(TranslateService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  schools = signal<SchoolListItem[]>([]);
  totalCount = signal(0);
  loading = signal(false);
  searchTerm = signal('');
  cityFilter = signal<string | null>(null);
  stageFilter = signal<SchoolStage | null>(null);
  isActiveFilter = signal<boolean | null>(null);

  // Assign Manager dialog
  assignDialogVisible = signal(false);
  assignSchoolId = signal<number | null>(null);
  assignSchoolName = signal('');
  availableManagers = signal<{ userId: string; fullName: string }[]>([]);
  managersLoading = signal(false);
  assignForm: FormGroup = this.fb.group({
    userId: ['', Validators.required]
  });

  /** A5: SchoolStage → Arabic label via i18n (ابتدائي / متوسط / ثانوي). */
  stageLabel(stage: SchoolStage | string | null | undefined): string {
    if (!stage) return '—';
    return this.translate.instant(`SCHOOLS.STAGE.${String(stage).toUpperCase()}`);
  }

  readonly stageOptions = [
    { label: 'ابتدائي', value: 'Primary' },
    { label: 'متوسط', value: 'Intermediate' },
    { label: 'ثانوي', value: 'Secondary' }
  ];

  readonly isActiveOptions = [
    { label: 'نشطة', value: true },
    { label: 'غير نشطة', value: false }
  ];

  ngOnInit(): void {
    this.loadSchools();
  }

  loadSchools(event?: TableLazyLoadEvent): void {
    const page = (event?.first ?? 0) / (event?.rows ?? 20) + 1;
    const pageSize = event?.rows ?? 20;
    this.loading.set(true);

    this.schoolsService.list({
      page,
      pageSize,
      search: this.searchTerm() || undefined,
      city: this.cityFilter() ?? undefined,
      stage: this.stageFilter() ?? undefined,
      isActive: this.isActiveFilter() ?? undefined
    }).subscribe({
      next: (response) => {
        if (response.isSuccess && response.data) {
          this.schools.set(response.data.items);
          this.totalCount.set(response.data.totalCount);
        }
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  onSearch(): void { this.loadSchools(); }
  onFilter(): void { this.loadSchools(); }

  goToCreate(): void { this.router.navigate(['/schools/new']); }
  goToEdit(school: SchoolListItem): void { this.router.navigate(['/schools', school.id, 'edit']); }

  confirmDelete(school: SchoolListItem, event: Event): void {
    this.confirm.confirm({
      target: event.target as EventTarget,
      message: 'هل تريد حذف هذه المدرسة؟ سيتم إخفاء المدرسة من القوائم مع الإبقاء على بياناتها.',
      header: 'تأكيد الحذف',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'نعم، احذف',
      rejectLabel: 'إلغاء',
      accept: () => this.deleteSchool(school.id)
    });
  }

  deleteSchool(id: number): void {
    this.schoolsService.delete(id).subscribe({
      next: (response) => {
        if (response.isSuccess) {
          this.toast.success('تم الحذف', response.message || 'تم حذف المدرسة بنجاح.');
          this.loadSchools();
        }
      }
    });
  }

  toggleActive(school: SchoolListItem): void {
    const action = school.isActive
      ? this.schoolsService.deactivate(school.id)
      : this.schoolsService.activate(school.id);

    action.subscribe({
      next: (response) => {
        if (response.isSuccess) {
          this.toast.success('تم التحديث', response.message || 'تم تحديث حالة المدرسة.');
          this.loadSchools();
        }
      }
    });
  }

  openAssignManager(school: SchoolListItem): void {
    this.assignSchoolId.set(school.id);
    this.assignSchoolName.set(school.name);
    this.assignForm.reset();
    this.availableManagers.set([]);
    this.managersLoading.set(true);
    this.assignDialogVisible.set(true);
    // FIX 1: load eligible managers (SchoolManager-role, active) for THIS school.
    // The API also trims each user's `schools[]` to the caller's ActiveSchoolId
    // (D-24) — Main Manager / Super Admin callers see everyone.
    this.usersService.list({ role: 'SchoolManager', isActive: true, page: 1, pageSize: 100 }).subscribe({
      next: (response) => {
        this.managersLoading.set(false);
        if (response.isSuccess && response.data) {
          this.availableManagers.set(
            response.data.items
              .filter(u => !u.schools.some(s => s.schoolId === school.id) || u.userId === school.managerUserId)
              .map(u => ({ userId: u.userId, fullName: `${u.fullName} (${u.username})` }))
          );
        }
      },
      error: () => this.managersLoading.set(false)
    });
  }

  submitAssignManager(): void {
    if (this.assignForm.invalid) {
      this.assignForm.markAllAsTouched();
      return;
    }
    const schoolId = this.assignSchoolId();
    if (!schoolId) return;

    this.schoolsService.assignManager(schoolId, this.assignForm.value).subscribe({
      next: (response) => {
        if (response.isSuccess) {
          this.toast.success('تم التعيين', response.message || 'تم تعيين مدير المدرسة بنجاح.');
          this.assignDialogVisible.set(false);
          this.loadSchools();
        }
      }
    });
  }
}