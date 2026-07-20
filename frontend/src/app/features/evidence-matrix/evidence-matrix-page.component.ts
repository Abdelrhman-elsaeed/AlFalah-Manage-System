import { CommonModule, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { EvidenceMatrixApiService } from './services/evidence-matrix-api.service';
import { EvidenceCellFiles, EvidenceCellStatus, EvidenceMatrix, EvidenceMatrixCell, EvidenceMatrixFilter, EvidenceMatrixTeacherRow, EvidenceSubmissionFile } from './models/evidence-matrix.models';

const STATUS_LABELS: Record<EvidenceCellStatus, string> = {
  1: 'لم يُرفع', 2: 'مرفوع', 3: 'بانتظار المراجعة', 4: 'معتمد', 5: 'مرفوض', 6: 'مفقود من OneDrive'
};

@Component({
  selector: 'app-evidence-matrix-page',
  standalone: true,
  imports: [CommonModule, FormsModule, DatePipe],
  templateUrl: './evidence-matrix-page.component.html',
  styleUrls: ['./evidence-matrix-page.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EvidenceMatrixPageComponent {
  private readonly api = inject(EvidenceMatrixApiService);
  private readonly destroyRef = inject(DestroyRef);
  readonly matrix = signal<EvidenceMatrix | null>(null);
  readonly academicYears = signal<readonly { id: number; nameAr: string; isActive: boolean }[]>([]);
  readonly loading = signal(true);
  readonly exporting = signal(false);
  readonly error = signal<string | null>(null);
  readonly selectedCell = signal<EvidenceCellFiles | null>(null);
  readonly selectedRow = signal<EvidenceMatrixTeacherRow | null>(null);
  readonly selectedTask = signal<{ id: number; nameAr: string } | null>(null);
  readonly cellLoading = signal(false);
  readonly reviewNote = signal('');
  readonly categories = signal<string[]>([]);
  readonly schools = computed(() => [...new Map((this.matrix()?.rows || []).map(row => [row.schoolId, row.schoolName])).entries()].map(([id, name]) => ({ id, name })));
  readonly teachers = computed(() => (this.matrix()?.rows || []).map(row => ({ id: row.teacherId, name: row.teacherName, schoolId: row.schoolId })));
  readonly statusLabels = STATUS_LABELS;
  filter: EvidenceMatrixFilter = {};

  constructor() {
    this.api.academicYears().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: years => { this.academicYears.set(years); this.filter.academicYearId = years.find(year => year.isActive)?.id; this.load(); },
      error: () => { this.error.set('تعذر تحميل السنوات الدراسية.'); this.loading.set(false); }
    });
  }

  load(): void {
    this.loading.set(true); this.error.set(null);
    this.api.matrix(this.filter).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false))).subscribe({
      next: matrix => {
        this.matrix.set(matrix);
        if (!this.filter.academicYearId) this.filter.academicYearId = matrix.academicYear.id;
        if (!this.categories().length) this.categories.set([...new Set(matrix.tasks.map(task => task.category))]);
      },
      error: () => this.error.set('تعذر تحميل مصفوفة الأدلة.')
    });
  }

  resetFilters(): void {
    this.filter = { academicYearId: this.academicYears().find(year => year.isActive)?.id };
    this.load();
  }

  openCell(row: EvidenceMatrixTeacherRow, taskId: number): void {
    const task = this.matrix()?.tasks.find(item => item.id === taskId);
    if (!task || !this.filter.academicYearId) return;
    this.selectedRow.set(row); this.selectedTask.set(task); this.selectedCell.set(null); this.cellLoading.set(true); this.reviewNote.set('');
    this.api.files(row.teacherId, taskId, this.filter.academicYearId).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.cellLoading.set(false))).subscribe({
      next: files => this.selectedCell.set(files),
      error: () => this.error.set('تعذر تحميل ملفات المهمة.')
    });
  }

  closeCell(): void { this.selectedCell.set(null); this.selectedRow.set(null); this.selectedTask.set(null); }
  review(file: EvidenceSubmissionFile, status: 3 | 4): void {
    this.api.review(file.submissionId, status, this.reviewNote()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { const row = this.selectedRow(); const task = this.selectedTask(); if (row && task) this.openCell(row, task.id); this.load(); },
      error: () => this.error.set('تعذر تحديث مراجعة الملف.')
    });
  }
  export(format: 'excel' | 'pdf'): void {
    this.exporting.set(true);
    this.api.export(format, this.filter).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.exporting.set(false))).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a'); link.href = url; link.download = `evidence-matrix.${format === 'excel' ? 'xlsx' : 'pdf'}`; link.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.error.set('تعذر تصدير التقرير.')
    });
  }
  cellFor(row: EvidenceMatrixTeacherRow, taskId: number): EvidenceMatrixCell { return row.cells.find(cell => cell.taskId === taskId) ?? { taskId, status: 1, isChecked: false, activeFilesCount: 0 }; }
  trackTask = (_: number, task: { id: number }) => task.id;
  trackRow = (_: number, row: EvidenceMatrixTeacherRow) => row.teacherId;
}
