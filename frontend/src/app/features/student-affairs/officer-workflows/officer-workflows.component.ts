import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { Observable, finalize } from 'rxjs';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import { ApiResponse, PagedResult } from '../../../core/models/api-response.model';
import { ClassroomDto, StudentStatsDto } from '../../../core/models/daily-operations.models';
import {
  AssignableSocialWorkerDto,
  ClassroomEntryPermitDto,
  OperationalRecordDto
} from '../../../core/models/officer-operations.models';
import { ReferralDto, SummonDto } from '../../../core/models/phase5.models';
import { DailyOperationsService } from '../../../core/services/daily-operations.service';
import { OfficerOperationsService } from '../../../core/services/officer-operations.service';

type WorkflowMode = 'permits' | 'referrals' | 'automation' | 'records';

@Component({
  selector: 'app-officer-workflows',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, ButtonModule, CardModule, DropdownModule,
    InputTextModule, InputTextareaModule, ProgressSpinnerModule, TableModule, TagModule
  ],
  templateUrl: './officer-workflows.component.html',
  styleUrl: './officer-workflows.component.css'
})
export class OfficerWorkflowsComponent {
  private readonly api = inject(OfficerOperationsService);
  private readonly directoryApi = inject(DailyOperationsService);
  private readonly route = inject(ActivatedRoute);
  private readonly messages = inject(MessageService);
  private readonly destroyRef = inject(DestroyRef);

  readonly mode = (this.route.snapshot.data['workflowMode'] ?? 'records') as WorkflowMode;
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly total = signal(0);
  readonly pageSize = signal(20);

  readonly studentSearch = new FormControl('', { nonNullable: true });
  readonly classroomControl = new FormControl<number | null>(null);
  readonly classrooms = signal<readonly ClassroomDto[]>([]);
  readonly classroomsLoading = signal(false);
  readonly studentOptions = signal<readonly StudentStatsDto[]>([]);
  readonly studentLookupLoading = signal(false);
  readonly studentLookupHint = signal('اختر الفصل لعرض طلابه، أو ابحث بالاسم أو الرقم في جميع الفصول.');
  readonly selectedStudent = signal<StudentStatsDto | null>(null);

