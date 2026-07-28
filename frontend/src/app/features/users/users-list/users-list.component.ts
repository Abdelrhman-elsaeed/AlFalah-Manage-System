import { Component, OnInit, computed, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, FormGroup, Validators, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputGroupModule } from 'primeng/inputgroup';
import { InputGroupAddonModule } from 'primeng/inputgroupaddon';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { ClearableSelectComponent } from '../../../shared/components/clearable-select/clearable-select.component';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { ConfirmationService } from 'primeng/api';
import { ToastService } from '../../../core/services/toast.service';
import { UsersService } from '../../../core/services/users.service';
import { UserSchoolRolesService } from '../../../core/services/user-school-roles.service';
import { SchoolsService } from '../../../core/services/schools.service';
import { AuthService } from '../../../core/services/auth.service';
import { ListPageHeaderComponent } from '../../../shared/components/list-toolbar/list-page-header.component';
import { ListToolbarComponent } from '../../../shared/components/list-toolbar/list-toolbar.component';
import { ListToolbarFieldComponent } from '../../../shared/components/list-toolbar/list-toolbar-field.component';
import { UserListItem, PhaseTwoRole } from '../../../core/models/phase2.models';

/**
 * Role badges come off the API as raw Identity role names; the UI is Arabic, so
 * map every role the system seeds — global ones included — to its label key.
 */
const ROLE_LABEL_KEYS: Readonly<Record<string, string>> = {
  SuperAdmin: 'USERS.ROLE_SUPER_ADMIN',
  MainManager: 'USERS.ROLE_MAIN_MANAGER',
  SchoolManager: 'USERS.ROLE_SCHOOL_MANAGER',
  Secretary: 'USERS.ROLE_SECRETARY',
  Moderator: 'USERS.ROLE_MODERATOR',
  Instructor: 'USERS.ROLE_INSTRUCTOR'
};

@Component({
  selector: 'app-users-list',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ReactiveFormsModule, TranslateModule,
    TableModule, ButtonModule, InputTextModule, InputGroupModule, InputGroupAddonModule, InputNumberModule, ClearableSelectComponent,
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
  private readonly route = inject(ActivatedRoute);
  private readonly auth = inject(AuthService);
  private readonly translate = inject(TranslateService);

  /**
   * Set by the role sub-tab routes (`/users/school-managers`, `/users/moderators`,
   * `/users/secretaries`) via route data. Null on the plain `/users` tab, which is
   * the "everyone together" view and keeps the role dropdown.
   */
  readonly scopedRole = signal<PhaseTwoRole | null>(
    (this.route.snapshot.data['scopedRole'] as PhaseTwoRole | undefined) ?? null
  );

  users = signal<UserListItem[]>([]);
  totalCount = signal(0);
  loading = signal(false);
  searchTerm = signal('');
  roleFilter = signal<string | null>(this.scopedRole());
  isActiveFilter = signal<boolean | null>(null);

  /** SCHOOL_MANAGER | MODERATOR | SECRETARY | ALL — the i18n sub-key for this tab. */
  private readonly scopeKey = computed(() => {
    const role = this.scopedRole();
    return role ? role.replace(/([a-z])([A-Z])/g, '$1_$2').toUpperCase() : 'ALL';
  });
  readonly headerTitleKey = computed(() => `USERS.SCOPE.${this.scopeKey()}.TITLE`);
  readonly headerSubtitleKey = computed(() => `USERS.SCOPE.${this.scopeKey()}.SUBTITLE`);
  readonly createLabelKey = computed(() => `USERS.SCOPE.${this.scopeKey()}.NEW`);

  /**
   * D-24: adding a School Manager is a Main Manager privilege, so the scoped tab
   * hides the button instead of letting the POST come back 403.
   */
  readonly canCreate = computed(() =>
    this.scopedRole() !== 'SchoolManager'
    || this.auth.hasRole('MainManager')
    || this.auth.hasRole('SuperAdmin'));

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

  /** Assign-to-school dialog: the roles a UserSchoolRole may carry. */
  readonly roleOptions = computed(() => [
    ...(this.auth.hasRole('SchoolManager') && !this.auth.hasRole('MainManager') && !this.auth.hasRole('SuperAdmin')
      ? []
      : [{ label: this.translate.instant('USERS.ROLE_SCHOOL_MANAGER'), value: 'SchoolManager' as PhaseTwoRole }]),
    { label: this.translate.instant('USERS.ROLE_SECRETARY'), value: 'Secretary' as PhaseTwoRole },
    { label: this.translate.instant('USERS.ROLE_MODERATOR'), value: 'Moderator' },
    { label: this.translate.instant('USERS.ROLE_INSTRUCTOR'), value: 'Instructor' }
  ]);

  /** The "everyone" tab filters across every staff role, Secretary included. */
  readonly roleFilterOptions = [
    { label: this.translate.instant('USERS.ROLE_SCHOOL_MANAGER'), value: 'SchoolManager' },
    { label: this.translate.instant('USERS.ROLE_MODERATOR'), value: 'Moderator' },
    { label: this.translate.instant('USERS.ROLE_SECRETARY'), value: 'Secretary' },
    { label: this.translate.instant('USERS.ROLE_INSTRUCTOR'), value: 'Instructor' }
  ];

  readonly isActiveOptions = [
    { label: this.translate.instant('USERS.ACTIVE'), value: true },
    { label: this.translate.instant('USERS.INACTIVE'), value: false }
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

  roleLabel(role: string): string {
    const key = ROLE_LABEL_KEYS[role];
    return key ? this.translate.instant(key) : role;
  }

  goToCreate(): void {
    // Creating from a role tab pre-selects that role in the form.
    const role = this.scopedRole();
    this.router.navigate(['/users/new'], role ? { queryParams: { role } } : {});
  }
  goToEdit(user: UserListItem): void { this.router.navigate(['/users', user.userId, 'edit']); }

  confirmDeactivate(user: UserListItem, event: Event): void {
    this.confirm.confirm({
      target: event.target as EventTarget,
      message: this.translate.instant('USERS.CONFIRM_DEACTIVATE'),
      header: this.translate.instant('USERS.DEACTIVATE_CONFIRM_TITLE'),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.translate.instant('USERS.DEACTIVATE_ACCEPT'),
      rejectLabel: this.translate.instant('COMMON.CANCEL'),
      accept: () => this.deactivateUser(user.userId)
    });
  }

  deactivateUser(id: string): void {
    this.usersService.deactivate(id).subscribe({
      next: (response) => {
        if (response.isSuccess) {
          this.toast.success(this.translate.instant('USERS.DEACTIVATED'), response.message || this.translate.instant('USERS.DEACTIVATE_SUCCESS'));
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
          this.toast.success(this.translate.instant('COMMON.SUCCESS'), response.message || this.translate.instant('USERS.ASSIGN_SUCCESS'));
          this.assignDialogVisible.set(false);
          this.loadUsers();
        }
      }
    });
  }
}
