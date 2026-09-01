import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnDestroy, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { ActivatedRoute } from '@angular/router';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { catchError, from, map, mergeMap, of, switchMap, toArray } from 'rxjs';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import {
  AbsenceExcuseDto,
  AbsenceExcuseStatus,
  AbsenceExcuseType,
  AttachmentDto,
  OfficerExcuseQueueItem,
  StudentAttendanceHistoryDto,
  StudentAttendanceRecordDto
} from '../../../core/models/daily-operations.models';
import { GuardianStudentDto } from '../../../core/models/student-affairs-dashboard.models';
import { DailyOperationsService } from '../../../core/services/daily-operations.service';
import { downloadBlob, fileNameFromResponse } from '../../../core/utils/browser-download';

const MAX_PDF_BYTES = 10 * 1024 * 1024;
type ExcuseMode = 'guardian' | 'officer';

@Component({
  selector: 'app-excuses-management',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    ButtonModule,
    CardModule,
    DialogModule,
    DropdownModule,
    InputTextareaModule,
    ProgressSpinnerModule,
    TableModule,
    TagModule
  ],
  templateUrl: './excuses-management.component.html',
  styleUrl: './excuses-management.component.css'
})
export class ExcusesManagementComponent implements OnDestroy {
  private readonly api = inject(DailyOperationsService);
  private readonly messages = inject(MessageService);
  private readonly route = inject(ActivatedRoute);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly destroyRef = inject(DestroyRef);

  readonly mode = (this.route.snapshot.data['excuseMode'] ?? 'guardian') as ExcuseMode;
  readonly studentControl = new FormControl<number | null>(null);
  readonly guardianStudents = signal<readonly GuardianStudentDto[]>([]);
  readonly history = signal<StudentAttendanceHistoryDto | null>(null);
  readonly guardianLoading = signal(false);

