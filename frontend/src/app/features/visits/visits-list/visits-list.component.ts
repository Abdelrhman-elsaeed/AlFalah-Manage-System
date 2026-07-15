import { Component, OnInit, signal, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { ClearableSelectComponent } from '../../../shared/components/clearable-select/clearable-select.component';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';
import { CalendarModule } from 'primeng/calendar';
import { ToastService } from '../../../core/services/toast.service';
import { VisitsService, filenameFromContentDisposition } from '../../../core/services/visits.service';
import { AuthService } from '../../../core/services/auth.service';
import { ListPageHeaderComponent } from '../../../shared/components/list-toolbar/list-page-header.component';
import { ListToolbarComponent } from '../../../shared/components/list-toolbar/list-toolbar.component';
import { ListToolbarFieldComponent } from '../../../shared/components/list-toolbar/list-toolbar-field.component';
import {
  VISIT_STATUSES, VISIT_CATEGORIES,
  VisitListItem, VisitStatusOption, VisitCategoryOption
} from '../../../core/models/visit.models';

@Component({
  selector: 'app-visits-list',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ReactiveFormsModule, TranslateModule,
    TableModule, ButtonModule, InputTextModule, ClearableSelectComponent, TagModule, TooltipModule,
    ConfirmDialogModule, CalendarModule,
    ListPageHeaderComponent, ListToolbarComponent, ListToolbarFieldComponent
  ],
  providers: [ConfirmationService],
  templateUrl: './visits-list.component.html',
  styleUrls: ['./visits-list.component.css']
})
export class VisitsListComponent implements OnInit {
  private readonly visitsService = inject(VisitsService);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmationService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);

  readonly visits = signal<VisitListItem[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly firstRow = signal(0);

  readonly statuses = VISIT_STATUSES.map(option => ({
    ...option,
    label: this.translate.instant(option.labelKey)
  }));
  readonly categories = VISIT_CATEGORIES.map(option => ({
    ...option,
    label: this.translate.instant(option.labelKey)
  }));

  readonly statusFilter = signal<number | null>(null);
  readonly categoryFilter = signal<number | null>(null);
  readonly fromDate = signal<Date | null>(null);
  readonly toDate = signal<Date | null>(null);

  // D-41: per-row print loading + bulk export loading state.
  readonly pdfPrinting = signal<number | null>(null);
  readonly bulkExporting = signal(false);

  readonly canCreate = computed(() => this.auth.hasPermission('Visit.Create'));

  ngOnInit(): void {
    this.load();
  }

  load(event?: TableLazyLoadEvent): void {
    this.firstRow.set(event?.first ?? 0);
    const page = (event?.first ?? 0) / (event?.rows ?? 20) + 1;
    const pageSize = event?.rows ?? 20;
    this.loading.set(true);

    this.visitsService.list({
      page,
      pageSize,
      status: this.statusFilter() ?? undefined,
      visitCategory: this.categoryFilter() ?? undefined,
      fromDate: this.fromDate()?.toISOString(),
      toDate: this.toDate()?.toISOString()
    }).subscribe({
      next: (resp) => {
        if (resp.isSuccess && resp.data) {
          this.visits.set(resp.data.items);
          this.totalCount.set(resp.data.totalCount);
        }
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  onFilter(): void { this.load(); }
  onClearFilters(): void {
    this.statusFilter.set(null);
    this.categoryFilter.set(null);
    this.fromDate.set(null);
    this.toDate.set(null);
    this.load();
  }

  goToCreate(): void { this.router.navigate(['/visits/new']); }
  goToDetail(v: VisitListItem): void { this.router.navigate(['/visits', v.id]); }

  confirmDelete(v: VisitListItem, event: Event): void {
    this.confirm.confirm({
      target: event.target as EventTarget,
      message: this.t('VISITS.CONFIRM_DELETE'),
      header: this.t('VISITS.DELETE_CONFIRM_TITLE'),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.t('VISITS.DELETE_ACCEPT'),
      rejectLabel: this.t('COMMON.CANCEL'),
      accept: () => this.deleteVisit(v.id)
    });
  }

  deleteVisit(id: number): void {
    this.visitsService.softDelete(id).subscribe({
      next: (resp) => {
        if (resp.isSuccess) {
          this.toast.success(this.t('VISITS.DELETE_SUCCESS_TITLE'), resp.message || this.t('VISITS.DELETE_SUCCESS_DESC'));
          this.load();
        }
      }
    });
  }

  /**
   * D-41 / Task 3 — print/PDF download for a single visit row.
   * Triggers a Blob download via the same endpoint the detail page uses
   * (`/api/v1/visits/{id}/report/pdf`). All visible visit states download
   * through the same normal report layout.
   */
  printVisit(v: VisitListItem): void {
    if (this.pdfPrinting() !== null) return;
    this.pdfPrinting.set(v.id);
    this.visitsService.downloadReportPdf(v.id).subscribe({
      next: (resp) => {
        const blob = resp.body;
        if (!blob) {
          this.pdfPrinting.set(null);
          this.toast.error(this.t('VISITS.PDF_DOWNLOAD_FAILED_TITLE'), this.t('VISITS.PDF_DOWNLOAD_FAILED_DESC'));
          return;
        }
        const headerName = filenameFromContentDisposition(resp.headers.get('Content-Disposition'));
        const filename = headerName ?? this.visitsService.suggestedPdfFilename(v);
        triggerBlobDownload(blob, filename);
        this.toast.success(this.t('VISITS.PDF_DOWNLOAD_SUCCESS_TITLE'), this.t('VISITS.PDF_DOWNLOAD_SUCCESS_DESC'));
        this.pdfPrinting.set(null);
      },
      error: (err) => {
        this.pdfPrinting.set(null);
        const detail = extractApiErrorMessage(err) ?? 'VISITS.PDF_DOWNLOAD_FAILED_DESC';
        this.toast.error(this.t('VISITS.PDF_DOWNLOAD_FAILED_TITLE'), detail);
      }
    });
  }

  /**
   * D-41 / Task 6 — bulk export of every visit currently visible to the
   * caller (respecting all visibility gates — the backend reuses the same
   * scoped list query, then renders one PDF per visit and packages them
   * into a single application/zip).
   */
  exportAllZip(): void {
    if (this.bulkExporting()) return;
    this.bulkExporting.set(true);
    this.visitsService.exportAllZip({
      status: this.statusFilter() ?? undefined,
      visitCategory: this.categoryFilter() ?? undefined,
      fromDate: this.fromDate()?.toISOString(),
      toDate: this.toDate()?.toISOString()
    }).subscribe({
      next: (resp) => {
        const blob = resp.body;
        if (!blob) {
          this.bulkExporting.set(false);
          this.toast.error(this.t('VISITS.EXPORT_ALL_FAILED_TITLE'), this.t('VISITS.EXPORT_ALL_FAILED_DESC'));
          return;
        }
        const headerName = filenameFromContentDisposition(resp.headers.get('Content-Disposition'));
        const filename = headerName ?? 'visits.zip';
        triggerBlobDownload(blob, filename);
        this.toast.success(this.t('VISITS.EXPORT_ALL_SUCCESS_TITLE'), this.t('VISITS.EXPORT_ALL_SUCCESS_DESC'));
        this.bulkExporting.set(false);
      },
      error: (err) => {
        this.bulkExporting.set(false);
        const detail = extractApiErrorMessage(err) ?? 'VISITS.EXPORT_ALL_FAILED_DESC';
        this.toast.error(this.t('VISITS.EXPORT_ALL_FAILED_TITLE'), detail);
      }
    });
  }

  statusSeverity(status: string): 'success' | 'warning' | 'danger' | 'info' | 'secondary' {
    const s = Number(status);
    if (s === 1) return 'warning';       // Draft
    if (s === 3) return 'info';          // PendingApproval
    if (s === 4) return 'success';       // Approved
    if (s === 5) return 'danger';        // Rejected
    return 'secondary';
  }

  isDraft(status: string): boolean { return Number(status) === 1; }

  private t(key: string): string {
    return this.translate.instant(key);
  }
}

/**
 * Triggers a browser download of the given Blob using the supplied filename.
 * Allocates + releases an object URL around the click.
 */
function triggerBlobDownload(blob: Blob, filename: string): void {
  const url = window.URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  a.remove();
  window.URL.revokeObjectURL(url);
}

/**
 * D-41 — extract the Arabic ApiResponse `message` / `error` from a failed
 * HTTP error so toasts can surface the server's Arabic text verbatim.
 * Returns null if the body is empty / not parseable.
 */
function extractApiErrorMessage(err: any): string | null {
  if (!err) return null;
  if (typeof err.error === 'string') {
    try {
      const obj = JSON.parse(err.error);
      if (obj?.message) return String(obj.message);
      if (obj?.error) return String(obj.error);
    } catch {
      // not JSON — fallthrough
    }
  } else if (err.error && typeof err.error === 'object') {
    if (err.error.message) return String(err.error.message);
    if (err.error.error)   return String(err.error.error);
  }
  return null;
}
