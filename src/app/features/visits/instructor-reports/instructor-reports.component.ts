import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { VisitsService } from '../../../core/services/visits.service';
import { VisitListItem } from '../../../core/models/visit.models';
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
  private readonly router = inject(Router);

  readonly reports = signal<VisitListItem[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);

  ngOnInit(): void {
    this.load();
  }

  load(event?: TableLazyLoadEvent): void {
    const page = (event?.first ?? 0) / (event?.rows ?? 20) + 1;
    const pageSize = event?.rows ?? 20;
    this.loading.set(true);

    this.visitsService.listMyApprovedReports(page, pageSize).subscribe({
      next: (response) => {
        if (response.isSuccess && response.data) {
          this.reports.set(response.data.items);
          this.totalCount.set(response.data.totalCount);
        }
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  openReport(report: VisitListItem): void {
    this.router.navigate(['/instructor/reports', report.id]);
  }
}