  readonly excuseTypes: readonly { label: string; value: AbsenceExcuseType }[] = [
    { label: 'طبي', value: 'Medical' },
    { label: 'عائلي', value: 'Family' },
    { label: 'رسمي', value: 'Official' },
    { label: 'أخرى', value: 'Other' }
  ];
  readonly uploadForm = new FormGroup({
    excuseType: new FormControl<AbsenceExcuseType>('Medical', { nonNullable: true, validators: [Validators.required] }),
    notes: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(1000)] })
  });
  readonly uploadDialogVisible = signal(false);
  readonly uploadAttendance = signal<StudentAttendanceRecordDto | null>(null);
  readonly uploadFile = signal<File | null>(null);
  readonly submitting = signal(false);
  private uploadIdempotencyKey: string | null = null;

  readonly excusesDialogVisible = signal(false);
  readonly displayedExcuses = signal<readonly AbsenceExcuseDto[]>([]);
  readonly detailsLoading = signal(false);
  readonly previewUrl = signal<SafeResourceUrl | null>(null);
  readonly previewName = signal('');
  private rawPreviewUrl: string | null = null;

  readonly queue = signal<readonly OfficerExcuseQueueItem[]>([]);
  readonly queueLoading = signal(false);
  readonly queueError = signal('');
  readonly reviewDialogVisible = signal(false);
  readonly selectedQueueItem = signal<OfficerExcuseQueueItem | null>(null);
  readonly reviewNote = new FormControl('', { nonNullable: true });
  readonly rejectionReason = new FormControl('', { nonNullable: true, validators: [Validators.required] });
  readonly reviewing = signal(false);

  constructor() {
    if (this.mode === 'guardian') this.initializeGuardian();
    else this.loadOfficerQueue();

    this.uploadForm.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.uploadIdempotencyKey = null;
    });
  }

  ngOnDestroy(): void {
    this.revokePreview();
  }

  loadGuardianHistory(studentId: number | null): void {
    this.history.set(null);
    if (!studentId) return;
    this.guardianLoading.set(true);
    this.api.getStudentAttendanceHistory(studentId).subscribe({
      next: response => {
        this.guardianLoading.set(false);
        if (!response.isSuccess || !response.data) {
          this.messages.add({ severity: 'error', summary: 'تعذر تحميل الحضور', detail: response.errors[0] ?? response.message });
          return;
        }
        this.history.set(response.data);
      },
      error: error => {
        this.guardianLoading.set(false);
        this.showHttpError('تعذر تحميل الحضور', error);
      }
    });
  }

  canUpload(record: StudentAttendanceRecordDto): boolean {
    return record.status === 'Absent' && record.excuseStatus !== 'Pending' && record.excuseStatus !== 'Accepted';
  }

  openUpload(record: StudentAttendanceRecordDto): void {
    if (!this.canUpload(record)) return;
    this.uploadAttendance.set(record);
    this.uploadFile.set(null);
    this.uploadForm.reset({ excuseType: 'Medical', notes: '' });
    this.uploadIdempotencyKey = null;
    this.uploadDialogVisible.set(true);
  }

  selectPdf(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    input.value = '';
    if (!file) return;
    const extensionOk = file.name.toLocaleLowerCase('en').endsWith('.pdf');
    const mimeOk = !file.type || file.type === 'application/pdf';
    if (!extensionOk || !mimeOk) {
      this.messages.add({ severity: 'warn', summary: 'ملف غير صالح', detail: 'اختر ملف PDF فقط.' });
      return;
    }
    if (file.size === 0 || file.size > MAX_PDF_BYTES) {
      this.messages.add({ severity: 'warn', summary: 'حجم غير صالح', detail: 'يجب أن يكون ملف PDF أكبر من صفر وألا يتجاوز 10 MB.' });
      return;
    }
    this.uploadFile.set(file);
    this.uploadIdempotencyKey = null;
  }

  submitExcuse(): void {
    const attendance = this.uploadAttendance();
    const file = this.uploadFile();
    if (!attendance || !file || this.uploadForm.invalid || this.submitting()) return;
    const key = this.uploadIdempotencyKey ?? this.api.createIdempotencyKey();
    this.uploadIdempotencyKey = key;
    this.submitting.set(true);
    const value = this.uploadForm.getRawValue();
    this.api.submitExcuse(attendance.id, value.excuseType, value.notes, file, key).subscribe({
      next: response => {
        this.submitting.set(false);
        if (!response.isSuccess || !response.data) {
          this.messages.add({ severity: 'error', summary: 'تعذر إرسال العذر', detail: response.errors[0] ?? response.message });
          return;
        }
        this.uploadDialogVisible.set(false);
        this.uploadIdempotencyKey = null;
        this.messages.add({ severity: 'success', summary: 'تم إرسال العذر', detail: 'أُرسل العذر وهو الآن قيد مراجعة شؤون الطلاب.' });
        this.loadGuardianHistory(attendance.student.id);
      },
      error: error => {
        this.submitting.set(false);
        this.showHttpError('تعذر إرسال العذر', error);
      }
    });
  }

  openExcuses(attendanceId: number): void {
    this.excusesDialogVisible.set(true);
    this.displayedExcuses.set([]);
    this.detailsLoading.set(true);
    this.revokePreview();
    this.api.getExcuses(attendanceId).subscribe({
      next: response => {
        this.detailsLoading.set(false);
        if (!response.isSuccess || !response.data) {
          this.messages.add({ severity: 'error', summary: 'تعذر تحميل الأعذار', detail: response.errors[0] ?? response.message });
          return;
        }
        this.displayedExcuses.set(response.data);
      },
      error: error => {
        this.detailsLoading.set(false);
        this.showHttpError('تعذر تحميل الأعذار', error);
      }
    });
  }

  loadOfficerQueue(): void {
    this.queueLoading.set(true);
    this.queueError.set('');
    this.api.getAttendanceRecords({ pageNumber: 1, pageSize: 25, excuseStatus: 'Pending' }).pipe(
      switchMap(response => {
        if (!response.isSuccess || !response.data) {
          throw new Error(response.errors[0] ?? response.message ?? 'تعذر تحميل قائمة الأعذار.');
        }
        return from(response.data.items).pipe(
          mergeMap(attendance => this.api.getExcuses(attendance.id).pipe(
            map(excuseResponse => ({
              attendance,
              excuses: excuseResponse.isSuccess && excuseResponse.data ? excuseResponse.data : []
            })),
            catchError(() => of({ attendance, excuses: [] as readonly AbsenceExcuseDto[] }))
          ), 4),
          mergeMap(group => from(group.excuses
            .filter(excuse => excuse.status === 'Pending')
            .map(excuse => ({ attendance: group.attendance, excuse })))),
          toArray()
        );
      })
    ).subscribe({
      next: items => {
        this.queueLoading.set(false);
        this.queue.set(items);
      },
      error: error => {
        this.queueLoading.set(false);
        this.queueError.set(error instanceof Error ? error.message : 'تعذر تحميل قائمة الأعذار.');
      }
    });
  }

  openReview(item: OfficerExcuseQueueItem): void {
    this.selectedQueueItem.set(item);
    this.reviewNote.setValue('');
    this.rejectionReason.setValue('');
    this.revokePreview();
    this.reviewDialogVisible.set(true);
  }

  accept(): void {
    const item = this.selectedQueueItem();
    if (!item || item.excuse.status !== 'Pending' || this.reviewing()) return;
    this.reviewing.set(true);
    this.api.acceptExcuse(item.excuse.id, {
      reviewNote: this.reviewNote.value.trim() || null,
      rowVersion: item.excuse.rowVersion
    }).subscribe({
      next: response => this.handleReviewResponse(item, response),
      error: error => this.handleReviewError(item, error)
    });
  }

  reject(): void {
    const item = this.selectedQueueItem();
    const reason = this.rejectionReason.value.trim();
    if (!item || item.excuse.status !== 'Pending' || !reason || this.reviewing()) {
      this.rejectionReason.markAsTouched();
      return;
    }
    this.reviewing.set(true);
    this.api.rejectExcuse(item.excuse.id, {
      rejectionReason: reason,
      rowVersion: item.excuse.rowVersion
    }).subscribe({
      next: response => this.handleReviewResponse(item, response),
      error: error => this.handleReviewError(item, error)
    });
  }

  previewAttachment(excuse: AbsenceExcuseDto, attachment: AttachmentDto): void {
    this.api.downloadExcuseAttachment(excuse.id, attachment.id).subscribe({
      next: response => {
        const blob = response.body;
        if (!blob || !blob.type.toLocaleLowerCase('en').includes('pdf')) {
          this.messages.add({ severity: 'error', summary: 'تعذر عرض الملف', detail: 'الملف المستلم ليس PDF.' });
          return;
        }
        this.revokePreview();
        this.rawPreviewUrl = URL.createObjectURL(blob);
        this.previewUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(this.rawPreviewUrl));
        this.previewName.set(attachment.originalName);
      },
      error: error => this.showHttpError('تعذر عرض الملف', error)
    });
  }

  downloadAttachment(excuse: AbsenceExcuseDto, attachment: AttachmentDto): void {
    this.api.downloadExcuseAttachment(excuse.id, attachment.id).subscribe({
      next: response => {
        if (!response.body) return;
        downloadBlob(response.body, fileNameFromResponse(response, attachment.originalName));
      },
      error: error => this.showHttpError('تعذر تنزيل الملف', error)
    });
  }

  statusLabel(status: AbsenceExcuseStatus | null): string {
    if (status === 'Pending') return 'قيد المراجعة';
    if (status === 'Accepted') return 'مقبول';
    if (status === 'Rejected') return 'مرفوض';
    return 'لا يوجد عذر';
  }

  statusSeverity(status: AbsenceExcuseStatus | null): 'success' | 'info' | 'warning' | 'danger' {
    if (status === 'Accepted') return 'success';
    if (status === 'Pending') return 'warning';
    if (status === 'Rejected') return 'danger';
    return 'info';
  }

  excuseTypeLabel(type: AbsenceExcuseType): string {
    return this.excuseTypes.find(item => item.value === type)?.label ?? type;
  }

  formatDate(value: string): string {
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('ar-SA', { dateStyle: 'medium', timeStyle: 'short' }).format(date);
  }

  formatBytes(value: number): string {
    return value >= 1024 * 1024 ? `${(value / (1024 * 1024)).toFixed(2)} MB` : `${Math.max(1, Math.round(value / 1024))} KB`;
  }

  private initializeGuardian(): void {
    this.guardianLoading.set(true);
    this.api.getGuardianStudents().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: response => {
        this.guardianLoading.set(false);
        if (!response.isSuccess || !response.data) {
          this.messages.add({ severity: 'error', summary: 'تعذر تحميل الطلاب', detail: response.errors[0] ?? response.message });
          return;
        }
        const eligible = response.data.filter(link => link.canSubmitExcuses);
        this.guardianStudents.set(eligible);
        if (eligible.length) this.studentControl.setValue(eligible[0].student.id);
      },
      error: error => {
        this.guardianLoading.set(false);
        this.showHttpError('تعذر تحميل الطلاب', error);
      }
    });
    this.studentControl.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(id => this.loadGuardianHistory(id));
  }

  private handleReviewResponse(
    original: OfficerExcuseQueueItem,
    response: { readonly isSuccess: boolean; readonly message: string; readonly data?: AbsenceExcuseDto | null; readonly errors: readonly string[] }
  ): void {
    this.reviewing.set(false);
    if (!response.isSuccess || !response.data) {
      this.messages.add({ severity: 'warn', summary: 'لم يُحفظ القرار', detail: response.errors[0] ?? response.message });
      this.refreshWinningDecision(original);
      return;
    }
    const updated = response.data;
    this.selectedQueueItem.set({ ...original, excuse: updated });
    this.queue.update(items => items.filter(item => item.excuse.id !== updated.id));
    this.refreshAttendanceAfterReview(original, updated.status);
  }

  private handleReviewError(original: OfficerExcuseQueueItem, error: HttpErrorResponse): void {
    this.reviewing.set(false);
    if (error.status === 409) {
      this.refreshWinningDecision(original);
      return;
    }
    this.showHttpError('تعذر حفظ القرار', error);
  }

  private refreshWinningDecision(original: OfficerExcuseQueueItem): void {
    this.api.getExcuses(original.attendance.id).subscribe({
      next: response => {
        const latest = response.data?.find(excuse => excuse.id === original.excuse.id);
        if (!latest) return;
        this.selectedQueueItem.set({ ...original, excuse: latest });
        if (latest.status !== 'Pending') {
          this.queue.update(items => items.filter(item => item.excuse.id !== latest.id));
          this.messages.add({ severity: 'info', summary: 'سبق حسم العذر', detail: `القرار الأحدث: ${this.statusLabel(latest.status)}.` });
        }
      },
      error: error => this.showHttpError('تعذر تحديث قرار العذر', error)
    });
  }

  private refreshAttendanceAfterReview(original: OfficerExcuseQueueItem, decision: AbsenceExcuseStatus): void {
    this.api.getAttendanceRecords({
      pageNumber: 1,
      pageSize: 10,
      fromDate: original.attendance.date,
      toDate: original.attendance.date,
      studentId: original.attendance.student.id
    }).subscribe({
      next: response => {
        const actual = response.data?.items.find(record => record.id === original.attendance.id);
        const actualLabel = actual?.status === 'AbsentExcused' ? 'غائب بعذر' : actual?.status === 'Absent' ? 'غائب' : actual?.status === 'Present' ? 'حاضر' : 'تم تحديث السجل';
        this.messages.add({
          severity: 'success',
          summary: decision === 'Accepted' ? 'تم قبول العذر' : 'تم رفض العذر',
          detail: `حالة سجل الحضور الفعلية: ${actualLabel}.`
        });
      },
      error: () => this.messages.add({ severity: 'success', summary: 'تم حفظ القرار', detail: 'تم تحديث قائمة الأعذار.' })
    });
  }

  private revokePreview(): void {
    if (this.rawPreviewUrl) URL.revokeObjectURL(this.rawPreviewUrl);
    this.rawPreviewUrl = null;
    this.previewUrl.set(null);
    this.previewName.set('');
  }

  private showHttpError(summary: string, error: unknown): void {
    this.messages.add({ severity: 'error', summary, detail: extractHttpErrorMessage(error) ?? 'حاول مرة أخرى.' });
  }
}
