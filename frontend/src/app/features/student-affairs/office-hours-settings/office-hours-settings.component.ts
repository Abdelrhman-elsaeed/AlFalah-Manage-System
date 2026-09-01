import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CalendarModule } from 'primeng/calendar';
import { CheckboxModule } from 'primeng/checkbox';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TagModule } from 'primeng/tag';
import { finalize, forkJoin } from 'rxjs';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import { DayOfWeek, OfficeHourSlotDto, consistentOfficeHoursRowVersion } from '../../../core/models/phase5.models';
import { Phase5Service } from '../../../core/services/phase5.service';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-office-hours-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, ButtonModule, CalendarModule, CheckboxModule, ProgressSpinnerModule, TagModule],
  templateUrl: './office-hours-settings.component.html',
  styleUrl: './office-hours-settings.component.css'
})
export class OfficeHoursSettingsComponent {
  private readonly api = inject(Phase5Service);
  private readonly toast = inject(ToastService);

  readonly eligibleSlots = signal<readonly OfficeHourSlotDto[]>([]);
  readonly currentSlots = signal<readonly OfficeHourSlotDto[]>([]);
  readonly selectedIds = signal<ReadonlySet<number>>(new Set<number>());
  readonly rowVersion = signal<string | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly errorMessage = signal('');
  readonly conflict = signal(false);
  readonly effectiveFrom = new FormControl<Date | null>(new Date(), { validators: [Validators.required] });
  readonly days: readonly DayOfWeek[] = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday'];

  constructor() { this.load(); }

  get canSave(): boolean { return this.rowVersion() !== null && this.effectiveFrom.valid && !this.saving(); }

  load(preserveSelection = false): void {
    this.loading.set(true);
    this.errorMessage.set('');
    forkJoin({ eligible: this.api.getEligibleOfficeHours(), current: this.api.getMyOfficeHours() }).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: ({ eligible, current }) => {
        if (!eligible.isSuccess || !eligible.data || !current.isSuccess || !current.data) {
          this.errorMessage.set(eligible.errors[0] ?? current.errors[0] ?? eligible.message ?? current.message ?? 'تعذر تحميل الساعات المكتبية.');
          return;
        }
        this.eligibleSlots.set(eligible.data);
        this.currentSlots.set(current.data);
        this.rowVersion.set(consistentOfficeHoursRowVersion(current.data));
        if (!preserveSelection) this.selectedIds.set(new Set(current.data.map(slot => slot.id)));
      },
      error: error => this.errorMessage.set(this.httpMessage(error, 'تعذر تحميل الساعات المكتبية.'))
    });
  }

  slotsFor(day: DayOfWeek): readonly OfficeHourSlotDto[] { return this.eligibleSlots().filter(slot => slot.dayOfWeek === day); }
  isSelected(id: number): boolean { return this.selectedIds().has(id); }
  setSelected(slot: OfficeHourSlotDto, checked: boolean): void {
    if (!slot.isEligible) return;
    this.selectedIds.update(current => {
      const next = new Set(current);
      if (checked) next.add(slot.id); else next.delete(slot.id);
      return next;
    });
  }

  save(): void {
    const version = this.rowVersion();
    const date = this.effectiveFrom.value;
    if (!version || !date || this.saving()) return;
    const eligibleIds = new Set(this.eligibleSlots().filter(slot => slot.isEligible).map(slot => slot.id));
    const selected = [...this.selectedIds()].filter(id => eligibleIds.has(id));
    this.saving.set(true);
    this.api.updateMyOfficeHours({ eligibleSlotIds: [...new Set(selected)], effectiveFrom: this.dateValue(date), rowVersion: version }).pipe(finalize(() => this.saving.set(false))).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) {
          if (this.isConflict(`${response.message} ${response.errors.join(' ')}`)) this.handleConflict();
          else this.toast.warn('لم تُحفظ الساعات المكتبية', response.errors[0] ?? response.message);
          return;
        }
        this.currentSlots.set(response.data);
        this.rowVersion.set(consistentOfficeHoursRowVersion(response.data));
        this.selectedIds.set(new Set(response.data.map(slot => slot.id)));
        this.conflict.set(false);
        this.toast.success('تم حفظ الساعات المكتبية', 'ستُستخدم المواعيد الجديدة في جدولة رسائل أولياء الأمور.');
      },
      error: (error: HttpErrorResponse) => {
        if (error.status === 409) this.handleConflict();
        else this.toast.error('تعذر حفظ الساعات المكتبية', this.httpMessage(error, 'حاول مرة أخرى.'));
      }
    });
  }

  dayLabel(day: DayOfWeek): string { return ({ Sunday: 'الأحد', Monday: 'الاثنين', Tuesday: 'الثلاثاء', Wednesday: 'الأربعاء', Thursday: 'الخميس', Friday: 'الجمعة', Saturday: 'السبت' })[day]; }
  sourceLabel(source: OfficeHourSlotDto['source']): string { return ({ DerivedFromPublishedTimetable: 'مستخرجة من الجدول المنشور', TeacherSelected: 'مختارة من المعلم', ManagerOverride: 'معتمدة بتعديل مدير المدرسة' })[source]; }
  sourceSeverity(source: OfficeHourSlotDto['source']): 'info' | 'success' | 'warning' { return source === 'TeacherSelected' ? 'success' : source === 'ManagerOverride' ? 'warning' : 'info'; }
  timeLabel(value: string): string {
    const parts = value.split(':');
    const date = new Date(2000, 0, 1, Number(parts[0]), Number(parts[1]));
    return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('ar-SA', { hour: 'numeric', minute: '2-digit' }).format(date);
  }

  private handleConflict(): void {
    this.conflict.set(true);
    this.toast.warn('تغيرت إعدادات الساعات المكتبية', 'احتفظنا باختياراتك وجلبنا أحدث إصدار. راجعها قبل الحفظ من جديد.');
    this.load(true);
  }
  private dateValue(value: Date): string {
    return `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`;
  }
  private isConflict(value: string): boolean { const text = value.toLowerCase(); return text.includes('rowversion') || text.includes('row version') || text.includes('concurrency') || text.includes('مستخدم آخر'); }
  private httpMessage(error: unknown, fallback: string): string { return extractHttpErrorMessage(error) ?? fallback; }
}
