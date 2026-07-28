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
    this.loading.set(true); this.error.set(null);
    this.api.status().pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false))).subscribe({
      next: status => {
        this.status.set(status);
        if (status.isMicrosoftLinked && status.isDriveConfigured) {
          this.loadUploadCatalog();
          this.loadFolder();
        }
      },
      error: () => this.error.set('يلزم تسجيل الدخول بحساب Microsoft المدرسي لعرض ملفات الإنجاز.')
    });
  }

  connectMicrosoft(): void {
    this.error.set(null);
    this.api.linkAccount().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => this.load(),
      error: error => {
        const serverMessage = error?.error?.errors?.[0] ?? error?.error?.message;
        this.error.set(error?.name === 'EntraConfigurationError'
          ? 'خدمة Microsoft Entra غير مفعّلة على المنصة بعد. اطلب من مسؤول النظام إكمال إعداد Azure AD.'
          : serverMessage || 'تعذر ربط حساب Microsoft. تأكد من استخدام بريدك المدرسي.');
      }
    });
  }

  loadFolder(folderId?: string): void {
    this.currentFolderId.set(folderId);
    this.loading.set(true); this.error.set(null);
    this.api.items(folderId, this.search.trim() || undefined).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false))).subscribe({
      next: page => {
        this.items.set(page.items);
        this.loadBreadcrumb(folderId);
        this.api.recent().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: files => this.recent.set(files) });
      },
      error: () => this.error.set('تعذر تحميل ملفات المجلد. تحقق من جلسة Microsoft واتصال الإنترنت.')
    });
  }

  open(item: DriveItem): void {
    if (item.isFolder) { this.loadFolder(item.itemId); return; }
    this.api.preview(item.itemId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: preview => window.open(preview.previewUrl || preview.webUrl, '_blank', 'noopener'),
      error: () => this.error.set('تعذرت معاينة الملف. يمكنك فتحه من OneDrive.')
    });
  }

  chooseFile(input: HTMLInputElement): void { this.selectedFile.set(input.files?.[0] ?? null); }
  upload(): void {
    const file = this.selectedFile();
    if (!file || !this.selectedTaskId) return;
    this.uploadProgress.set(0); this.error.set(null);
    this.api.upload(file, this.selectedTaskId, this.currentFolderId()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: value => {
        if (typeof value === 'number') this.uploadProgress.set(value);
        else {
          this.selectedFile.set(null);
          this.selectedTaskId = null;
          this.uploadProgress.set(null);
          this.items.update(items => [value, ...items]);
          this.api.recent().subscribe({ next: files => this.recent.set(files) });
        }
      },
      error: () => { this.uploadProgress.set(null); this.error.set('فشل رفع الملف. تحقق من نوع الملف وحجمه ثم أعد المحاولة.'); }
    });
  }
  searchFolder(): void { this.loadFolder(this.currentFolderId()); }
  formatSize(size?: number): string { if (!size) return '—'; return size < 1024 * 1024 ? `${Math.ceil(size / 1024)} KB` : `${(size / 1024 / 1024).toFixed(1)} MB`; }
  private loadUploadCatalog(): void { this.api.evidenceTasks().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: catalog => this.uploadCatalog.set(catalog), error: () => this.error.set('تعذر تحميل قائمة المهام. لا يمكن رفع ملف دون اختيار المهمة.') }); }
  private loadBreadcrumb(folderId?: string): void { this.api.breadcrumb(folderId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: items => this.breadcrumbs.set(items) }); }
}
