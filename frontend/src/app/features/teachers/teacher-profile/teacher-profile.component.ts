import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { ChipsModule } from 'primeng/chips';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { ChartModule } from 'primeng/chart';
import { DialogModule } from 'primeng/dialog';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';
import { ToastService } from '../../../core/services/toast.service';
import { TeachersService } from '../../../core/services/teachers.service';
import { TeacherDriveAdminService } from '../../../core/services/teacher-drive-admin.service';
import { AuthService } from '../../../core/services/auth.service';
import {
  PUBLISHED_MAXIMUM,
  PublishedScoreDeltaPipe,
  PublishedScorePipe,
  toPublishedScore
} from '../../../shared/score-scale';
import {
  TeacherProfile,
  TeacherProgress,
  TeacherVisitProgress,
  TeacherVisitSummary
} from '../../../core/models/teacher.models';
import { AdminDriveFolderItem, DriveFolderMapping } from '../../../core/models/teacher-drive-admin.models';

@Component({
  selector: 'app-teacher-profile',
  standalone: true,
  imports: [
    CommonModule, FormsModule, TranslateModule,
    ButtonModule, InputTextModule, ChipsModule, TableModule, TagModule, TooltipModule, ChartModule,
    DialogModule, ConfirmDialogModule,
    PublishedScorePipe, PublishedScoreDeltaPipe
  ],
  providers: [ConfirmationService],
  templateUrl: './teacher-profile.component.html',
  styleUrls: ['./teacher-profile.component.css']
})
export class TeacherProfileComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly teachersService = inject(TeachersService);
  private readonly driveAdmin = inject(TeacherDriveAdminService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmationService);
  private readonly translate = inject(TranslateService);

  readonly userId = signal<string | null>(null);
  readonly profile = signal<TeacherProfile | null>(null);
  readonly visits = signal<TeacherVisitSummary[]>([]);
  readonly progress = signal<TeacherProgress | null>(null);
  readonly loading = signal(false);
  readonly editingClasses = signal(false);
  readonly savingClasses = signal(false);
  readonly editableClasses = signal<string[]>([]);

  // The radar chart payload — built reactively from `progress()`.
  readonly radarData = computed(() => {
    const p = this.progress();
    if (!p || p.axisLabels.length === 0 || p.visits.length === 0) return null;

    const labels = p.axisLabels.map(a => a.domainNameAr || a.domainCode);

    // High-contrast palette: avoid adjacent green shades so overlapping visits
    // remain distinguishable in both the chart and its RTL legend.
    const palette = [
      '#2563EB', // blue
      '#E76F51', // coral
      '#B7791F', // amber
      '#0F766E', // teal
      '#7C3AED', // violet
      '#C026D3'  // magenta
    ];

    const datasets = p.visits.map((v, idx) => {
      const color = palette[idx % palette.length];
      return {
        label: v.legendLabel,
        // D-UI-1: plotted on the one published scale (0–100).
        data: v.domainAverages.map(d => toPublishedScore(d.averageScore) ?? 0),
        borderColor: color,
        backgroundColor: hexToRgba(color, 0.11),
        borderWidth: 2.5,
        pointBackgroundColor: '#FFFFFF',
        pointBorderColor: color,
        pointBorderWidth: 2,
        pointRadius: 4,
        pointHoverRadius: 6,
        hoverBorderWidth: 3
      };
    });

    return { labels, datasets };
  });

  readonly radarOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        position: 'bottom',
        rtl: true,
        textDirection: 'rtl',
        labels: { color: '#0F172A', font: { family: 'Tajawal, Cairo, sans-serif', size: 12 } }
      },
      tooltip: {
        rtl: true,
        textDirection: 'rtl'
      }
    },
    scales: {
      r: {
        angleLines: { color: 'rgba(15, 23, 42, 0.12)' },
        grid: { color: 'rgba(15, 23, 42, 0.10)' },
        pointLabels: {
          color: '#0F172A',
          font: { family: 'Tajawal, Cairo, sans-serif', size: 12, weight: '600' as any }
        },
        ticks: {
          stepSize: 20,
          backdropColor: 'transparent',
          color: 'rgba(15, 23, 42, 0.55)',
          font: { size: 10 }
        },
        suggestedMin: 0,
        suggestedMax: PUBLISHED_MAXIMUM
      }
    }
  };

  // "+ زيارة جديدة" is for Moderator + School Manager only.
  readonly canCreateVisit = computed(() =>
    this.auth.hasPermission('Visit.Create')
    && (this.auth.hasRole('Moderator') || this.auth.hasRole('SchoolManager') || this.auth.hasRole('SuperAdmin'))
  );

  readonly visitCount = computed(() => this.visits().length);
  readonly firstToLastComparison = computed(() => this.progress()?.firstToLastComparison ?? null);
  readonly canManageTeaching = computed(() =>
    this.auth.hasPermission('User.Edit')
    && !this.auth.hasRole('MainManager')
    && !this.auth.hasRole('Moderator'));

  // ─── Evidence-files Google Drive folder grant ────────────────────────────
  // Keyed by InstructorProfile.Id (profile().instructorProfileId), NOT userId.
  readonly canViewDriveFolder = computed(() => this.auth.hasPermission('Instructor.View'));
  readonly canEditDriveFolder = computed(() => this.auth.hasPermission('Instructor.Edit'));
  readonly driveFolder = signal<DriveFolderMapping | null>(null);
  readonly driveFolderLoading = signal(false);
  readonly driveFolderSaving = signal(false);
  readonly folderBrowserVisible = signal(false);
  readonly folderBrowserLoading = signal(false);
  readonly folderBrowserLoadingMore = signal(false);
  readonly folderBrowserError = signal('');
  readonly folderBrowserFolders = signal<AdminDriveFolderItem[]>([]);
  readonly folderBrowserBreadcrumbs = signal<DriveFolderBreadcrumb[]>([]);
  readonly folderBrowserNextPageToken = signal<string | null>(null);
  readonly assigningFolderId = signal<string | null>(null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('userId');
    if (!id) {
      this.router.navigate(['/teachers']);
      return;
    }
    this.userId.set(id);
    this.loadAll(id);
  }

  loadAll(userId: string): void {
    this.loading.set(true);

    this.teachersService.getProfile(userId).subscribe({
      next: (resp) => {
        if (resp.isSuccess && resp.data) {
          this.profile.set(resp.data);
          if (resp.data.instructorProfileId && this.canViewDriveFolder()) {
            this.loadDriveFolder(resp.data.instructorProfileId);
          }
        } else {
          this.toast.error(this.translate.instant('TEACHERS.PROFILE_LOAD_FAILED'), resp.message || '');
        }
      }
    });

    this.teachersService.getVisits(userId).subscribe({
      next: (resp) => {
        if (resp.isSuccess && resp.data) {
          this.visits.set(resp.data);
        } else {
          this.toast.error(this.translate.instant('TEACHERS.VISITS_LOAD_FAILED'), resp.message || '');
        }
      }
    });

    this.teachersService.getProgress(userId).subscribe({
      next: (resp) => {
        if (resp.isSuccess && resp.data) {
          this.progress.set(resp.data);
        } else {
          this.toast.error(this.translate.instant('TEACHERS.PROGRESS_LOAD_FAILED'), resp.message || '');
        }
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  goBack(): void { this.router.navigate(['/teachers']); }

  // ─── Evidence-files Google Drive folder grant ────────────────────────────

  loadDriveFolder(instructorProfileId: number): void {
    this.driveFolderLoading.set(true);
    this.driveAdmin.getFolder(instructorProfileId).subscribe({
      next: resp => {
        this.driveFolderLoading.set(false);
        if (resp.isSuccess) this.driveFolder.set(resp.data ?? null);
      },
      error: () => this.driveFolderLoading.set(false)
    });
  }

  beginGrantDriveFolder(): void {
    this.folderBrowserVisible.set(true);
    this.folderBrowserBreadcrumbs.set([]);
    this.folderBrowserFolders.set([]);
    this.folderBrowserError.set('');
    this.loadFolderBrowser();
  }

  closeFolderBrowser(): void {
    if (!this.driveFolderSaving()) this.folderBrowserVisible.set(false);
  }

  openFolderBrowserItem(folder: AdminDriveFolderItem): void {
    this.loadFolderBrowser(folder.itemId);
  }

  openFolderBrowserBreadcrumb(index: number): void {
    const crumb = this.folderBrowserBreadcrumbs()[index];
    if (!crumb) return;
    this.folderBrowserBreadcrumbs.set(this.folderBrowserBreadcrumbs().slice(0, index + 1));
    this.loadFolderBrowser(crumb.itemId, false, true);
  }

  loadMoreFolders(): void {
    const currentFolderId = this.folderBrowserBreadcrumbs().at(-1)?.itemId;
    const pageToken = this.folderBrowserNextPageToken();
    if (currentFolderId && pageToken) {
      this.loadFolderBrowser(currentFolderId, true, true, pageToken);
    }
  }

  assignDriveFolder(folder: AdminDriveFolderItem): void {
    const instructorProfileId = this.profile()?.instructorProfileId;
    if (!instructorProfileId || folder.isAssigned) return;

    this.driveFolderSaving.set(true);
    this.assigningFolderId.set(folder.itemId);
    this.driveAdmin.upsertFolder(instructorProfileId, { rootItemId: folder.itemId }).subscribe({
      next: resp => {
        this.driveFolderSaving.set(false);
        this.assigningFolderId.set(null);
        if (resp.isSuccess && resp.data) {
          this.driveFolder.set(resp.data);
          this.folderBrowserVisible.set(false);
          this.toast.success(resp.message || 'تم منح المعلم صلاحية المجلد.');
        } else {
          this.toast.error('تعذر حفظ مجلد المعلم.', resp.message || '');
        }
      },
      error: err => {
        this.driveFolderSaving.set(false);
        this.assigningFolderId.set(null);
        this.toast.error('تعذر حفظ مجلد المعلم.', err?.error?.message || '');
      }
    });
  }

  confirmRevokeDriveFolder(): void {
    const folder = this.driveFolder();
    if (!folder) return;
    this.confirm.confirm({
      header: 'إلغاء تعيين المجلد',
      message: `إلغاء تعيين «${folder.folderDisplayName}» من هذا المعلم؟ ستظل الملفات المرفوعة محفوظة في مصفوفة المتابعة.`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'إلغاء التعيين',
      rejectLabel: 'رجوع',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.revokeDriveFolder()
    });
  }

  private revokeDriveFolder(): void {
    const instructorProfileId = this.profile()?.instructorProfileId;
    if (!instructorProfileId) return;

    this.driveFolderSaving.set(true);
    this.driveAdmin.revokeFolder(instructorProfileId).subscribe({
      next: resp => {
        this.driveFolderSaving.set(false);
        if (resp.isSuccess) {
          // Evidence already uploaded stays in the matrix — revoking only blocks new access.
          this.driveFolder.set(null);
          this.toast.success(resp.message || 'تم سحب صلاحية المجلد.');
        }
      },
      error: () => this.driveFolderSaving.set(false)
    });
  }

  private loadFolderBrowser(
    parentItemId?: string,
    append = false,
    keepBreadcrumbs = false,
    pageToken?: string
  ): void {
    const instructorProfileId = this.profile()?.instructorProfileId;
    if (!instructorProfileId) return;

    append ? this.folderBrowserLoadingMore.set(true) : this.folderBrowserLoading.set(true);
    this.folderBrowserError.set('');
    this.driveAdmin.browseFolders(instructorProfileId, parentItemId, pageToken).subscribe({
      next: resp => {
        this.folderBrowserLoading.set(false);
        this.folderBrowserLoadingMore.set(false);
        if (!resp.isSuccess || !resp.data) {
          this.folderBrowserError.set(resp.message || 'تعذر تحميل مجلدات Google Drive.');
          return;
        }

        const page = resp.data;
        this.folderBrowserFolders.set(append
          ? [...this.folderBrowserFolders(), ...page.folders]
          : page.folders);
        this.folderBrowserNextPageToken.set(page.nextPageToken ?? null);

        if (!append && !keepBreadcrumbs) {
          const current = this.folderBrowserBreadcrumbs();
          const existingIndex = current.findIndex(x => x.itemId === page.currentFolderId);
          this.folderBrowserBreadcrumbs.set(existingIndex >= 0
            ? current.slice(0, existingIndex + 1)
            : [...current, {
                itemId: page.currentFolderId,
                name: page.currentFolderName,
                isSchoolRoot: page.isSchoolRoot
              }]);
        }
      },
      error: err => {
        this.folderBrowserLoading.set(false);
        this.folderBrowserLoadingMore.set(false);
        this.folderBrowserError.set(err?.error?.message || 'تعذر تحميل مجلدات Google Drive.');
      }
    });
  }

  beginClassesEdit(): void {
    this.editableClasses.set([...(this.profile()?.classes ?? [])]);
    this.editingClasses.set(true);
  }

  cancelClassesEdit(): void {
    this.editingClasses.set(false);
    this.editableClasses.set([]);
  }

  saveClasses(): void {
    const profile = this.profile();
    if (!profile) return;

    const classes = [...new Set(this.editableClasses()
      .map(classLabel => classLabel.trim())
      .filter(Boolean))];

    this.savingClasses.set(true);
    this.teachersService.updateTeaching(profile.userId, {
      subject: profile.subject,
      stage: profile.stage,
      classes
    }).subscribe({
      next: response => {
        this.savingClasses.set(false);
        if (!response.isSuccess || !response.data) {
          this.toast.error(this.translate.instant('TEACHERS.CLASSES_SAVE_FAILED'), response.message || '');
          return;
        }

        this.profile.update(current => current
          ? { ...current, subject: response.data!.subject, stage: response.data!.stage, classes: response.data!.classes }
          : current);
        this.editingClasses.set(false);
        this.toast.success(this.translate.instant('TEACHERS.CLASSES_SAVE_SUCCESS'), response.message || '');
      },
      error: () => {
        this.savingClasses.set(false);
        this.toast.error(this.translate.instant('TEACHERS.CLASSES_SAVE_FAILED'));
      }
    });
  }

  startNewVisitForTeacher(): void {
    const id = this.userId();
    if (!id) return;
    // Reuse the existing visit-create flow with the instructor preselected
    // (visit-form already reads `instructorId` from queryParam if present).
    this.router.navigate(['/visits/new'], { queryParams: { instructorId: id } });
  }

  statusSeverity(status: number): 'success' | 'warning' | 'danger' | 'info' | 'secondary' {
    if (status === 1) return 'warning';   // Draft
    if (status === 3) return 'info';      // PendingApproval
    if (status === 4) return 'success';   // Approved
    if (status === 5) return 'danger';    // Rejected
    if (status === 6) return 'warning';   // Reopened
    return 'secondary';
  }

  readonly canViewVisit = computed(() => this.auth.hasPermission('Visit.View'));

  openVisit(visitId: number): void {
    if (!this.canViewVisit()) return;
    this.router.navigate(['/visits', visitId]);
  }

  trackVisit(_idx: number, v: TeacherVisitSummary): number { return v.id; }
  trackSeries(_idx: number, v: TeacherVisitProgress): number { return v.visitId; }

  deltaState(delta: number | null): 'up' | 'down' | 'same' | 'unavailable' {
    if (delta === null || delta === undefined) return 'unavailable';
    if (delta > 0) return 'up';
    if (delta < 0) return 'down';
    return 'same';
  }
}

interface DriveFolderBreadcrumb {
  itemId: string;
  name: string;
  isSchoolRoot: boolean;
}

/** Converts #RRGGBB + alpha into an rgba() string for chart fill tints. */
function hexToRgba(hex: string, alpha: number): string {
  const h = hex.replace('#', '');
  const r = parseInt(h.substring(0, 2), 16);
  const g = parseInt(h.substring(2, 4), 16);
  const b = parseInt(h.substring(4, 6), 16);
  return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}
