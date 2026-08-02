import { CommonModule, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';
import { extractHttpErrorMessage, readHttpErrorBody } from '../../core/http/http-error-message';
import {
  SchoolTimetable,
  TimetableCatalog,
  TimetableDay,
  TimetableEntry,
  TimetableEntryType,
  TimetablePdfColorMode,
  TimetableSemester,
  TimetableTeacher,
  TimetableVersion
} from '../../core/models/timetable.models';
import { TimetableService } from '../../core/services/timetable.service';
import { ToastService } from '../../core/services/toast.service';

interface CellAddress {
  teacherId: number;
  day: TimetableDay;
  period: number;
}

@Component({
  selector: 'app-school-timetable',
  standalone: true,
  imports: [CommonModule, DatePipe, FormsModule, ButtonModule, DialogModule, InputTextModule, TagModule],
  templateUrl: './school-timetable.component.html',
  styleUrls: ['./school-timetable.component.css']
})
export class SchoolTimetableComponent implements OnInit {
  private readonly api = inject(TimetableService);
  private readonly toast = inject(ToastService);

  readonly catalog = signal<TimetableCatalog | null>(null);
  readonly timetable = signal<SchoolTimetable | null>(null);
  readonly selectedYearId = signal<number | null>(null);
  readonly selectedSemester = signal<TimetableSemester>(1);
  readonly title = signal('');
  readonly entries = signal<Record<string, TimetableEntry>>({});
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly dirty = signal(false);
  readonly cellDialogVisible = signal(false);
  readonly createDialogVisible = signal(false);
  readonly versionsDialogVisible = signal(false);
  readonly grantsDialogVisible = signal(false);
  readonly versions = signal<TimetableVersion[]>([]);
  readonly versionsLoading = signal(false);

  readonly canManage = computed(() => this.timetable()?.capabilities.canManage ?? this.catalog()?.capabilities.canManage ?? false);
  readonly canDelegate = computed(() => this.catalog()?.capabilities.canDelegate ?? false);
  readonly isPersonalView = computed(() => {
    const teachers = this.catalog()?.teachers ?? [];
    return !this.canManage() && teachers.length === 1 && teachers[0].isCurrentUser;
  });
  readonly periodNumbers = computed(() => Array.from({ length: this.catalog()?.periodCount ?? 8 }, (_, index) => index + 1));

  selectedCell: CellAddress | null = null;
  draftType: 0 | TimetableEntryType = 0;
  draftClass = '';
  draftSubject = '';
  newTitle = '';
  grantedModeratorIds = new Set<string>();

  ngOnInit(): void {
    this.loadCatalog();
  }

  loadCatalog(): void {
    this.loading.set(true);
    this.api.getCatalog().subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) {
          this.loading.set(false);
          this.toast.error('تعذر تحميل بيانات الجدول', response.message || '');
          return;
        }
        this.catalog.set(response.data);
        const activeYear = response.data.academicYears.find(year => year.isActive) ?? response.data.academicYears[0];
        if (activeYear) this.selectedYearId.set(activeYear.id);
        this.grantedModeratorIds = new Set(response.data.moderators.filter(item => item.isGranted).map(item => item.userId));
        this.loadCurrent();
      },
      error: error => {
        this.loading.set(false);
        this.toast.error('تعذر تحميل بيانات الجدول', extractHttpErrorMessage(error) ?? '');
      }
    });
  }

  loadCurrent(): void {
    const academicYearId = this.selectedYearId();
    if (!academicYearId) {
      this.loading.set(false);
      return;
    }
    this.loading.set(true);
    this.api.getCurrent(academicYearId, this.selectedSemester()).subscribe({
      next: response => {
        this.loading.set(false);
        this.applyTimetable(response.data ?? null);
      },
      error: error => {
        this.loading.set(false);
        this.toast.error('تعذر تحميل الجدول', extractHttpErrorMessage(error) ?? '');
      }
    });
  }

  changeYear(value: string): void {
    this.selectedYearId.set(Number(value));
    this.loadCurrent();
  }

  changeSemester(value: string): void {
    this.selectedSemester.set(Number(value) as TimetableSemester);
    this.loadCurrent();
  }

  openCreate(): void {
    const year = this.catalog()?.academicYears.find(item => item.id === this.selectedYearId());
    const semester = this.catalog()?.semesters.find(item => item.value === this.selectedSemester());
    this.newTitle = `الجدول المدرسي - ${year?.nameAr ?? ''} - ${semester?.labelAr ?? ''}`.trim();
    this.createDialogVisible.set(true);
  }

  create(): void {
    const academicYearId = this.selectedYearId();
    if (!academicYearId || !this.newTitle.trim()) {
      this.toast.warn('أدخل عنوان الجدول', '');
      return;
    }
    this.saving.set(true);
    this.api.create({ academicYearId, semester: this.selectedSemester(), title: this.newTitle.trim() }).subscribe({
      next: response => {
        this.saving.set(false);
        if (response.data) this.applyTimetable(response.data);
        this.createDialogVisible.set(false);
        this.toast.success('تم إنشاء مسودة الجدول', 'ابدأ بإضافة الحصص ثم احفظ وانشر.');
      },
      error: error => {
        this.saving.set(false);
        this.toast.error('تعذر إنشاء الجدول', extractHttpErrorMessage(error) ?? '');
      }
    });
  }

  markTitle(value: string): void {
    this.title.set(value);
    this.dirty.set(true);
  }

  save(): void {
    const timetable = this.timetable();
    if (!timetable || !this.title().trim()) return;
    this.saving.set(true);
    this.api.save(timetable.id, {
      title: this.title().trim(),
      revision: timetable.revision,
      entries: Object.values(this.entries())
    }).subscribe({
      next: response => {
        this.saving.set(false);
        if (response.data) this.applyTimetable(response.data);
        this.toast.success('تم حفظ الجدول', 'تم إنشاء نسخة جديدة تلقائيًا.');
      },
      error: error => {
        this.saving.set(false);
        this.toast.error('تعذر حفظ الجدول', extractHttpErrorMessage(error) ?? '');
      }
    });
  }

  publish(): void {
    const timetable = this.timetable();
    if (!timetable) return;
    if (this.dirty()) {
      this.toast.warn('احفظ التعديلات أولًا', 'النشر يعتمد آخر نسخة محفوظة.');
      return;
    }
    this.saving.set(true);
    this.api.publish(timetable.id, timetable.revision).subscribe({
      next: response => {
        this.saving.set(false);
        if (response.data) this.applyTimetable(response.data);
        this.toast.success('تم نشر الجدول', 'أصبح ظاهرًا الآن لجميع المعلمين.');
      },
      error: error => {
        this.saving.set(false);
        this.toast.error('تعذر نشر الجدول', extractHttpErrorMessage(error) ?? '');
      }
    });
  }

  openCell(teacher: TimetableTeacher, day: TimetableDay, period: number): void {
    if (!this.canManage()) return;
    this.selectedCell = { teacherId: teacher.instructorProfileId, day, period };
    const entry = this.getEntry(teacher.instructorProfileId, day, period);
    this.draftType = entry?.entryType ?? 0;
    this.draftClass = entry?.classLabel ?? teacher.classes[0] ?? '';
    this.draftSubject = entry?.subject ?? teacher.subject ?? '';
    this.cellDialogVisible.set(true);
  }

  saveCell(): void {
    const cell = this.selectedCell;
    if (!cell) return;
    const key = this.cellKey(cell.teacherId, cell.day, cell.period);
    const next = { ...this.entries() };
    if (this.draftType === 0) {
      delete next[key];
    } else if (this.draftType === 2) {
      next[key] = { instructorProfileId: cell.teacherId, day: cell.day, period: cell.period, entryType: 2, classLabel: null, subject: null };
    } else {
      if (!this.draftClass.trim() || !this.draftSubject.trim()) {
        this.toast.warn('الفصل والمادة مطلوبان للحصة', '');
        return;
      }
      next[key] = {
        instructorProfileId: cell.teacherId,
        day: cell.day,
        period: cell.period,
        entryType: 1,
        classLabel: this.draftClass.trim(),
        subject: this.draftSubject.trim()
      };
    }
    this.entries.set(next);
    this.dirty.set(true);
    this.cellDialogVisible.set(false);
  }

  getEntry(teacherId: number, day: TimetableDay, period: number): TimetableEntry | null {
    return this.entries()[this.cellKey(teacherId, day, period)] ?? null;
  }

  cellText(entry: TimetableEntry | null): string {
    if (!entry) return '';
    return entry.entryType === 2 ? 'منتظر' : `${entry.classLabel}\n${entry.subject}`;
  }

  lessonCount(teacherId: number): number {
    return Object.values(this.entries()).filter(entry => entry.instructorProfileId === teacherId && entry.entryType === 1).length;
  }

  standbyCount(teacherId: number): number {
    return Object.values(this.entries()).filter(entry => entry.instructorProfileId === teacherId && entry.entryType === 2).length;
  }

  onDragStart(event: DragEvent, cell: CellAddress): void {
    if (!this.canManage() || !this.getEntry(cell.teacherId, cell.day, cell.period)) return;
    event.dataTransfer?.setData('text/timetable-cell', this.cellKey(cell.teacherId, cell.day, cell.period));
    if (event.dataTransfer) event.dataTransfer.effectAllowed = 'move';
  }

  onDragOver(event: DragEvent): void {
    if (!this.canManage()) return;
    event.preventDefault();
    if (event.dataTransfer) event.dataTransfer.dropEffect = 'move';
  }

  onDrop(event: DragEvent, target: CellAddress): void {
    if (!this.canManage()) return;
    event.preventDefault();
    const sourceKey = event.dataTransfer?.getData('text/timetable-cell');
    if (!sourceKey) return;
    const source = this.parseKey(sourceKey);
    if (!source) return;
    const targetKey = this.cellKey(target.teacherId, target.day, target.period);
    if (sourceKey === targetKey) return;
    const next = { ...this.entries() };
    const sourceEntry = next[sourceKey];
    const targetEntry = next[targetKey];
    if (!sourceEntry) return;
    next[targetKey] = { ...sourceEntry, instructorProfileId: target.teacherId, day: target.day, period: target.period };
    if (targetEntry) {
      next[sourceKey] = { ...targetEntry, instructorProfileId: source.teacherId, day: source.day, period: source.period };
    } else {
      delete next[sourceKey];
    }
    this.entries.set(next);
    this.dirty.set(true);
  }

  copyCell(event: ClipboardEvent, cell: CellAddress): void {
    const entry = this.getEntry(cell.teacherId, cell.day, cell.period);
    if (!entry) return;
    event.preventDefault();
    event.clipboardData?.setData('text/plain', entry.entryType === 2 ? 'منتظر' : `${entry.classLabel} | ${entry.subject}`);
  }

  pasteCell(event: ClipboardEvent, target: CellAddress): void {
    if (!this.canManage()) return;
    const text = event.clipboardData?.getData('text/plain').trim();
    if (!text) return;
    event.preventDefault();
    let entry: TimetableEntry;
    if (text === 'منتظر') {
      entry = { instructorProfileId: target.teacherId, day: target.day, period: target.period, entryType: 2, classLabel: null, subject: null };
    } else {
      const separator = text.indexOf('|');
      if (separator <= 0 || separator >= text.length - 1) {
        this.toast.warn('صيغة اللصق غير صحيحة', 'استخدم: الفصل | المادة، أو منتظر.');
        return;
      }
      entry = {
        instructorProfileId: target.teacherId,
        day: target.day,
        period: target.period,
        entryType: 1,
        classLabel: text.slice(0, separator).trim(),
        subject: text.slice(separator + 1).trim()
      };
    }
    this.entries.set({ ...this.entries(), [this.cellKey(target.teacherId, target.day, target.period)]: entry });
    this.dirty.set(true);
  }

  openVersions(): void {
    const timetable = this.timetable();
    if (!timetable) return;
    this.versionsDialogVisible.set(true);
    this.versionsLoading.set(true);
    this.api.getVersions(timetable.id).subscribe({
      next: response => {
        this.versions.set(response.data ?? []);
        this.versionsLoading.set(false);
      },
      error: error => {
        this.versionsLoading.set(false);
        this.toast.error('تعذر تحميل النسخ', extractHttpErrorMessage(error) ?? '');
      }
    });
  }

  restore(version: TimetableVersion): void {
    const timetable = this.timetable();
    if (!timetable || !window.confirm(`استرجاع النسخة رقم ${version.versionNumber}؟ سيتم حفظها كنسخة حالية جديدة.`)) return;
    this.saving.set(true);
    this.api.restore(timetable.id, version.versionNumber, timetable.revision).subscribe({
      next: response => {
        this.saving.set(false);
        if (response.data) this.applyTimetable(response.data);
        this.versionsDialogVisible.set(false);
        this.toast.success('تم استرجاع النسخة', '');
      },
      error: error => {
        this.saving.set(false);
        this.toast.error('تعذر استرجاع النسخة', extractHttpErrorMessage(error) ?? '');
      }
    });
  }

  openGrants(): void {
    this.grantedModeratorIds = new Set(this.catalog()?.moderators.filter(item => item.isGranted).map(item => item.userId) ?? []);
    this.grantsDialogVisible.set(true);
  }

  toggleGrant(userId: string, checked: boolean): void {
    const next = new Set(this.grantedModeratorIds);
    checked ? next.add(userId) : next.delete(userId);
    this.grantedModeratorIds = next;
  }

  saveGrants(): void {
    this.saving.set(true);
    this.api.updateGrants([...this.grantedModeratorIds]).subscribe({
      next: response => {
        this.saving.set(false);
        const catalog = this.catalog();
        if (catalog && response.data) this.catalog.set({ ...catalog, moderators: response.data });
        this.grantsDialogVisible.set(false);
        this.toast.success('تم تحديث التفويضات', '');
      },
      error: error => {
        this.saving.set(false);
        this.toast.error('تعذر تحديث التفويضات', extractHttpErrorMessage(error) ?? '');
      }
    });
  }

  importExcel(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    const timetable = this.timetable();
    input.value = '';
    if (!file || !timetable) return;
    if (!window.confirm('سيستبدل ملف Excel كل خانات الجدول الحالية. هل تريد المتابعة؟')) return;
    this.saving.set(true);
    this.api.import(timetable.id, timetable.revision, file).subscribe({
      next: response => {
        this.saving.set(false);
        if (response.data?.timetable) this.applyTimetable(response.data.timetable);
        const warnings = response.data?.warnings ?? [];
        warnings.length
          ? this.toast.warn(`تم الاستيراد مع ${warnings.length} ملاحظة`, warnings.slice(0, 2).join(' — '))
          : this.toast.success('تم استيراد الجدول', '');
      },
      error: error => {
        this.saving.set(false);
        this.toast.error('تعذر استيراد Excel', extractHttpErrorMessage(error) ?? '');
      }
    });
  }

  downloadTemplate(): void {
    const timetable = this.timetable();
    if (!timetable) return;
    this.api.downloadTemplate(timetable.id).subscribe({
      next: blob => this.saveBlob(blob, `نموذج-${this.title()}.xlsx`),
      error: async error => {
        await readHttpErrorBody(error);
        this.toast.error('تعذر تنزيل نموذج Excel', extractHttpErrorMessage(error) ?? '');
      }
    });
  }

  downloadPdf(colorMode: TimetablePdfColorMode): void {
    const timetable = this.timetable();
    if (!timetable) return;
    this.api.downloadPdf(timetable.id, colorMode).subscribe({
      next: blob => {
        const suffix = colorMode === 'color' ? 'ملون' : 'أبيض-وأسود';
        this.saveBlob(blob, `${this.title()}-A4-${suffix}.pdf`);
      },
      error: async error => {
        await readHttpErrorBody(error);
        this.toast.error('تعذر تنزيل PDF', extractHttpErrorMessage(error) ?? '');
      }
    });
  }

  selectedTeacher(): TimetableTeacher | null {
    return this.catalog()?.teachers.find(item => item.instructorProfileId === this.selectedCell?.teacherId) ?? null;
  }

  dayLabel(day: TimetableDay): string {
    return this.catalog()?.days.find(item => item.value === day)?.labelAr ?? '';
  }

  dayValue(value: number): TimetableDay {
    return value as TimetableDay;
  }

  private applyTimetable(timetable: SchoolTimetable | null): void {
    this.timetable.set(timetable);
    this.title.set(timetable?.title ?? '');
    const lookup: Record<string, TimetableEntry> = {};
    for (const entry of timetable?.entries ?? []) lookup[this.cellKey(entry.instructorProfileId, entry.day, entry.period)] = entry;
    this.entries.set(lookup);
    this.dirty.set(false);
  }

  private cellKey(teacherId: number, day: TimetableDay, period: number): string {
    return `${teacherId}:${day}:${period}`;
  }

  private parseKey(key: string): CellAddress | null {
    const [teacherId, day, period] = key.split(':').map(Number);
    if (!teacherId || !day || !period) return null;
    return { teacherId, day: day as TimetableDay, period };
  }

  private saveBlob(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName.replace(/[\\/:*?"<>|]/g, '-');
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
