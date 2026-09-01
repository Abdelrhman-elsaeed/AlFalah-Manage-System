import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CalendarModule } from 'primeng/calendar';
import { CardModule } from 'primeng/card';
import { CheckboxModule } from 'primeng/checkbox';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { EMPTY, catchError, combineLatest, filter, startWith, switchMap } from 'rxjs';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import {
  ClassroomDto,
  StudentAttendanceSheetDto,
  StudentAttendanceSheetRowDto
} from '../../../core/models/daily-operations.models';
import { DailyOperationsService } from '../../../core/services/daily-operations.service';

@Component({
  selector: 'app-attendance-sheet',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    ButtonModule,
    CalendarModule,
    CardModule,
    CheckboxModule,
    ConfirmDialogModule,
    DropdownModule,
    InputTextModule,
    ProgressSpinnerModule,
    TableModule,
    TagModule
  ],
  providers: [ConfirmationService],
  templateUrl: './attendance-sheet.component.html',
  styleUrl: './attendance-sheet.component.css'
})
export class AttendanceSheetComponent {
  private readonly api = inject(DailyOperationsService);
  private readonly messages = inject(MessageService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly destroyRef = inject(DestroyRef);

  readonly classroomControl = new FormControl<number | null>(null);
  readonly today = this.schoolLocalToday();
  readonly dateControl = new FormControl<Date>(this.today, { nonNullable: true });
  readonly classrooms = signal<readonly ClassroomDto[]>([]);
  readonly sheet = signal<StudentAttendanceSheetDto | null>(null);
  readonly selectedAbsentIds = signal<ReadonlySet<number>>(new Set<number>());
  readonly search = signal('');
  readonly loadingClassrooms = signal(true);
  readonly loadingSheet = signal(false);
  readonly saving = signal(false);
  readonly dirty = signal(false);
  readonly errorMessage = signal('');
  readonly removedDuringConflict = signal<readonly string[]>([]);
  private saveIdempotencyKey: string | null = null;

  readonly filteredRows = computed(() => {
    const rows = this.sheet()?.rows ?? [];
    const term = this.search().trim().toLocaleLowerCase('ar');
    if (!term) return rows;
    return rows.filter(row =>
      row.student.displayName.toLocaleLowerCase('ar').includes(term) ||
      row.student.studentNumber.toLocaleLowerCase('ar').includes(term));
  });
  readonly selectedAbsentCount = computed(() => this.selectedAbsentIds().size);
  readonly presentCount = computed(() => Math.max(0, (this.sheet()?.rows.length ?? 0) - this.selectedAbsentCount()));
  readonly hasExcusedRows = computed(() => this.sheet()?.rows.some(row => row.status === 'AbsentExcused') ?? false);

  constructor() {
    this.loadClassrooms();
    combineLatest([
      this.classroomControl.valueChanges.pipe(startWith(this.classroomControl.value)),
      this.dateControl.valueChanges.pipe(startWith(this.dateControl.value))
    ]).pipe(
      filter((selection): selection is [number, Date] => selection[0] !== null),
      switchMap(([classroomId, date]) => {
        this.loadingSheet.set(true);
        this.errorMessage.set('');
        this.sheet.set(null);
        this.dirty.set(false);
        this.saveIdempotencyKey = null;
        return this.api.getAttendanceSheet(this.toDateOnly(date), classroomId).pipe(
          catchError((error: HttpErrorResponse) => {
            this.loadingSheet.set(false);
            this.errorMessage.set(extractHttpErrorMessage(error) ?? 'تعذر تحميل كشف الحضور. حاول مرة أخرى.');
            return EMPTY;
          })
        );
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: response => {
        this.loadingSheet.set(false);
        if (!response.isSuccess || !response.data) {
          this.errorMessage.set(response.errors[0] ?? response.message ?? 'تعذر تحميل كشف الحضور.');
          return;
        }
        this.applySheet(response.data);
      }
    });
  }

  setSearch(event: Event): void {
    this.search.set((event.target as HTMLInputElement).value);
  }

  isAbsent(row: StudentAttendanceSheetRowDto): boolean {
    return this.selectedAbsentIds().has(row.student.id);
  }

  setAbsent(row: StudentAttendanceSheetRowDto, absent: boolean): void {
    if (row.status === 'AbsentExcused') return;
    this.selectedAbsentIds.update(current => {
      const next = new Set(current);
      if (absent) next.add(row.student.id);
      else next.delete(row.student.id);
      return next;
    });
    this.dirty.set(true);
    this.removedDuringConflict.set([]);
    this.saveIdempotencyKey = null;
  }

  confirmSave(): void {
    const sheet = this.sheet();
    if (!sheet || this.saving() || this.hasExcusedRows()) return;
    const count = this.selectedAbsentCount();
    const message = count === 0
      ? 'سيتم تسجيل جميع طلاب الفصل حاضرين.'
      : `سيتم تسجيل ${count} طالبًا غائبًا، وتسجيل بقية طلاب الفصل حاضرين.`;
    this.confirmation.confirm({
      header: 'تأكيد حفظ كشف الحضور',
      message,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'حفظ الكشف',
      rejectLabel: 'إلغاء',
      accept: () => this.save()
    });
  }

  statusLabel(row: StudentAttendanceSheetRowDto): string {
    if (row.status === 'AbsentExcused') return 'غائب بعذر';
    if (row.status === 'Absent') return 'غائب';
    return 'حاضر';
  }

  statusSeverity(row: StudentAttendanceSheetRowDto): 'success' | 'warning' | 'danger' {
    if (row.status === 'AbsentExcused') return 'warning';
    return row.status === 'Absent' ? 'danger' : 'success';
  }

  private loadClassrooms(): void {
    this.loadingClassrooms.set(true);
    this.api.getClassrooms().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: response => {
        this.loadingClassrooms.set(false);
        if (!response.isSuccess || !response.data) {
          this.errorMessage.set(response.errors[0] ?? response.message ?? 'تعذر تحميل الفصول.');
          return;
        }
        const active = response.data.items.filter(classroom => classroom.isActive);
        this.classrooms.set(active);
        if (active.length === 1) this.classroomControl.setValue(active[0].id);
      },
      error: error => {
        this.loadingClassrooms.set(false);
        this.errorMessage.set(extractHttpErrorMessage(error) ?? 'تعذر تحميل الفصول.');
      }
    });
  }

