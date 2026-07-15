import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ChartData, ChartOptions } from 'chart.js';
import { Observable } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { ChartModule } from 'primeng/chart';
import { TagModule } from 'primeng/tag';
import { InstructorReport, VisitDetail } from '../../../core/models/visit.models';
import { ApiResponse } from '../../../core/models/api-response.model';
import { AuthService } from '../../../core/services/auth.service';
import { VisitsService } from '../../../core/services/visits.service';

@Component({
  selector: 'app-report-preview', standalone: true,
  imports: [CommonModule, TranslateModule, ButtonModule, TagModule, ChartModule],
  templateUrl: './report-preview.component.html', styleUrls: ['./report-preview.component.css']
})
export class ReportPreviewComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly visits = inject(VisitsService);
  private readonly auth = inject(AuthService);
  private readonly translate = inject(TranslateService);
  readonly report = signal<VisitDetail | null>(null);
  readonly loading = signal(true);
  readonly instructorOnly = computed(() => {
    const roles = this.auth.roles();
    return roles.includes('Instructor') && !roles.some(role =>
      ['SchoolManager', 'Moderator', 'MainManager', 'SuperAdmin'].includes(role));
  });
  readonly radarData = computed<ChartData<'radar'> | null>(() => {
    const domains = this.report()?.analysis?.domainAverages;
    return domains?.length ? {
      labels: domains.map(d => d.domainNameAr),
      datasets: [{ label: this.translate.instant('VISITS.DOMAIN_AVERAGE_CHART_LABEL'), data: domains.map(d => d.averageScore),
        borderColor: '#0F7132', backgroundColor: 'rgba(15,113,50,.18)',
        pointBackgroundColor: '#D4AF37', pointBorderColor: '#0F7132' }]
    } : null;
  });
  readonly radarOptions: ChartOptions<'radar'> = {
    responsive: true, maintainAspectRatio: false, plugins: { legend: { display: false } },
    scales: { r: { min: 0, max: 4, ticks: { stepSize: 1 }, beginAtZero: true } }
  };

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!id) { this.goBack(); return; }
    const request = (this.instructorOnly()
      ? this.visits.getInstructorReport(id)
      : this.visits.getById(id)) as Observable<ApiResponse<InstructorReport | VisitDetail>>;
    request.subscribe({
      next: response => {
        if (response.isSuccess && response.data) {
          const report = this.instructorOnly()
            ? mapInstructorReport(response.data as InstructorReport)
            : response.data as VisitDetail;
          this.report.set({ ...report, planFollowUps: report.planFollowUps ?? [] });
        }
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  goBack(): void {
    const id = this.report()?.id;
    this.router.navigate(this.instructorOnly() ? ['/instructor/reports', id ?? ''] : ['/visits', id ?? '']);
  }
  print(): void { window.print(); }
  goPlans(): void {
    const id = this.report()?.id;
    if (id) this.router.navigate(['/visits', id, 'improvement-plans']);
  }
}

function mapInstructorReport(r: InstructorReport): VisitDetail {
  return {
    id: r.visitId, schoolId: r.schoolId, schoolName: r.schoolName,
    instructorId: r.instructorId, instructorFullName: r.instructorFullName,
    createdByUserId: '', createdByFullName: '', rubricVersionId: r.rubricVersionId,
    rubricVersionNumber: r.rubricVersionNumber, visitCategory: r.visitCategory,
    visitCategoryLabelAr: r.visitCategoryLabelAr, visitSequence: r.visitSequence,
    visitSequenceLabelAr: r.visitSequenceLabelAr, status: r.status,
    statusLabelAr: r.statusLabelAr, visitDate: r.visitDate, subject: r.subject,
    gradeClass: r.gradeClass, lessonTitle: r.lessonTitle, presentCount: r.presentCount,
    absentCount: r.absentCount, notes: null, createdAt: r.visitDate, updatedAt: r.visitDate,
    submittedAt: r.submittedAt, approvedByFullName: r.approvedByFullName,
    approvedAt: r.approvedAt, isReadOnly: true, scores: r.scores, analysis: r.analysis,
    planFollowUps: r.planFollowUps ?? []
  };
}
