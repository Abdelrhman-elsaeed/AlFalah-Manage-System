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
import { DayOfWeek, OfficeHourSlotDto, OfficeHoursAggregateDto } from '../../../core/models/phase5.models';
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

  readonly aggregate = signal<OfficeHoursAggregateDto | null>(null);
  readonly selectedKeys = signal<ReadonlySet<string>>(new Set<string>());
  readonly rowVersion = signal<string | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly errorMessage = signal('');
  readonly conflict = signal(false);
  readonly effectiveFrom = new FormControl<Date | null>(new Date(), { validators: [Validators.required] });
  get days(): readonly DayOfWeek[] { return [...new Set(this.aggregate()?.slots.map(slot => slot.dayOfWeek) ?? [])]; }

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
        this.aggregate.set(eligible.data);
        this.rowVersion.set(current.data.rowVersion);
        if (!preserveSelection) this.selectedKeys.set(new Set(current.data.slots.filter(slot => slot.isSelected).map(slot => slot.stableKey)));
      },
      error: error => this.errorMessage.set(this.httpMessage(error, 'تعذر تحميل الساعات المكتبية.'))
    });
  }

  slotsFor(day: DayOfWeek): readonly OfficeHourSlotDto[] { return this.aggregate()?.slots.filter(slot => slot.dayOfWeek === day) ?? []; }
  isSelected(key: string): boolean { return this.selectedKeys().has(key); }
  setSelected(slot: OfficeHourSlotDto, checked: boolean): void {
    if (!slot.isEligible) return;
    this.selectedKeys.update(current => {
      const next = new Set(current);
      if (checked) next.add(slot.stableKey); else next.delete(slot.stableKey);
      return next;
    });
  }

  save(): void {
    const version = this.rowVersion();
    const date = this.effectiveFrom.value;
    if (!version || !date || this.saving()) return;
    const eligibleKeys = new Set(this.aggregate()?.slots.filter(slot => slot.isEligible).map(slot => slot.stableKey) ?? []);
    const selected = [...this.selectedKeys()].filter(key => eligibleKeys.has(key));
    this.saving.set(true);
    this.api.updateMyOfficeHours({ selectedSlotKeys: [...new Set(selected)], effectiveFrom: this.dateValue(date), rowVersion: version }).pipe(finalize(() => this.saving.set(false))).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) {
          if (this.isConflict(`${response.message} ${response.errors.join(' ')}`)) this.handleConflict();
          else this.toast.warn('لم تُحفظ الساعات المكتبية', response.errors[0] ?? response.message);
          return;
        }
        this.aggregate.set(response.data);
        this.rowVersion.set(response.data.rowVersion);
        this.selectedKeys.set(new Set(response.data.slots.filter(slot => slot.isSelected).map(slot => slot.stableKey)));
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
