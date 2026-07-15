import { Component, OnInit, ViewChild, TemplateRef, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { ClearableSelectComponent } from '../../../shared/components/clearable-select/clearable-select.component';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';
import { ToastService } from '../../../core/services/toast.service';
import { AuthService } from '../../../core/services/auth.service';
import { ComplaintsService } from '../../../core/services/complaints.service';
import {
  Complaint,
  COMPLAINT_STATUSES,
  COMPLAINT_STATUS_SEVERITY
} from '../../../core/models/complaint.models';

/**
 * Phase 8 — School-management complaints page. The backend scopes the data
 * (SchoolManager = active school, SuperAdmin = support/global; MainManager and
 * Moderator → 403 and have no nav/route). Instructors submit from their viewed
 * approved report instead of receiving this management surface.
 * UI gates below are UX only.
 */
@Component({
  selector: 'app-complaints-list',
  standalone: true,
  imports: [
    CommonModule, FormsModule, TranslateModule,
    TableModule, TagModule, ButtonModule, DialogModule, DropdownModule,
    ClearableSelectComponent,
    InputTextareaModule, TooltipModule, ConfirmDialogModule
  ],
  providers: [ConfirmationService],
  templateUrl: './complaints-list.component.html',
  styleUrls: ['./complaints-list.component.css']
})
export class ComplaintsListComponent implements OnInit {
  private readonly complaintsService = inject(ComplaintsService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly confirm = inject(ConfirmationService);
  private readonly translate = inject(TranslateService);

  readonly complaints = signal<Complaint[]>([]);
  readonly loading = signal(false);
  readonly statusFilter = signal<number | null>(null);

  // Template refs for the status-filter dropdown's translated item labels.
  @ViewChild('statusItemTpl', { static: true })
    statusItemTpl!: TemplateRef<any>;
  @ViewChild('statusSelectedItemTpl', { static: true })
    statusSelectedItemTpl!: TemplateRef<any>;

  // Detail dialog
  readonly selected = signal<Complaint | null>(null);
  readonly detailVisible = signal(false);

  // Status-change state (inside the detail dialog)
  readonly nextStatus = signal<number | null>(null);
  readonly resolutionNote = signal('');
  readonly statusSaving = signal(false);

  // Reopen-visit dialog
  readonly reopenDialogVisible = signal(false);
  readonly reopenReason = signal('');
  readonly reopenSaving = signal(false);

  // Capability flags (UX only — backend enforces)
  readonly canHandle = computed(() => this.auth.hasPermission('Complaint.Manage'));
  readonly canDelete = computed(() => this.auth.hasPermission('Complaint.Delete'));
  readonly canReopenVisit = computed(() =>
    this.canHandle() && this.auth.hasPermission('Visit.Reopen'));

  readonly statusOptions = COMPLAINT_STATUSES;

  readonly nextStatusOptions = computed(() => {
    const c = this.selected();
    if (!c) return [] as Array<{ value: number; labelKey: string }>;
    return COMPLAINT_STATUSES.filter(s => c.allowedNextStatuses.includes(s.value));
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.complaintsService.list(this.statusFilter()).subscribe({
      next: (resp) => {
        if (resp.isSuccess && resp.data) {
          this.complaints.set(resp.data);
        } else {
          this.toast.error(this.t('COMPLAINTS.LOAD_FAILED'), resp.message || '');
        }
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.toast.error(this.t('COMPLAINTS.LOAD_FAILED'), err?.error?.message || '');
      }
    });
  }

  severity(status: number): 'info' | 'warning' | 'success' | 'danger' | 'secondary' {
    return COMPLAINT_STATUS_SEVERITY[status] ?? 'secondary';
  }

  openDetail(c: Complaint): void {
    this.selected.set(c);
    this.nextStatus.set(null);
    this.resolutionNote.set(c.resolutionNote ?? '');
    this.detailVisible.set(true);
  }

  closeDetail(): void {
    this.detailVisible.set(false);
    this.selected.set(null);
  }

  goToVisit(c: Complaint): void {
    this.router.navigate(['/visits', c.visitId]);
  }

  saveStatus(): void {
    const c = this.selected();
    const target = this.nextStatus();
    if (!c || target === null) return;

    this.statusSaving.set(true);
    this.complaintsService.updateStatus(c.id, {
      status: target,
      resolutionNote: this.resolutionNote().trim() || null
    }).subscribe({
      next: (resp) => {
        this.statusSaving.set(false);
        if (resp.isSuccess && resp.data) {
          this.selected.set(resp.data);
          this.nextStatus.set(null);
          this.toast.success(this.t('COMPLAINTS.STATUS_UPDATED_TITLE'), resp.message || '');
          this.load();
        } else {
          this.toast.error(this.t('COMPLAINTS.STATUS_UPDATE_FAILED'), resp.message || '');
        }
      },
      error: (err) => {
        this.statusSaving.set(false);
        this.toast.error(this.t('COMPLAINTS.STATUS_UPDATE_FAILED'), err?.error?.message || '');
      }
    });
  }

  openReopenDialog(): void {
    this.reopenReason.set('');
    this.reopenDialogVisible.set(true);
  }

  submitReopen(): void {
    const c = this.selected();
    const reason = this.reopenReason().trim();
    if (!c) return;
    if (!reason) {
      this.toast.warn(this.t('COMPLAINTS.REOPEN_REASON_REQUIRED'), '');
      return;
    }

    this.reopenSaving.set(true);
    this.complaintsService.reopenVisit(c.id, { reason }).subscribe({
      next: (resp) => {
        this.reopenSaving.set(false);
        this.reopenDialogVisible.set(false);
        if (resp.isSuccess && resp.data) {
          this.selected.set(resp.data);
          this.toast.success(this.t('COMPLAINTS.REOPEN_SUCCESS_TITLE'), resp.message || this.t('COMPLAINTS.REOPEN_SUCCESS_DESC'));
          this.load();
        } else {
          this.toast.error(this.t('COMPLAINTS.REOPEN_FAILED'), resp.message || '');
        }
      },
      error: (err) => {
        this.reopenSaving.set(false);
        this.toast.error(this.t('COMPLAINTS.REOPEN_FAILED'), err?.error?.message || '');
      }
    });
  }

  confirmDelete(c: Complaint): void {
    this.confirm.confirm({
      message: this.t('COMPLAINTS.DELETE_CONFIRM'),
      header: this.t('COMMON.CONFIRM'),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.t('COMMON.DELETE'),
      rejectLabel: this.t('COMMON.CANCEL'),
      accept: () => {
        this.complaintsService.delete(c.id).subscribe({
          next: (resp) => {
            if (resp.isSuccess) {
              this.toast.success(this.t('COMPLAINTS.DELETE_SUCCESS_TITLE'), resp.message || '');
              this.closeDetail();
              this.load();
            } else {
              this.toast.error(this.t('COMPLAINTS.DELETE_FAILED'), resp.message || '');
            }
          },
          error: (err) => this.toast.error(this.t('COMPLAINTS.DELETE_FAILED'), err?.error?.message || '')
        });
      }
    });
  }

  private t(key: string): string {
    return this.translate.instant(key);
  }
}
