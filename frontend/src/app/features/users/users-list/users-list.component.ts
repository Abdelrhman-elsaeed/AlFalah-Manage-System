import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { FormBuilder, FormGroup, Validators, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputGroupModule } from 'primeng/inputgroup';
import { InputGroupAddonModule } from 'primeng/inputgroupaddon';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { DropdownModule } from 'primeng/dropdown';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { ConfirmationService } from 'primeng/api';
import { ToastService } from '../../../core/services/toast.service';
import { UsersService } from '../../../core/services/users.service';
import { UserSchoolRolesService } from '../../../core/services/user-school-roles.service';
import { SchoolsService } from '../../../core/services/schools.service';
import { ListPageHeaderComponent } from '../../../shared/components/list-toolbar/list-page-header.component';
import { ListToolbarComponent } from '../../../shared/components/list-toolbar/list-toolbar.component';
import { ListToolbarFieldComponent } from '../../../shared/components/list-toolbar/list-toolbar-field.component';
import { UserListItem, PhaseTwoRole } from '../../../core/models/phase2.models';

@Component({
  selector: 'app-users-list',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ReactiveFormsModule, TranslateModule,
    TableModule, ButtonModule, InputTextModule, InputGroupModule, InputGroupAddonModule, InputNumberModule, DropdownModule,
    TagModule, TooltipModule, ConfirmDialogModule, DialogModule,
    ListPageHeaderComponent, ListToolbarComponent, ListToolbarFieldComponent
  ],
  providers: [ConfirmationService],
  templateUrl: './users-list.component.html',
  styleUrls: ['./users-list.component.css']
})
export class UsersListComponent implements OnInit {
  private readonly usersService = inject(UsersService);
  private readonly userSchoolRolesService = inject(UserSchoolRolesService);
  private readonly schoolsService = inject(SchoolsService);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmationService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  users = signal<UserListItem[]>([]);
  totalCount = signal(0);
  loading = signal(false);
  searchTerm = signal('');
  roleFilter = signal<string | null>(null);
  isActiveFilter = signal<boolean | null>(null);

  // Assign-to-school dialog
  assignDialogVisible = signal(false);
  selectedUserId = signal<string | null>(null);
  selectedUserName = signal<string>('');
  availableSchools = signal<{ id: number; name: string }[]>([]);
  schoolsLoading = signal(false);
  assignForm: FormGroup = this.fb.group({
    schoolId: ['', Validators.required],
    role: ['Moderator' as PhaseTwoRole, Validators.required]
  });

  readonly roleOptions = [
    { label: 'مدير المدرسة', value: 'SchoolManager' },
    { label: 'مشرف', value: 'Moderator' },
    { label: 'معلم', value: 'Instructor' }
  ];

  readonly isActiveOptions = [
    { label: 'نشط', value: true },
    { label: 'غير نشط', value: false }
  ];

  ngOnInit(): void {
    this.loadUsers();
  }

  loadUsers(event?: TableLazyLoadEvent): void {
    const page = (event?.first ?? 0) / (event?.rows ?? 20) + 1;
    const pageSize = event?.rows ?? 20;
    this.loading.set(true);

    this.usersService.list({
      page,
      pageSize,
      search: this.searchTerm() || undefined,
      role: this.roleFilter() ?? undefined,
      isActive: this.isActiveFilter() ?? undefined
    }).subscribe({
      next: (response) => {
        if (response.isSuccess && response.data) {
          this.users.set(response.data.items);
          this.totalCount.set(response.data.totalCount);
        }
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  onSearch(): void { this.loadUsers(); }
  onFilter(): void { this.loadUsers(); }

  goToCreate(): void { this.router.navigate(['/users/new']); }
  goToEdit(user: UserListItem): void { this.router.navigate(['/users', user.userId, 'edit']); }

  confirmDeactivate(user: UserListItem, event: Event): void {
    this.confirm.confirm({
      target: event.target as EventTarget,
      message: 'هل تريد تعطيل هذا المستخدم؟',
      header: 'تأكيد التعطيل',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'نعم، عطّل',
      rejectLabel: 'إلغاء',
      accept: () => this.deactivateUser(user.userId)
    });
  }

  deactivateUser(id: string): void {
    this.usersService.deactivate(id).subscribe({
      next: (response) => {
        if (response.isSuccess) {
          this.toast.success('تم التعطيل', response.message || 'تم تعطيل المستخدم بنجاح.');
          this.loadUsers();
        }
      }
    });
  }

  openAssignDialog(user: UserListItem): void {
    this.selectedUserId.set(user.userId);
    this.selectedUserName.set(user.fullName);
    this.assignForm.reset({ schoolId: '', role: 'Moderator' });
    this.availableSchools.set([]);
    this.schoolsLoading.set(true);
    this.assignDialogVisible.set(true);

    this.schoolsService.list({ isActive: true, page: 1, pageSize: 100 }).subscribe({
      next: (response) => {
        this.schoolsLoading.set(false);
        if (response.isSuccess && response.data) {
          this.availableSchools.set(
            response.data.items.map(s => ({ id: s.id, name: `${s.name} — ${s.city}` }))
          );
        }
      },
      error: () => this.schoolsLoading.set(false)
    });
  }

  submitAssign(): void {
    const userId = this.selectedUserId();
    if (!userId || this.assignForm.invalid) {
      this.assignForm.markAllAsTouched();
      return;
    }
    const v = this.assignForm.value;
    this.userSchoolRolesService.create({
      userId,
      schoolId: Number(v.schoolId),
      role: v.role as PhaseTwoRole
    }).subscribe({
      next: (response) => {
        if (response.isSuccess) {
          this.toast.success('تم التعيين', response.message || 'تم تعيين المستخدم للمدرسة.');
          this.assignDialogVisible.set(false);
          this.loadUsers();
        }
      }
    });
  }
}