  readonly permits = signal<readonly ClassroomEntryPermitDto[]>([]);
  readonly selectedPermit = signal<ClassroomEntryPermitDto | null>(null);
  readonly revokeReason = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(1000)]
  });
  readonly permitForm = new FormGroup({
    studentId: new FormControl<number | null>(null, Validators.required),
    reason: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(1000)] }),
    validFrom: new FormControl(this.localDateTime(new Date()), { nonNullable: true, validators: [Validators.required] }),
    validUntil: new FormControl(this.localDateTime(new Date(Date.now() + 20 * 60_000)), { nonNullable: true, validators: [Validators.required] })
  });

  readonly referrals = signal<readonly ReferralDto[]>([]);
  readonly referralForm = new FormGroup({
    studentId: new FormControl<number | null>(null, Validators.required),
    reason: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(1000)] }),
    priority: new FormControl('Normal', { nonNullable: true, validators: [Validators.required] }),
    workerId: new FormControl('', { nonNullable: true }),
    assignmentReason: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(1000)] })
  });
  readonly priorities = [{ label: 'عادية', value: 'Normal' }, { label: 'عالية', value: 'High' }, { label: 'حرجة', value: 'Critical' }];
  readonly workers = signal<readonly AssignableSocialWorkerDto[]>([]);
  readonly workersLoading = signal(false);
  readonly workersError = signal('');
  readonly selectedReferral = signal<ReferralDto | null>(null);
  readonly assignmentForm = new FormGroup({
    workerId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    reason: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(1000)] })
  });
  private referralIdempotencyKey: string | null = null;

  readonly reviews = signal<readonly SummonDto[]>([]);
  readonly selectedReview = signal<SummonDto | null>(null);
  readonly reviewForm = new FormGroup({
    decision: new FormControl('Retain', { nonNullable: true, validators: [Validators.required] }),
    rationale: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(1000)] })
  });
  readonly decisions = [{ label: 'الإبقاء', value: 'Retain' }, { label: 'الإلغاء', value: 'Cancel' }, { label: 'الإغلاق', value: 'Close' }];

  readonly records = signal<readonly OperationalRecordDto[]>([]);
  readonly selectedRecord = signal<OperationalRecordDto | null>(null);
  readonly recordType = new FormControl('morning-delays', { nonNullable: true });
  readonly recordSearch = new FormControl('', { nonNullable: true });
  readonly recordTypes = [
    { label: 'التأخر الصباحي', value: 'morning-delays', icon: 'pi-clock', tone: 'amber', hint: 'الوصول بعد وقت الاصطفاف' },
    { label: 'التأخر عن الحصص', value: 'session-delays', icon: 'pi-stopwatch', tone: 'orange', hint: 'تأخر مرتبط بحصة محددة' },
    { label: 'الملاحظات السلوكية', value: 'behaviors', icon: 'pi-exclamation-circle', tone: 'rose', hint: 'وقائع السلوك ودرجة الشدة' },
    { label: 'الملاحظات الأكاديمية', value: 'academic-concerns', icon: 'pi-book', tone: 'blue', hint: 'مؤشرات تحتاج متابعة دراسية' },
    { label: 'التكريمات', value: 'recognitions', icon: 'pi-star', tone: 'green', hint: 'التميز والإنجازات الإيجابية' }
  ];
  readonly currentRecordType = computed(() => this.recordTypes.find(item => item.value === this.recordType.value) ?? this.recordTypes[0]);
  readonly recordsWithSeverity = computed(() => this.records().filter(item => item.severity).length);

  constructor() {
    this.load();
    if (this.mode === 'permits' || this.mode === 'referrals') this.loadStudentDirectory();
    if (this.mode === 'referrals') this.loadWorkers();
    this.referralForm.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => { this.referralIdempotencyKey = null; });
    this.recordType.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => { this.selectedRecord.set(null); this.load(); });
    this.classroomControl.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.clearSelectedStudent();
      this.loadStudents();
    });
  }

  load(event?: TableLazyLoadEvent): void {
    if (this.loading()) return;
    const size = event?.rows ?? this.pageSize();
    const page = Math.floor((event?.first ?? 0) / size) + 1;
    this.pageSize.set(size);
    this.loading.set(true);
    this.error.set('');
    const request = (this.mode === 'permits'
      ? this.api.listEntryPermits(page, size)
      : this.mode === 'referrals'
        ? this.api.listReferrals(page, size)
        : this.mode === 'automation'
          ? this.api.listAutomationReviews(page, size)
          : this.api.listOperationalRecords(this.recordType.value, page, size, this.recordSearch.value)) as Observable<ApiResponse<PagedResult<unknown>>>;
    request.pipe(
      finalize(() => this.loading.set(false)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) { this.error.set(response.errors[0] ?? response.message); return; }
        this.total.set(response.data.totalCount);
        if (this.mode === 'permits') this.permits.set(response.data.items as readonly ClassroomEntryPermitDto[]);
        else if (this.mode === 'referrals') this.referrals.set(response.data.items as readonly ReferralDto[]);
        else if (this.mode === 'automation') this.reviews.set(response.data.items as readonly SummonDto[]);
        else this.records.set(response.data.items as readonly OperationalRecordDto[]);
      },
      error: (error: HttpErrorResponse) => { this.error.set(extractHttpErrorMessage(error) ?? 'تعذر تحميل البيانات.'); }
    });
  }

  searchForStudents(): void {
    const search = this.studentSearch.value.trim();
    if (!this.classroomControl.value && search.length < 2) {
      this.studentOptions.set([]);
      this.studentLookupHint.set('اكتب حرفين على الأقل للبحث في جميع الفصول، أو اختر فصلًا أولًا.');
      return;
    }
    this.loadStudents(search);
  }

  clearStudentSearch(): void {
    this.studentSearch.reset('');
    if (this.classroomControl.value) this.loadStudents();
    else this.studentOptions.set([]);
  }

  private loadStudents(search = this.studentSearch.value.trim()): void {
    this.studentLookupLoading.set(true);
    this.studentLookupHint.set('جارٍ تحميل قائمة الطلاب…');
    this.directoryApi.getStudentsStats({
      pageNumber: 1,
      pageSize: 100,
      classroomId: this.classroomControl.value ?? undefined,
      isActive: true,
      search: search || undefined
    }).pipe(
      finalize(() => this.studentLookupLoading.set(false)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: response => {
        const students = response.data?.items ?? [];
        this.studentOptions.set(students);
        this.studentLookupHint.set(students.length
          ? `${students.length} طالبًا متاحًا للاختيار`
          : 'لا يوجد طلاب مطابقون داخل النطاق المحدد.');
      },
      error: (error: HttpErrorResponse) => {
        this.studentOptions.set([]);
        this.studentLookupHint.set(extractHttpErrorMessage(error) ?? 'تعذر تحميل الطلاب.');
      }
    });
  }

  chooseStudent(student: StudentStatsDto): void {
    this.selectedStudent.set(student);
    this.permitForm.controls.studentId.setValue(student.studentId);
    this.referralForm.controls.studentId.setValue(student.studentId);
    this.studentOptions.set([]);
  }

  clearSelectedStudent(): void {
    this.selectedStudent.set(null);
    this.permitForm.controls.studentId.reset(null);
    this.referralForm.controls.studentId.reset(null);
  }

  issuePermit(): void {
    if (this.permitForm.invalid || this.saving()) return;
    const value = this.permitForm.getRawValue();
    this.saving.set(true);
    this.api.issueEntryPermit({ studentId: value.studentId!, reason: value.reason.trim(), validFrom: new Date(value.validFrom).toISOString(), validUntil: new Date(value.validUntil).toISOString() }).subscribe({
      next: response => { this.saving.set(false); if (!response.isSuccess) { this.warn(response.errors[0] ?? response.message); return; } this.success('تم إصدار تصريح الدخول.'); this.load(); },
      error: error => this.failed(error)
    });
  }

  openRevoke(permit: ClassroomEntryPermitDto): void {
    this.selectedPermit.set(permit);
    this.revokeReason.reset('');
  }

  revokePermit(): void {
    const permit = this.selectedPermit();
    const reason = this.revokeReason.value.trim();
    if (!permit || this.revokeReason.invalid || !reason || this.saving()) return;
    this.saving.set(true);
    this.api.revokeEntryPermit(permit.id, reason, permit.rowVersion).subscribe({
      next: response => { this.saving.set(false); if (!response.isSuccess) { this.warn(response.errors[0] ?? response.message); return; } this.selectedPermit.set(null); this.success('تم إلغاء التصريح.'); this.load(); },
      error: error => this.failed(error)
    });
  }

  createReferral(): void {
    if (this.referralForm.invalid || this.saving()) return;
    const value = this.referralForm.getRawValue();
    this.referralIdempotencyKey ??= this.api.createIdempotencyKey();
    this.saving.set(true);
    this.api.createReferral({ studentId: value.studentId!, reason: value.reason.trim(), source: 'Manual', priority: value.priority }, this.referralIdempotencyKey).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) { this.saving.set(false); this.warn(response.errors[0] ?? response.message); return; }
        this.referralIdempotencyKey = null;
        if (value.workerId) {
          this.assignCreatedReferral(response.data, value.workerId, value.assignmentReason);
          return;
        }
        this.saving.set(false);
        this.afterReferralSaved('تم إنشاء الإحالة وإضافتها إلى قائمة انتظار الإسناد.');
      },
      error: error => this.failed(error)
    });
  }

  openAssignment(referral: ReferralDto): void { this.selectedReferral.set(referral); this.assignmentForm.reset({ workerId: '', reason: '' }); }

  assignReferral(): void {
    const referral = this.selectedReferral();
    if (!referral || this.assignmentForm.invalid || this.saving()) return;
    const value = this.assignmentForm.getRawValue();
    this.saving.set(true);
    this.api.assignReferral(referral.id, value.workerId, value.reason, referral.rowVersion).subscribe({
      next: response => { this.saving.set(false); if (!response.isSuccess) { this.warn(response.errors[0] ?? response.message); return; } this.selectedReferral.set(null); this.success('تم إسناد الإحالة.'); this.load(); },
      error: error => this.failed(error)
    });
  }

  openReview(review: SummonDto): void { this.selectedReview.set(review); this.reviewForm.reset({ decision: 'Retain', rationale: '' }); }

  submitReview(): void {
    const review = this.selectedReview();
    if (!review || this.reviewForm.invalid || this.saving()) return;
    const value = this.reviewForm.getRawValue();
    this.saving.set(true);
    this.api.reviewAutomationImpact(review.id, value.decision, value.rationale.trim(), review.rowVersion).subscribe({
      next: response => { this.saving.set(false); if (!response.isSuccess) { this.warn(response.errors[0] ?? response.message); return; } this.selectedReview.set(null); this.success('تم تسجيل مراجعة أثر الأتمتة.'); this.load(); },
      error: error => this.failed(error)
    });
  }

  recordDate(record: OperationalRecordDto): string { return record.occurredAt ?? record.arrivalAt ?? record.recognizedAt ?? '—'; }
  formatDate(value: string): string { const date = new Date(value); return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('ar-SA', { dateStyle: 'medium', timeStyle: 'short' }).format(date); }
  chooseRecordType(value: string): void { if (this.recordType.value !== value) this.recordType.setValue(value); }
  recordSummary(record: OperationalRecordDto): string { return record.description || record.title || 'لا يوجد وصف إضافي'; }
  recordCategory(record: OperationalRecordDto): string { return record.category || record.recognitionType || record.status || 'سجل تشغيلي'; }
  severityLabel(value?: string): string {
    if (!value) return 'غير مصنف';
    return ({ Low: 'منخفضة', Minor: 'بسيطة', Medium: 'متوسطة', Moderate: 'متوسطة', High: 'عالية', Major: 'عالية', Critical: 'حرجة' } as Record<string, string>)[value] ?? value;
  }

  private loadStudentDirectory(): void {
    this.classroomsLoading.set(true);
    this.directoryApi.getClassrooms().pipe(
      finalize(() => this.classroomsLoading.set(false)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: response => {
        const classrooms = (response.data?.items ?? []).filter(item => item.isActive);
        this.classrooms.set(classrooms);
        if (classrooms.length === 1) this.classroomControl.setValue(classrooms[0].id);
      },
      error: (error: HttpErrorResponse) => this.studentLookupHint.set(extractHttpErrorMessage(error) ?? 'تعذر تحميل الفصول.')
    });
  }
  private loadWorkers(): void {
    this.workersLoading.set(true);
    this.workersError.set('');
    this.api.assignableWorkers().pipe(
      finalize(() => this.workersLoading.set(false)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: response => {
        this.workers.set(response.data ?? []);
        if (!response.isSuccess) this.workersError.set(response.errors[0] ?? response.message);
      },
      error: (error: HttpErrorResponse) => {
        this.workers.set([]);
        this.workersError.set(extractHttpErrorMessage(error) ?? 'تعذر تحميل قائمة الأخصائيين.');
      }
    });
  }
  private assignCreatedReferral(referral: ReferralDto, workerId: string, reason: string): void {
    this.api.assignReferral(referral.id, workerId, reason, referral.rowVersion).subscribe({
      next: response => {
        this.saving.set(false);
        if (!response.isSuccess) { this.warn(response.errors[0] ?? response.message); this.load(); return; }
        this.afterReferralSaved('تم إنشاء الإحالة وإسنادها مباشرةً إلى الأخصائي المختار.');
      },
      error: error => { this.failed(error); this.load(); }
    });
  }
  private afterReferralSaved(detail: string): void {
    this.success(detail);
    this.referralForm.reset({ studentId: null, reason: '', priority: 'Normal', workerId: '', assignmentReason: '' });
    this.selectedStudent.set(null);
    this.load();
  }
  private localDateTime(date: Date): string { const offset = date.getTimezoneOffset() * 60_000; return new Date(date.getTime() - offset).toISOString().slice(0, 16); }
  private failed(error: HttpErrorResponse): void { this.saving.set(false); this.warn(error.status === 409 ? 'سبق تعديل السجل. حدّث القائمة ثم أعد المحاولة.' : extractHttpErrorMessage(error) ?? 'تعذر حفظ العملية.'); }
  private warn(detail: string): void { this.messages.add({ severity: 'warn', summary: 'لم تكتمل العملية', detail }); }
  private success(detail: string): void { this.messages.add({ severity: 'success', summary: 'تم الحفظ', detail }); }
}
