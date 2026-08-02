import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { TeacherDriveApiService } from '../../services/teacher-drive-api.service';
import { DriveBreadcrumb, DriveItem, EvidenceUploadCatalog, RecentFile, TeacherDriveStatus } from '../../models/teacher-drive.models';

@Component({
  selector: 'app-teacher-evidence-files-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './teacher-evidence-files-page.component.html',
  styleUrls: ['./teacher-evidence-files-page.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TeacherEvidenceFilesPageComponent {
  private readonly api = inject(TeacherDriveApiService);
  private readonly destroyRef = inject(DestroyRef);
  readonly status = signal<TeacherDriveStatus | null>(null);
  readonly items = signal<DriveItem[]>([]);
  readonly breadcrumbs = signal<DriveBreadcrumb[]>([]);
  readonly recent = signal<RecentFile[]>([]);
  readonly uploadCatalog = signal<EvidenceUploadCatalog | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly selectedFile = signal<File | null>(null);
  readonly uploadProgress = signal<number | null>(null);
  readonly currentFolderId = signal<string | undefined>(undefined);
  selectedTaskId: number | null = null;
  search = '';

  constructor() { this.load(); }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.status().pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false))).subscribe({
      next: status => {
        this.status.set(status);
        if (status.isSchoolDriveEnabled && status.isFolderAssigned) {
          this.loadUploadCatalog();
          this.loadFolder();
        }
      },
      error: () => this.error.set('تعذر تحميل حالة ملفات الإنجاز. حدّث الصفحة أو تواصل مع إدارة المدرسة.')
    });
  }

  /** Message for the empty state, keyed off the server's connection state. */
  get connectionTitle(): string {
    switch (this.status()?.connectionState) {
      case 'NotATeacher': return 'ملفات الإنجاز متاحة لحساب المعلم فقط';
      case 'SchoolNotConfigured': return 'لم تُفعّل مدرستك ملفات الإنجاز بعد';
      default: return 'لم يتم تخصيص مجلد لك بعد';
    }
  }

  get connectionMessage(): string {
    switch (this.status()?.connectionState) {
      case 'NotATeacher': return 'هذه الصفحة مخصصة للمعلمين لرفع أدلة الإنجاز الخاصة بهم.';
      case 'SchoolNotConfigured': return 'يحتاج مدير المدرسة إلى ربط حساب Google Drive الخاص بالمدرسة أولاً.';
      default: return 'تواصل مع إدارة المدرسة لمنحك المجلد المخصص لك على Google Drive.';
    }
  }

  loadFolder(folderId?: string): void {
    this.currentFolderId.set(folderId);
    this.loading.set(true);
    this.error.set(null);
    this.api.items(folderId, this.search.trim() || undefined)
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false)))
      .subscribe({
        next: page => {
          this.items.set(page.items);
          this.loadBreadcrumb(folderId);
          this.refreshRecent();
        },
        error: () => this.error.set('تعذر تحميل ملفات المجلد. تحقق من اتصال الإنترنت ثم أعد المحاولة.')
      });
  }

  open(item: DriveItem): void {
    if (item.isFolder) { this.loadFolder(item.itemId); return; }
    this.openFile(item.itemId, item.name);
  }

  /**
   * Opens a file by downloading it through the API and handing the browser an object URL.
   * A direct Drive link cannot be used — the file belongs to the school's Google account.
   */
  openFile(itemId: string, fileName: string): void {
    this.error.set(null);
    this.api.content(itemId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const opened = window.open(url, '_blank', 'noopener');
        if (!opened) {
          // Pop-up blocked: fall back to a download so the click is never a dead end.
          const link = document.createElement('a');
          link.href = url;
          link.download = fileName;
          link.click();
        }
        // Revoking immediately would race the new tab's own load of the URL.
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
      },
      error: () => this.error.set('تعذر فتح الملف. قد يكون حُذف من Google Drive.')
    });
  }

  chooseFile(input: HTMLInputElement): void { this.selectedFile.set(input.files?.[0] ?? null); }

  upload(): void {
    const file = this.selectedFile();
    if (!file || !this.selectedTaskId) return;
    this.uploadProgress.set(0);
    this.error.set(null);
    this.api.upload(file, this.selectedTaskId, this.currentFolderId())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: value => {
          if (typeof value === 'number') { this.uploadProgress.set(value); return; }
          this.selectedFile.set(null);
          this.selectedTaskId = null;
          this.uploadProgress.set(null);
          this.items.update(items => [value, ...items]);
          this.refreshRecent();
        },
        error: () => {
          this.uploadProgress.set(null);
          this.error.set('فشل رفع الملف. تحقق من نوع الملف وحجمه ثم أعد المحاولة.');
        }
      });
  }

  searchFolder(): void { this.loadFolder(this.currentFolderId()); }

  formatSize(size?: number): string {
    if (!size) return '—';
    return size < 1024 * 1024 ? `${Math.ceil(size / 1024)} KB` : `${(size / 1024 / 1024).toFixed(1)} MB`;
  }

  private refreshRecent(): void {
    this.api.recent().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: files => this.recent.set(files) });
  }

  private loadUploadCatalog(): void {
    this.api.evidenceTasks().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: catalog => this.uploadCatalog.set(catalog),
      error: () => this.error.set('تعذر تحميل قائمة المهام. لا يمكن رفع ملف دون اختيار المهمة.')
    });
  }

  private loadBreadcrumb(folderId?: string): void {
    this.api.breadcrumb(folderId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: items => this.breadcrumbs.set(items)
    });
  }
}
