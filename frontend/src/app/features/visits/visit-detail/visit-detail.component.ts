import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { InputTextModule } from 'primeng/inputtext';
import { DialogModule } from 'primeng/dialog';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { MenuModule } from 'primeng/menu';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmationService, MenuItem } from 'primeng/api';
import { ToastService } from '../../../core/services/toast.service';
import { VisitsService, filenameFromContentDisposition } from '../../../core/services/visits.service';
import { ComplaintsService } from '../../../core/services/complaints.service';
import { AuthService } from '../../../core/services/auth.service';
import { extractHttpErrorMessage, readHttpErrorBody } from '../../../core/http/http-error-message';
import { PublishedScorePipe, formatPublishedTotal } from '../../../shared/score-scale';
import {
  InstructorReport,
  ReportViewStatus,
  VisitDetail
} from '../../../core/models/visit.models';

/** One header action: what it looks like and what it does. */
interface VisitPrimaryAction {
  icon: string;
  label: string;
  styleClass: string;
  run: () => void;
}

@Component({
  selector: 'app-visit-detail',
  standalone: true,
  imports: [
    CommonModule, FormsModule, TranslateModule,
    ButtonModule, TagModule, InputTextModule, InputTextareaModule,
    DialogModule, ConfirmDialogModule, MenuModule, TooltipModule,
    PublishedScorePipe
  ],
  providers: [ConfirmationService],
  templateUrl: './visit-detail.component.html',
  styleUrls: ['./visit-detail.component.css']
})
export class VisitDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly visitsService = inject(VisitsService);
  private readonly complaintsService = inject(ComplaintsService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmationService);
  private readonly translate = inject(TranslateService);

  readonly visit = signal<VisitDetail | null>(null);
  readonly loading = signal(false);
  readonly viewStatus = signal<ReportViewStatus | null>(null);
  readonly viewStatusLoading = signal(false);

  // Phase 6 / Stage 1: PDF download state
  readonly pdfDownloading = signal(false);

  // Reject / Reopen reason dialog state
  readonly reasonDialogVisible = signal(false);
  readonly reasonDialogMode = signal<'reject' | 'reopen' | null>(null);
  readonly reasonValue = signal('');
  readonly reasonActionLoading = signal(false);
  readonly complaintDialogVisible = signal(false);
  readonly complaintSubject = signal('');
  readonly complaintBody = signal('');
  readonly complaintSubmitting = signal(false);
  readonly reportViewRecorded = signal(false);

  // Role / capability signals
  readonly currentUserId = computed(() => this.auth.currentUser()?.userId);
  readonly isInstructor = computed(() => this.auth.hasRole('Instructor'));
  readonly isSchoolManager = computed(() => this.auth.hasRole('SchoolManager'));
  readonly isModerator = computed(() => this.auth.hasRole('Moderator'));
  readonly isGlobalAdmin = computed(() => this.auth.hasRole('SuperAdmin') || this.auth.hasRole('MainManager'));

  readonly canApprove = computed(() => this.auth.hasPermission('Visit.Approve'));
  readonly canReopen = computed(() => this.auth.hasPermission('Visit.Reopen'));
  readonly canEdit = computed(() => this.auth.hasPermission('Visit.Edit'));
  readonly canViewPlans = computed(() => this.auth.hasPermission('Plan.View'));
  readonly canCreateComplaint = computed(() => this.auth.hasPermission('Complaint.Create'));
  readonly isInstructorOnly = computed(() => {
    const roles = this.auth.roles();
    return roles.includes('Instructor')
      && !roles.some(role => ['SchoolManager', 'Moderator', 'MainManager', 'SuperAdmin'].includes(role));
  });

  readonly visitStatus = computed(() => Number(this.visit()?.status ?? 0));

  readonly isPendingApproval = computed(() => this.visitStatus() === 3);
  readonly isApproved = computed(() => this.visitStatus() === 4);
  readonly isRejected = computed(() => this.visitStatus() === 5);
  readonly isReopened = computed(() => this.visitStatus() === 6);

  // Instructor — only approved AND own visit can see the result
  readonly instructorSeesFullResult = computed(() =>
    this.isInstructor() &&
    this.isApproved() &&
    this.visit()?.instructorId === this.currentUserId()
  );
  readonly canSubmitComplaint = computed(() =>
    this.isInstructorOnly()
    && this.instructorSeesFullResult()
    && this.reportViewRecorded()
    && this.canCreateComplaint()
  );

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!id) {
      this.router.navigate(['/visits']);
      return;
    }
    this.load(id);
  }

  load(id: number): void {
    this.loading.set(true);
    this.reportViewRecorded.set(false);

    // D-36 close: instructors MUST go through the dedicated /report endpoint so
    // the backend gate (status == Approved AND own visit) is enforced and every
    // successful view is recorded as a ReportViewLog row. Managers / moderators /
    // global admins continue to use the manager endpoint.
    if (this.isInstructorOnly()) {
      this.visitsService.getInstructorReport(id).subscribe({
        next: (resp) => this.handleLoadResponse(resp, id, true),
        error: () => this.loading.set(false)
      });
    } else {
      this.visitsService.getById(id).subscribe({
        next: (resp) => this.handleLoadResponse(resp, id, false),
        error: () => this.loading.set(false)
      });
    }
  }

  private handleLoadResponse(
    resp: { isSuccess: boolean; data?: VisitDetail | InstructorReport | null; message?: string },
    id: number,
    instructorPath: boolean
  ): void {
    if (resp.isSuccess && resp.data) {
      const visit = instructorPath
        ? mapInstructorReportToVisitDetail(resp.data as InstructorReport)
        : (resp.data as VisitDetail);
      this.visit.set({ ...visit, planFollowUps: visit.planFollowUps ?? [] });
      // The instructor report endpoint writes ReportViewLog before returning
      // success. This explicit flag prevents the complaint surface from being
      // enabled from any non-recording or supervisor detail path.
      this.reportViewRecorded.set(instructorPath);
      // Manager / moderator: fetch report-view-status (irrelevant for instructors
      // — they are the viewer).
      if (!this.isInstructor() && this.isApproved()) {
        this.loadViewStatus(id);
      }
    } else {
      this.toast.error(
        this.translate.instant('COMMON.ERROR'),
        resp.message || this.translate.instant('VISITS.LOAD_FAILED'));
    }
    this.loading.set(false);
  }

  loadViewStatus(id: number): void {
    this.viewStatusLoading.set(true);
    this.visitsService.getReportViewStatus(id).subscribe({
      next: (resp) => {
        if (resp.isSuccess && resp.data) {
          this.viewStatus.set(resp.data);
        }
        this.viewStatusLoading.set(false);
      },
      error: () => this.viewStatusLoading.set(false)
    });
  }

  statusSeverity(status: string): 'success' | 'warning' | 'danger' | 'info' | 'secondary' {
    const s = Number(status);
    if (s === 1) return 'warning';          // Draft
    if (s === 3) return 'info';             // PendingApproval
    if (s === 4) return 'success';          // Approved
    if (s === 5) return 'danger';           // Rejected
    if (s === 6) return 'warning';          // Reopened
    return 'secondary';
  }

  scoreLabelAr(score: number | null): string {
    if (score === null || score === undefined) return '—';
    const key = `RUBRIC.SCORE_LABEL_${score}`;
    const translated = this.translate.instant(key);
    return translated === key ? String(score) : translated;
  }

  goEdit(): void {
    const v = this.visit();
    if (v) this.router.navigate(['/visits', v.id, 'edit']);
  }

  goImprovementPlans(): void {
    const v = this.visit();
    if (v) this.router.navigate(['/visits', v.id, 'improvement-plans']);
  }

  goReportPreview(): void {
    const v = this.visit();
    if (v) this.router.navigate(['/visit-reports', v.id, 'preview']);
  }

  goBack(): void {
    this.router.navigate(this.isInstructor() ? ['/instructor/reports'] : ['/visits']);
  }

  openComplaintDialog(): void {
    if (!this.visit() || !this.canSubmitComplaint()) return;
    this.complaintSubject.set('');
    this.complaintBody.set('');
    this.complaintDialogVisible.set(true);
  }

  cancelComplaintDialog(): void {
    if (!this.complaintSubmitting()) this.complaintDialogVisible.set(false);
  }

  submitComplaint(): void {
    const visit = this.visit();
    const subject = this.complaintSubject().trim();
    const body = this.complaintBody().trim();
    if (!visit || !this.canSubmitComplaint() || !subject || !body) return;

    this.complaintSubmitting.set(true);
    this.complaintsService.create(visit.id, { subject, body }).subscribe({
      next: response => {
        this.complaintSubmitting.set(false);
        if (!response.isSuccess) {
          this.toast.error(
            this.translate.instant('COMPLAINTS.SUBMIT_FAILED'),
            response.message || ''
          );
          return;
        }
        this.complaintDialogVisible.set(false);
        this.complaintSubject.set('');
        this.complaintBody.set('');
        this.toast.success(
          this.translate.instant('COMPLAINTS.SUBMIT_SUCCESS'),
          this.translate.instant('COMPLAINTS.SUBMIT_SUCCESS_DESC')
        );
      },
      error: (error) => {
        this.complaintSubmitting.set(false);
        this.toast.error(
          this.translate.instant('COMPLAINTS.SUBMIT_FAILED'),
          error?.error?.message || ''
        );
      }
    });
  }

  // Group scores by domain code for the read-only grid.
  groupedScores(): Array<{ domainCode: string; domainNameAr: string; scores: any[] }> {
    const v = this.visit();
    if (!v) return [];
    const map = new Map<string, { domainCode: string; domainNameAr: string; scores: any[] }>();
    for (const s of v.scores) {
      if (!map.has(s.domainCode)) {
        map.set(s.domainCode, { domainCode: s.domainCode, domainNameAr: s.domainNameAr, scores: [] });
      }
      map.get(s.domainCode)!.scores.push(s);
    }
    return Array.from(map.values())
      .sort((a, b) => a.domainCode.localeCompare(b.domainCode))
      .map(g => ({ ...g, scores: g.scores.sort((x, y) => x.standardCode.localeCompare(y.standardCode)) }));
  }

  /**
   * D-UI-1 — the visit total published on 0–100. The header cell used to carry
   * "78 / 100" with "3.12 / 4" beneath it: the same result on two scales.
   */
  publishedTotal(total: number, maximum: number): string {
    return formatPublishedTotal(total, maximum);
  }

  /**
   * Arabic performance level for a domain average. Replaces the second copy of
   * the score that each domain card used to print underneath the first.
   * Thresholds mirror docs/09 / VisitAnalysisEngine.MapPerformanceLevel.
   */
  performanceLevelAr(rawOutOfFour: number | null | undefined): string {
    if (rawOutOfFour === null || rawOutOfFour === undefined) return '—';
    if (rawOutOfFour >= 3.5) return 'متميز';
    if (rawOutOfFour >= 3.0) return 'جيد جداً';
    if (rawOutOfFour >= 2.5) return 'جيد';
    if (rawOutOfFour >= 2.0) return 'متحقق جزئياً';
    if (rawOutOfFour >= 1.0) return 'يحتاج تحسين';
    return 'غير مشاهد';
  }

  performanceLevelSeverity(level: string): 'success' | 'warning' | 'danger' | 'info' | 'secondary' {
    if (level === 'متميز') return 'success';
    if (level === 'جيد جداً') return 'success';
    if (level === 'جيد') return 'info';
    if (level === 'متحقق جزئياً') return 'warning';
    if (level === 'يحتاج تحسين') return 'warning';
    return 'danger';
  }

  trackDomain(_idx: number, d: { domainCode: string }): string { return d.domainCode; }
  trackStandard(_idx: number, s: { rubricStandardId: number }): number { return s.rubricStandardId; }

  // ─── Phase 5: approval actions ───────────────────────────────────────────

  confirmApprove(): void {
    const v = this.visit();
    if (!v) return;
    this.confirm.confirm({
      message: this.translate.instant('VISITS.APPROVE_CONFIRM_MESSAGE'),
      header: this.translate.instant('VISITS.APPROVE_CONFIRM_TITLE'),
      icon: 'pi pi-check-circle',
      acceptLabel: this.translate.instant('VISITS.APPROVE'),
      rejectLabel: this.translate.instant('COMMON.CANCEL'),
      accept: () => this.doApprove(v.id)
    });
  }

  doApprove(id: number): void {
    this.visitsService.approve(id).subscribe({
      next: (resp) => {
        if (resp.isSuccess && resp.data) {
          this.visit.set({ ...resp.data, planFollowUps: resp.data.planFollowUps ?? [] });
          this.toast.success(
            this.translate.instant('VISITS.APPROVE_SUCCESS_TITLE'),
            resp.message || this.translate.instant('VISITS.APPROVE_SUCCESS_DESC'));
          this.loadViewStatus(id);
        } else {
          this.toast.error(this.translate.instant('VISITS.APPROVE_FAILED'), resp.message || '');
        }
      }
    });
  }

  openRejectDialog(): void {
    this.reasonDialogMode.set('reject');
    this.reasonValue.set('');
    this.reasonDialogVisible.set(true);
  }

  openReopenDialog(): void {
    this.reasonDialogMode.set('reopen');
    this.reasonValue.set('');
    this.reasonDialogVisible.set(true);
  }

  cancelReasonDialog(): void {
    this.reasonDialogVisible.set(false);
    this.reasonDialogMode.set(null);
    this.reasonValue.set('');
  }

  submitReasonDialog(): void {
    const mode = this.reasonDialogMode();
    const reason = this.reasonValue().trim();
    if (!reason) {
      if (mode === 'reject') {
        this.toast.warn(this.translate.instant('VISITS.REJECT_REASON_REQUIRED'), '');
      } else {
        this.toast.warn(this.translate.instant('VISITS.REOPEN_REASON_REQUIRED'), '');
      }
      return;
    }
    const v = this.visit();
    if (!v || !mode) return;

    this.reasonActionLoading.set(true);
    if (mode === 'reject') {
      this.visitsService.reject(v.id, { reason }).subscribe({
        next: (resp) => this.handleReasonResponse(resp, 'reject'),
        error: () => this.reasonActionLoading.set(false)
      });
    } else {
      this.visitsService.reopen(v.id, { reason }).subscribe({
        next: (resp) => this.handleReasonResponse(resp, 'reopen'),
        error: () => this.reasonActionLoading.set(false)
      });
    }
  }

  private handleReasonResponse(resp: { isSuccess: boolean; data?: VisitDetail | null; message?: string }, mode: 'reject' | 'reopen'): void {
    this.reasonActionLoading.set(false);
    this.reasonDialogVisible.set(false);
    this.reasonDialogMode.set(null);
    this.reasonValue.set('');
    if (resp.isSuccess && resp.data) {
      this.visit.set({ ...resp.data, planFollowUps: resp.data.planFollowUps ?? [] });
      if (mode === 'reject') {
        this.toast.success(
          this.translate.instant('VISITS.REJECT_SUCCESS_TITLE'),
          resp.message || this.translate.instant('VISITS.REJECT_SUCCESS_DESC'));
      } else {
        this.toast.success(
          this.translate.instant('VISITS.REOPEN_SUCCESS_TITLE'),
          resp.message || this.translate.instant('VISITS.REOPEN_SUCCESS_DESC'));
        // After reopen, view-status is irrelevant until next approval
        this.viewStatus.set(null);
      }
    } else {
      if (mode === 'reject') {
        this.toast.error(this.translate.instant('VISITS.REJECT_FAILED'), resp.message || '');
      } else {
        this.toast.error(this.translate.instant('VISITS.REOPEN_FAILED'), resp.message || '');
      }
    }
  }

  // ─── Capability flags used by the template ───────────────────────────────

  /** Manager / admin can show Approve + Reject + Direct-edit on PendingApproval. */
  readonly canShowApproveActions = computed(() =>
    (this.isSchoolManager() || this.isGlobalAdmin()) &&
    this.canApprove() &&
    this.isPendingApproval()
  );

  /** Manager / admin can show Reopen on Approved. */
  readonly canShowReopenAction = computed(() =>
    (this.isSchoolManager() || this.isGlobalAdmin()) &&
    this.canReopen() &&
    this.isApproved()
  );

  /** Moderator can edit a Rejected or Reopened visit (creator or SM-enabled path). */
  readonly canModeratorEdit = computed(() =>
    this.isModerator() && (this.isRejected() || this.isReopened()) && this.canEdit()
  );

  /** Show edit button on Rejected/Reopened for the creator's school. */
  readonly canEditAfterRejectOrReopen = computed(() =>
    (this.isModerator() || this.isSchoolManager() || this.isGlobalAdmin()) &&
    (this.isRejected() || this.isReopened()) &&
    this.canEdit()
  );

  /** Keep the single edit action in the shared page toolbar for every editable state. */
  readonly canShowEditAction = computed(() => {
    const visit = this.visit();
    return !!visit && this.canEdit() && (
      visit.isReadOnly === false ||
      this.canDirectEdit() ||
      this.canEditAfterRejectOrReopen()
    );
  });

  /** SM direct-edit button (PendingApproval). */
  readonly canDirectEdit = computed(() =>
    (this.isSchoolManager() || this.isGlobalAdmin()) && this.isPendingApproval() && this.canEdit()
  );

  readonly canShowViewStatus = computed(() =>
    !this.isInstructor() &&
    this.isApproved() &&
    this.viewStatus() !== null
  );

  /** D-41 / Task 3 — PDF download is now allowed for ANY status. Backend stamps
   *  a "مسودة — غير معتمدة" watermark on non-Approved visits. The visibility
   *  gates (school-scope / moderator own-only / instructor own-only) remain
   *  enforced server-side. Front-end gate is UX only. */
  readonly canShowPdfDownload = computed(() =>
    this.visit() !== null &&
    (!this.isInstructor() || this.instructorSeesFullResult())
  );

  /** Phase 6 / Stage 1 — Download the visit's PDF report.
   *  Records a ReportViewLog on the backend (mirrors the /report endpoint).
   *  D-41: filename pattern is "{teacher} - {year} - {visitType}.pdf" —
   *  recovered from the Content-Disposition header, with a client-side
   *  fallback derived from the in-memory visit. */
  downloadReportPdf(): void {
    const v = this.visit();
    if (!v || this.pdfDownloading()) return;

    this.pdfDownloading.set(true);
    this.visitsService.downloadReportPdf(v.id).subscribe({
      next: (resp) => {
        const blob = resp.body;
        if (!blob) {
          this.pdfDownloading.set(false);
          this.toast.error(
            this.translate.instant('VISITS.PDF_DOWNLOAD_FAILED_TITLE'),
            this.translate.instant('VISITS.PDF_DOWNLOAD_FAILED_DESC'));
          return;
        }
        const headerName = filenameFromContentDisposition(resp.headers.get('Content-Disposition'));
        const filename = headerName ?? this.buildPdfFilename(v);

        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        a.remove();
        window.URL.revokeObjectURL(url);

        this.toast.success(
          'VISITS.PDF_DOWNLOAD_SUCCESS_TITLE',
          'VISITS.PDF_DOWNLOAD_SUCCESS_DESC'
        );
        this.pdfDownloading.set(false);
      },
      error: async (err) => {
        this.pdfDownloading.set(false);
        // The response is a blob, so the server's Arabic reason has to be read
        // out of it before it can be shown; without this the toast fell back to
        // a generic message and the actual reason was lost.
        await readHttpErrorBody(err);
        this.toast.error(
          'VISITS.PDF_DOWNLOAD_FAILED_TITLE',
          extractHttpErrorMessage(err) ?? 'VISITS.PDF_DOWNLOAD_FAILED_DESC');
      }
    });
  }

  /**
   * D-41 / Task 7 — fallback filename when the backend header is unavailable.
   * Mirrors the backend pattern `{teacher} - {year} - {visitType}.pdf`.
   */
  private buildPdfFilename(v: VisitDetail): string {
    const teacher = sanitizeForFilename(v.instructorFullName);
    const year = new Date(v.visitDate).getFullYear();
    const category = sanitizeForFilename(v.visitCategoryLabelAr);
    return `${teacher} - ${year} - ${category}.pdf`;
  }

  // ─── Action bar ──────────────────────────────────────────────────────────
  //
  // The header shows: back · download PDF · ONE primary action · overflow menu.
  // Everything else moves into the menu. The old bar rendered every permitted
  // action as a peer button — up to eight in a row, each given a different
  // colour by a :nth-of-type() rule, which both looked arbitrary and mis-tinted
  // buttons whenever an *ngIf removed one of its siblings.

  /**
   * The single action the page is asking the user to take, chosen by workflow
   * position: approve while a visit awaits approval, otherwise edit.
   */
  readonly primaryAction = computed<VisitPrimaryAction | null>(() => {
    if (this.canShowApproveActions()) {
      return {
        icon: 'pi pi-check-circle',
        label: this.translate.instant('VISITS.APPROVE'),
        styleClass: 'p-button-success',
        run: () => this.confirmApprove()
      };
    }
    if (this.canShowEditAction()) {
      return {
        icon: 'pi pi-pencil',
        label: this.translate.instant('COMMON.EDIT'),
        styleClass: '',
        run: () => this.goEdit()
      };
    }
    return null;
  });

  /** Everything permitted that is not the primary action or the PDF download. */
  readonly overflowActions = computed<MenuItem[]>(() => {
    const items: MenuItem[] = [];

    if (this.canShowPdfDownload()) {
      items.push({
        label: this.translate.instant('VISITS.REPORT_PREVIEW'),
        icon: 'pi pi-eye',
        command: () => this.goReportPreview()
      });
    }
    if (this.canViewPlans() && this.visit() && (this.isApproved() || !this.isInstructor())) {
      items.push({
        label: this.translate.instant('PLANS.TITLE'),
        icon: 'pi pi-list',
        command: () => this.goImprovementPlans()
      });
    }

    // Edit is only in the menu when approve took the primary slot.
    if (this.canShowApproveActions() && this.canShowEditAction()) {
      items.push({
        label: this.translate.instant('COMMON.EDIT'),
        icon: 'pi pi-pencil',
        command: () => this.goEdit()
      });
    }

    const stateChanges: MenuItem[] = [];
    if (this.canShowApproveActions()) {
      stateChanges.push({
        label: this.translate.instant('VISITS.REJECT'),
        icon: 'pi pi-undo',
        command: () => this.openRejectDialog()
      });
    }
    if (this.canShowReopenAction()) {
      stateChanges.push({
        label: this.translate.instant('VISITS.REOPEN'),
        icon: 'pi pi-history',
        command: () => this.openReopenDialog()
      });
    }
    if (this.canSubmitComplaint()) {
      stateChanges.push({
        label: this.translate.instant('COMPLAINTS.SUBMIT'),
        icon: 'pi pi-flag',
        styleClass: 'menu-item--danger',
        command: () => this.openComplaintDialog()
      });
    }

    if (items.length && stateChanges.length) items.push({ separator: true });
    return [...items, ...stateChanges];
  });

  readonly reasonDialogHeaderKey = computed(() => {
    const m = this.reasonDialogMode();
    if (m === 'reject') return 'VISITS.REJECT_REASON_TITLE';
    if (m === 'reopen') return 'VISITS.REOPEN_REASON_TITLE';
    return '';
  });

  readonly reasonDialogSubmitLabelKey = computed(() => {
    const m = this.reasonDialogMode();
    if (m === 'reject') return 'VISITS.REJECT';
    if (m === 'reopen') return 'VISITS.REOPEN';
    return 'COMMON.SAVE';
  });

  readonly reasonDialogPlaceholderKey = computed(() => {
    const m = this.reasonDialogMode();
    if (m === 'reject') return 'VISITS.REJECT_REASON_PLACEHOLDER';
    if (m === 'reopen') return 'VISITS.REOPEN_REASON_PLACEHOLDER';
    return '';
  });
}

