import { Component, OnInit, signal, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputGroupModule } from 'primeng/inputgroup';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';
import { ToastService } from '../../../core/services/toast.service';
import { TeachersService } from '../../../core/services/teachers.service';
import { UsersService } from '../../../core/services/users.service';
import { AuthService } from '../../../core/services/auth.service';
import { ListPageHeaderComponent } from '../../../shared/components/list-toolbar/list-page-header.component';
import { ListToolbarComponent } from '../../../shared/components/list-toolbar/list-toolbar.component';
import { ListToolbarFieldComponent } from '../../../shared/components/list-toolbar/list-toolbar-field.component';
import { TeacherListItem } from '../../../core/models/teacher.models';

@Component({
  selector: 'app-teachers-list',
  standalone: true,
  imports: [
    CommonModule, FormsModule, TranslateModule,
    TableModule, ButtonModule, InputTextModule, InputGroupModule,
    TagModule, TooltipModule, ConfirmDialogModule,
    ListPageHeaderComponent, ListToolbarComponent, ListToolbarFieldComponent
  ],
  providers: [ConfirmationService],
  templateUrl: './teachers-list.component.html',
  styleUrls: ['./teachers-list.component.css']
})
export class TeachersListComponent implements OnInit {
  private readonly teachersService = inject(TeachersService);
  private readonly usersService = inject(UsersService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmationService);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);

  readonly teachers = signal<TeacherListItem[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly searchTerm = signal('');

  // Permission gates (mirror docs/03 — Moderator/MainManager are
  // VIEW-only here; only roles with user-management write perms get
  // edit/delete, and ONLY roles allowed to create Instructors get the
  // "+ إضافة معلم" button).
  readonly canViewUsers = computed(() => this.auth.hasPermission('User.View'));
  readonly canCreateUsers = computed(() =>
    this.auth.hasPermission('User.Create')
    // MainManager + Moderator cannot create instructors per docs/03.
    // SuperAdmin / SchoolManager may.
    && !this.auth.hasRole('MainManager')
    && !this.auth.hasRole('Moderator'));
  readonly canEditUsers = computed(() =>
    this.auth.hasPermission('User.Edit')
    && !this.auth.hasRole('MainManager')
    && !this.auth.hasRole('Moderator'));
  readonly canDeleteUsers = computed(() =>
    this.auth.hasPermission('User.Delete')
    && !this.auth.hasRole('MainManager')
    && !this.auth.hasRole('Moderator'));

  ngOnInit(): void {
    this.load();
  }

  load(event?: TableLazyLoadEvent): void {
    const page = (event?.first ?? 0) / (event?.rows ?? 20) + 1;
    const pageSize = event?.rows ?? 20;
    this.loading.set(true);
    this.teachersService.list({
      page,
      pageSize,
      search: this.searchTerm() || undefined
    }).subscribe({
      next: (resp) => {
        if (resp.isSuccess && resp.data) {
          this.teachers.set(resp.data.items);
          this.totalCount.set(resp.data.totalCount);
        }
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  onSearch(): void { this.load(); }
  onClear(): void {
    this.searchTerm.set('');
    this.load();
  }

  goToCreate(): void { this.router.navigate(['/users/new'], { queryParams: { role: 'Instructor' } }); }
  goToEdit(t: TeacherListItem): void { this.router.navigate(['/users', t.userId, 'edit']); }
  goToProfile(t: TeacherListItem): void { this.router.navigate(['/teachers', t.userId]); }

  confirmDeactivate(t: TeacherListItem, event: Event): void {
    this.confirm.confirm({
      target: event.target as EventTarget,
      message: this.translate.instant('TEACHERS.DELETE_CONFIRM'),
      header: this.translate.instant('TEACHERS.DELETE'),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.translate.instant('COMMON.YES'),
      rejectLabel: this.translate.instant('COMMON.CANCEL'),
      accept: () => this.deactivateTeacher(t.userId)
    });
  }

  deactivateTeacher(userId: string): void {
    this.usersService.deactivate(userId).subscribe({
      next: (resp) => {
        if (resp.isSuccess) {
          this.toast.success(
            this.translate.instant('TEACHERS.DELETE_SUCCESS_TITLE'),
            resp.message || this.translate.instant('TEACHERS.DELETE_FAILED'));
          this.load();
        } else {
          this.toast.error(this.translate.instant('TEACHERS.DELETE_FAILED'), resp.message || '');
        }
      }
    });
  }
}
