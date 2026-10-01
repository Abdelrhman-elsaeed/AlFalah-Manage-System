import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { OfficeHourSlotDto, OfficeHoursAggregateDto, SchoolInstructorOptionDto } from '../../../core/models/phase5.models';
import { Phase5Service } from '../../../core/services/phase5.service';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-manager-office-hours',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './manager-office-hours.component.html',
  styleUrls: ['../manager-workspace.css', './manager-office-hours.component.css']
})
export class ManagerOfficeHoursComponent {
  private readonly api = inject(Phase5Service);
  private readonly toast = inject(ToastService);

  readonly teachers = signal<readonly SchoolInstructorOptionDto[]>([]);
  readonly aggregate = signal<OfficeHoursAggregateDto | null>(null);
  readonly selectedKeys = signal<ReadonlySet<string>>(new Set());
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly errorMessage = signal('');
  readonly teacherId = new FormControl<number | null>(null, Validators.required);
  readonly effectiveFrom = new FormControl(new Date().toISOString().slice(0, 10), { nonNullable: true, validators: Validators.required });
  readonly reason = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(2000)] });

  readonly selectedCount = computed(() => this.selectedKeys().size);
  readonly eligibleCount = computed(() => this.aggregate()?.slots.filter(slot => slot.isEligible).length ?? 0);
  readonly conflictCount = computed(() => this.aggregate()?.slots.filter(slot => slot.isConflicted).length ?? 0);
  readonly activeTeacher = computed(() => this.teachers().find(teacher => teacher.instructorProfileId === this.teacherId.value) ?? null);

  constructor() { this.loadTeachers(); }

  get canSave(): boolean {
    return !!this.aggregate()?.rowVersion && this.reason.valid && this.effectiveFrom.valid && !this.saving();
  }

  refresh(): void { this.teacherId.value ? this.loadTeacher(true) : this.loadTeachers(); }

  loadTeachers(): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.api.getSchoolInstructorOptions().pipe(finalize(() => this.loading.set(false))).subscribe({
      next: response => {
        if (response.isSuccess && response.data) this.teachers.set(response.data);
        else this.errorMessage.set(response.errors[0] ?? response.message);
      },
      error: (error: HttpErrorResponse) => this.errorMessage.set(this.error(error, 'تعذر تحميل قائمة المعلمين.'))
    });
  }

  loadTeacher(preserveDraft: boolean): void {
    const id = this.teacherId.value;
    if (!id) { this.aggregate.set(null); this.selectedKeys.set(new Set()); return; }
    this.loading.set(true);
    this.errorMessage.set('');
    this.api.getTeacherOfficeHours(id).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) { this.errorMessage.set(response.errors[0] ?? response.message); return; }
        this.aggregate.set(response.data);
        if (!preserveDraft) {
          this.selectedKeys.set(new Set(response.data.slots.filter(slot => slot.isSelected).map(slot => slot.stableKey)));
          this.effectiveFrom.setValue(response.data.effectiveFrom);
          this.reason.setValue('');
        }
      },
      error: (error: HttpErrorResponse) => this.errorMessage.set(this.error(error, 'تعذر تحميل الساعات المكتبية لهذا المعلم.'))
    });
  }

  isSelected(key: string): boolean { return this.selectedKeys().has(key); }

  toggle(slot: OfficeHourSlotDto, checked: boolean): void {
    if (!slot.isEligible) return;
    this.selectedKeys.update(current => {
      const next = new Set(current);
      checked ? next.add(slot.stableKey) : next.delete(slot.stableKey);
      return next;
    });
  }

  save(): void {
    const id = this.teacherId.value;
    const current = this.aggregate();
    this.reason.markAsTouched();
    if (!id || !current || !this.canSave || !confirm('هل تريد اعتماد هذا التجاوز الإداري؟ سيُحفظ السبب وسجل التدقيق.')) return;
    this.saving.set(true);
    this.api.overrideTeacherOfficeHours(id, {
      selectedSlotKeys: [...this.selectedKeys()],
      effectiveFrom: this.effectiveFrom.value,
      reason: this.reason.value.trim(),
      rowVersion: current.rowVersion
    }).pipe(finalize(() => this.saving.set(false))).subscribe({
      next: response => {
        if (response.isSuccess && response.data) {
          this.aggregate.set(response.data);
          this.selectedKeys.set(new Set(response.data.slots.filter(slot => slot.isSelected).map(slot => slot.stableKey)));
          this.reason.setValue('');
          this.toast.success('تم اعتماد التجاوز', 'حُفظ التعديل وسجل التدقيق بنجاح.');
        } else this.toast.warn('لم يُحفظ التعديل', response.errors[0] ?? response.message);
      },
      error: (error: HttpErrorResponse) => {
        if (error.status === 409) this.toast.warn('تغيرت البيانات', 'جُلب أحدث إصدار مع الاحتفاظ بمسودتك؛ راجعها ثم أعد الحفظ.');
        else this.toast.warn('تعذر تأكيد الحفظ', 'لن نعيد الإرسال تلقائيًا. جُلبت حالة الخادم للحفاظ على سلامة السجل.');
        this.loadTeacher(true);
      }
    });
  }

  sourceLabel(source: OfficeHoursAggregateDto['source']): string {
    return ({ DerivedFromPublishedTimetable: 'مستخرجة من الجدول المنشور', TeacherSelected: 'اختيار المعلم', ManagerOverride: 'تجاوز مدير المدرسة' })[source];
  }

  dayLabel(day: OfficeHourSlotDto['dayOfWeek']): string {
    return ({ Sunday: 'الأحد', Monday: 'الاثنين', Tuesday: 'الثلاثاء', Wednesday: 'الأربعاء', Thursday: 'الخميس', Friday: 'الجمعة', Saturday: 'السبت' })[day];
  }

  private error(error: HttpErrorResponse, fallback: string): string {
    if (error.status === 401) return 'انتهت الجلسة؛ سجّل الدخول مجددًا.';
    if (error.status === 403) return 'لا تملك صلاحية إدارة الساعات المكتبية.';
    return fallback;
  }
}