/**
 * D-36 helper — maps an InstructorReport payload (the response shape of
 * GET /api/v1/visits/{id}/report) to the VisitDetail shape the existing
 * detail template expects. The /report payload is a SUBSET of VisitDetail:
 *  - `visitId` → `id`
 *  - no `createdByUserId` / `createdByFullName` / `notes` / `updatedAt` /
 *    `isReadOnly` / `reopened*` / `rejectionReason` / `reopenReason` fields
 *    (instructors aren't authorized to see those for other instructors).
 *
 * We project to VisitDetail so the SAME template renders for managers and
 * instructors; for fields that don't exist in InstructorReport we leave the
 * `*ngIf` to hide them (e.g. `v.rejectionReason` is shown only when present).
 */
function mapInstructorReportToVisitDetail(r: InstructorReport): VisitDetail {
  return {
    id: r.visitId,
    schoolId: r.schoolId,
    schoolName: r.schoolName,
    instructorId: r.instructorId,
    instructorFullName: r.instructorFullName,
    // Instructor is the only "creator" the instructor-facing DTO exposes;
    // we don't have that field, leave undefined and let the template hide it.
    createdByUserId: '',
    createdByFullName: '',
    rubricVersionId: r.rubricVersionId,
    rubricVersionNumber: r.rubricVersionNumber,
    visitCategory: r.visitCategory,
    visitCategoryLabelAr: r.visitCategoryLabelAr,
    visitSequence: r.visitSequence,
    visitSequenceLabelAr: r.visitSequenceLabelAr,
    status: r.status,
    statusLabelAr: r.statusLabelAr,
    visitDate: r.visitDate,
    subject: r.subject ?? null,
    gradeClass: r.gradeClass ?? null,
    lessonTitle: r.lessonTitle ?? null,
    presentCount: r.presentCount,
    absentCount: r.absentCount,
    notes: null,
    createdAt: r.visitDate,             // best-effort; backend doesn't expose for instructors
    updatedAt: r.visitDate,
    submittedAt: r.submittedAt ?? null,
    // Approval metadata — /report only exposes ApprovedByFullName + ApprovedAt.
    approvedByFullName: r.approvedByFullName ?? null,
    approvedAt: r.approvedAt ?? null,
    approvedByUserId: null,
    rejectionReason: null,
    reopenReason: null,
    reopenedByUserId: null,
    reopenedByFullName: null,
    reopenedAt: null,
    isReadOnly: true,                  // instructors cannot edit (Phase 5)
    scores: r.scores ?? [],
    analysis: r.analysis ?? null,
    planFollowUps: r.planFollowUps ?? []
  };
}

/**
 * D-41 / Task 7 — sanitizes an Arabic string for use as a filesystem filename.
 * Removes filesystem-illegal characters while preserving Arabic letters /
 * digits / spaces / hyphens. Returns "ملف" as a safe fallback.
 */
function sanitizeForFilename(input: string | null | undefined): string {
  if (!input) return 'ملف';
  let s = input
    .replace(/[\\/:*?"<>|\u0000-\u001F]/g, '')
    .replace(/\s+/g, ' ')
    .trim();
  if (!s) return 'ملف';
  if (s.length > 80) s = s.substring(0, 80).trim();
  return s;
}
