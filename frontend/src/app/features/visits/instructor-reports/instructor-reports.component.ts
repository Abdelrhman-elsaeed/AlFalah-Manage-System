import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { VisitsService } from '../../../core/services/visits.service';
import { VisitsV2Service } from '../../../core/services/visits-v2.service';
import { forkJoin } from 'rxjs';
import { ListPageHeaderComponent } from '../../../shared/components/list-toolbar/list-page-header.component';

/**
 * Instructor-only report feed. It deliberately has no supervisor filters,
 * mutations, export, or generic visits-list request: the backend endpoint
 * returns only this instructor's Approved visits (D-36).
 */
@Component({
  selector: 'app-instructor-reports',
  standalone: true,
  imports: [
    CommonModule, TranslateModule, ButtonModule, TableModule, TagModule,
    TooltipModule, ListPageHeaderComponent
  ],
  templateUrl: './instructor-reports.component.html',
  styleUrls: ['./instructor-reports.component.css']
})
export class InstructorReportsComponent implements OnInit {
  private readonly visitsService = inject(VisitsService);
  private readonly visitsV2Service = inject(VisitsV2Service);
  private readonly router = inject(Router);

  readonly reports = signal<InstructorReportRow[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);

  ngOnInit(): void {
    this.load();
  }

  load(event?: TableLazyLoadEvent): void {
    const page = (event?.first ?? 0) / (event?.rows ?? 20) + 1;
    const pageSize = event?.rows ?? 20;
    const offset = (page - 1) * pageSize;
    const mergeWindow = Math.min(offset + pageSize, 100);
    this.loading.set(true);

    forkJoin({
      legacy: this.visitsService.listMyApprovedReports(1, mergeWindow),
      v2: this.visitsV2Service.list({ page: 1, pageSize: mergeWindow, status: 4 })
    }).subscribe({
      next: ({ legacy, v2 }) => {
        const legacyItems: InstructorReportRow[] = legacy.data?.items.map(item => ({
          id: item.id,
          experienceVersion: 1,
          visitCategoryLabelAr: item.visitCategoryLabelAr,
          visitSequenceLabelAr: item.visitSequenceLabelAr,
          visitDate: item.visitDate,
          statusLabelAr: item.statusLabelAr
        })) ?? [];
        const v2Items: InstructorReportRow[] = v2.data?.page.items.map(item => ({
          id: item.id,
          experienceVersion: 2,
          visitCategoryLabelAr: item.visitCategoryLabelAr,
          visitSequenceLabelAr: item.visitSequenceLabelAr,
          visitDate: item.visitDate,
          statusLabelAr: item.statusLabelAr
        })) ?? [];
        this.reports.set([...v2Items, ...legacyItems]
          .sort((a, b) => b.visitDate.localeCompare(a.visitDate))
          .slice(offset, offset + pageSize));
        this.totalCount.set((legacy.data?.totalCount ?? 0) + (v2.data?.page.totalCount ?? 0));
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  openReport(report: InstructorReportRow): void {
    if (report.experienceVersion === 2) {
      this.router.navigate(['/visits'], { queryParams: { visitId: report.id } });
      return;
    }
    this.router.navigate(['/instructor/reports', report.id]);
  }
}

interface InstructorReportRow {
  id: number;
  experienceVersion: 1 | 2;
  visitCategoryLabelAr: string;
  visitSequenceLabelAr: string;
  visitDate: string;
  statusLabelAr: string;
}