  private save(): void {
    const sheet = this.sheet();
    if (!sheet) return;
    this.saving.set(true);
    const idempotencyKey = this.saveIdempotencyKey ?? this.api.createIdempotencyKey();
    this.saveIdempotencyKey = idempotencyKey;
    const presentRosterIds = new Set(sheet.rows.map(row => row.student.id));
    const absentStudentIds = [...this.selectedAbsentIds()].filter(id => id > 0 && presentRosterIds.has(id));

    this.api.saveAttendanceSheet({
      date: sheet.date,
      classroomId: sheet.classroom.id,
      absentStudentIds,
      rosterRevision: sheet.rosterRevision
    }, idempotencyKey).subscribe({
      next: response => {
        this.saving.set(false);
        if (!response.isSuccess || !response.data) {
          const message = `${response.message} ${response.errors.join(' ')}`.toLocaleLowerCase('en');
          if (message.includes('changed by another request') || message.includes('roster')) {
            this.resolveRosterConflict();
            return;
          }
          this.messages.add({
            severity: 'error',
            summary: 'لم يتم الحفظ',
            detail: response.errors[0] ?? response.message ?? 'تعذر حفظ كشف الحضور.'
          });
          return;
        }
        this.applySheet(response.data);
        this.dirty.set(false);
        this.saveIdempotencyKey = null;
        this.messages.add({ severity: 'success', summary: 'تم الحفظ', detail: 'تم حفظ كشف الحضور' });
      },
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        if (error.status === 409) {
          this.resolveRosterConflict();
          return;
        }
        this.messages.add({
          severity: 'error',
          summary: 'لم يتم الحفظ',
          detail: extractHttpErrorMessage(error) ?? 'تعذر حفظ كشف الحضور. يمكنك إعادة المحاولة دون تغيير التحديد.'
        });
      }
    });
  }

  private resolveRosterConflict(): void {
    const current = this.sheet();
    if (!current) return;
    const previousRows = new Map(current.rows.map(row => [row.student.id, row.student.displayName]));
    const previousSelection = new Set(this.selectedAbsentIds());
    this.loadingSheet.set(true);
    this.api.getAttendanceSheet(current.date, current.classroom.id).subscribe({
      next: response => {
        this.loadingSheet.set(false);
        if (!response.isSuccess || !response.data) {
          this.messages.add({ severity: 'error', summary: 'تعارض في الكشف', detail: 'تغير كشف الفصل وتعذر تحميل النسخة الجديدة.' });
          return;
        }
        const newRosterIds = new Set(response.data.rows.map(row => row.student.id));
        const removed = [...previousSelection]
          .filter(id => !newRosterIds.has(id))
          .map(id => previousRows.get(id) ?? `طالب رقم ${id}`);
        this.sheet.set(response.data);
        this.selectedAbsentIds.set(new Set([...previousSelection].filter(id => newRosterIds.has(id))));
        this.removedDuringConflict.set(removed);
        this.dirty.set(true);
        this.saveIdempotencyKey = null;
        this.messages.add({
          severity: 'warn',
          summary: 'تم تحديث قائمة الفصل',
          detail: removed.length
            ? `خرج من الكشف: ${removed.join('، ')}. راجع التحديد ثم أكّد الحفظ من جديد.`
            : 'تغيرت قائمة الفصل. راجع التحديد ثم أكّد الحفظ من جديد.'
        });
      },
      error: () => {
        this.loadingSheet.set(false);
        this.messages.add({ severity: 'error', summary: 'تعارض في الكشف', detail: 'تعذر تحميل النسخة الجديدة من الكشف.' });
      }
    });
  }

  private applySheet(sheet: StudentAttendanceSheetDto): void {
    this.sheet.set(sheet);
    this.selectedAbsentIds.set(new Set(
      sheet.rows.filter(row => row.status === 'Absent').map(row => row.student.id)
    ));
    this.removedDuringConflict.set([]);
  }

  private toDateOnly(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private schoolLocalToday(): Date {
    const parts = new Intl.DateTimeFormat('en-CA', {
      timeZone: 'Asia/Riyadh',
      year: 'numeric',
      month: '2-digit',
      day: '2-digit'
    }).formatToParts(new Date());
    const value = (type: Intl.DateTimeFormatPartTypes) => Number(parts.find(part => part.type === type)?.value);
    return new Date(value('year'), value('month') - 1, value('day'));
  }
}
