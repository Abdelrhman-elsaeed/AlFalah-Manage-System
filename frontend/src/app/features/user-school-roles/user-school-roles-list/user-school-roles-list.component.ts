import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { InputGroupModule } from 'primeng/inputgroup';
import { InputGroupAddonModule } from 'primeng/inputgroupaddon';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { ClearableSelectComponent } from '../../../shared/components/clearable-select/clearable-select.component';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { ConfirmationService } from 'primeng/api';
import { ToastService } from '../../../core/services/toast.service';
import { AuthService } from '../../../core/services/auth.service';
import { UserSchoolRolesService } from '../../../core/services/user-school-roles.service';
import { UsersService } from '../../../core/services/users.service';
import { SchoolsService } from '../../../core/services/schools.service';
import { ListPageHeaderComponent } from '../../../shared/components/list-toolbar/list-page-header.component';
import { ListToolbarComponent } from '../../../shared/components/list-toolbar/list-toolbar.component';
import { ListToolbarFieldComponent } from '../../../shared/components/list-toolbar/list-toolbar-field.component';
import { UserSchoolRoleDetail, PhaseTwoRole } from '../../../core/models/phase2.models';

@Component({
  selector: 'app-user-school-roles-list',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ReactiveFormsModule, TranslateModule,
    TableModule, ButtonModule, ClearableSelectComponent, InputGroupModule, InputGroupAddonModule, TooltipModule, ConfirmDialogModule, DialogModule,
    ListPageHeaderComponent, ListToolbarComponent, ListToolbarFieldComponent
  ],
  providers: [ConfirmationService],
  templateUrl: './user-school-roles-list.component.html',
  styleUrls: ['./user-school-roles-list.component.css']
})
export class UserSchoolRolesListComponent implements OnInit {
  private readonly usrService = inject(UserSchoolRolesService);
  private readonly usersService = inject(UsersService);
  private readonly schoolsService = inject(SchoolsService);
  private readonly authService = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmationService);
  private readonly fb = inject(FormBuilder);
  private readonly translate = inject(TranslateService);

  assignments = signal<UserSchoolRoleDetail[]>([]);
  loading = signal(false);
  schoolFilter = signal<number | null>(null);

  schoolOptions = signal<{ id: number; name: string }[]>([]);
  userOptions = signal<{ userId: string; fullName: string }[]>([]);
  lookupsLoading = signal(false);

  readonly roleOptions = [
    { label: this.translate.instant('USERS.ROLE_SCHOOL_MANAGER'), value: 'SchoolManager' },
    { label: 'السكرتير', value: 'Secretary' },
    { label: this.translate.instant('USERS.ROLE_MODERATOR'), value: 'Moderator' },
    { label: this.translate.instant('USERS.ROLE_INSTRUCTOR'), value: 'Instructor' }
  ];

  createVisible = signal(false);
  form: FormGroup = this.fb.group({
    userId: ['', Validators.required],
    schoolId: ['', Validators.required],
    role: ['Moderator' as PhaseTwoRole, Validators.required]
  });

  /** True when the caller has no ActiveSchoolId (global admin). Admin sees all schools. */
  readonly isSchoolScoped: boolean;

  constructor() {
    const active = this.authService.activeSchoolId();
    this.isSchoolScoped = active !== null && active !== undefined;
    if (active) {
      // School-scoped callers default the assignments filter to their own school.
      this.schoolFilter.set(active);
    }
  }

  ngOnInit(): void {
    this.loadLookups();
    this.load();
  }

  loadLookups(): void {
    this.lookupsLoading.set(true);
    this.schoolsService.list({ isActive: true, page: 1, pageSize: 200 }).subscribe({
      next: (response) => {
        if (response.isSuccess && response.data) {
          this.schoolOptions.set(
            response.data.items.map(s => ({ id: s.id, name: `${s.name} — ${s.city}` }))
          );
        }
        this.lookupsLoading.set(false);
      },
      error: () => this.lookupsLoading.set(false)
    });
    this.usersService.list({ isActive: true, page: 1, pageSize: 200 }).subscribe({
      next: (response) => {
        if (response.isSuccess && response.data) {
          this.userOptions.set(
            response.data.items.map(u => ({ userId: u.userId, fullName: `${u.fullName} (${u.username})` }))
          );
        }
      }
    });
  }

  load(event?: TableLazyLoadEvent): void {
    this.loading.set(true);
    this.usrService.getBySchool(this.schoolFilter() ?? undefined).subscribe({
      next: (response) => {
        if (response.isSuccess && response.data) {
          this.assignments.set(response.data);
        }
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  openCreate(): void {
    const active = this.authService.activeSchoolId();
    this.form.reset({
      userId: '',
      schoolId: active ? String(active) : '',
      role: 'Moderator'
    });
    this.createVisible.set(true);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.value;
    this.usrService.create({
      userId: v.userId,
      schoolId: Number(v.schoolId),
      role: v.role as PhaseTwoRole
    }).subscribe({
      next: (response) => {
        if (response.isSuccess) {
          this.toast.success(
            this.translate.instant('COMMON.SUCCESS'),
            response.message || this.translate.instant('USER_SCHOOL_ROLES.CREATE_SUCCESS'));
          this.createVisible.set(false);
          this.load();
        }
      }
    });
  }

  confirmRemove(row: UserSchoolRoleDetail, event: Event): void {
    this.confirm.confirm({
      target: event.target as EventTarget,
      message: this.translate.instant('USER_SCHOOL_ROLES.CONFIRM_REMOVE'),
      header: this.translate.instant('USER_SCHOOL_ROLES.REMOVE_CONFIRM_TITLE'),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.translate.instant('USER_SCHOOL_ROLES.REMOVE_ACCEPT'),
      rejectLabel: this.translate.instant('COMMON.CANCEL'),
      accept: () => this.remove(row.id)
    });
  }

  remove(id: number): void {
    this.usrService.delete(id).subscribe({
      next: (response) => {
        if (response.isSuccess) {
          this.toast.success(
            this.translate.instant('COMMON.SUCCESS'),
            response.message || this.translate.instant('USER_SCHOOL_ROLES.REMOVE_SUCCESS'));
          this.load();
        }
      }
    });
  }
}
