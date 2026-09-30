import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { finalize } from 'rxjs';
import { OfficeHourSlotDto, OfficeHoursAggregateDto, SchoolInstructorOptionDto } from '../../../core/models/phase5.models';
import { Phase5Service } from '../../../core/services/phase5.service';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-manager-office-hours',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, ButtonModule],
  template: `
    <main class="manager-page" dir="rtl">
      <header><div><span>تجاوز إداري مدقق</span><h1>إدارة الساعات المكتبية</h1><p>اختر معلماً من المدرسة النشطة، وراجع المواعيد قبل اعتماد أي تجاوز.</p></div><button pButton type="button" class="p-button-outlined" icon="pi pi-refresh" label="تحديث" [loading]="loading()" (click)="refresh()"></button></header>
      <section class="panel chooser">
        <label for="teacher">المعلم</label>
        <select id="teacher" [formControl]="teacherId" (change)="loadTeacher(false)"><option [ngValue]="null">اختر معلماً</option><option *ngFor="let teacher of teachers()" [ngValue]="teacher.instructorProfileId">{{ teacher.displayName }}{{ teacher.subject ? ' — ' + teacher.subject : '' }}</option></select>
      </section>
      <p class="state" role="status" aria-live="polite" *ngIf="loading()">جارٍ تحميل البيانات…</p>
      <section class="state error" role="alert" *ngIf="errorMessage()">{{ errorMessage() }} <button type="button" (click)="refresh()">إعادة المحاولة</button></section>
      <section class="panel" *ngIf="!loading() && aggregate() as current">
        <div class="meta"><strong>المصدر الحالي: {{ sourceLabel(current.source) }}</strong><span *ngIf="current.statusReason">{{ current.statusReason }}</span></div>
        <p class="empty" *ngIf="current.slots.length === 0">لا توجد فترات مؤهلة في الجدول المنشور الحالي.</p>
        <div class="slots" *ngIf="current.slots.length">
          <label *ngFor="let slot of current.slots" [class.conflict]="slot.isConflicted">
            <input type="checkbox" [checked]="isSelected(slot.stableKey)" [disabled]="!slot.isEligible || saving()" (change)="toggle(slot, $any($event.target).checked)" />
            <span>{{ dayLabel(slot.dayOfWeek) }} · الحصة {{ slot.periodSequence }} · {{ slot.startsAt }}–{{ slot.endsAt }}</span>
            <small *ngIf="slot.isConflicted">تعارض: {{ slot.conflictReason || 'لم تعد الفترة مؤهلة' }}</small>
          </label>
        </div>
        <div class="form-row"><label>ساري من<input type="date" [formControl]="effectiveFrom" /></label><label>سبب التجاوز الإداري<textarea [formControl]="reason" maxlength="2000" rows="3"></textarea><small>{{ reason.value.length }}/2000</small></label></div>
        <button pButton type="button" icon="pi pi-check" label="اعتماد التجاوز" [loading]="saving()" [disabled]="!canSave" (click)="save()"></button>
      </section>
      <p class="state" *ngIf="!loading() && !errorMessage() && teachers().length === 0">لا يوجد معلمون نشطون في المدرسة.</p>
    </main>`,
  styles: [`
    .manager-page{display:grid;gap:1rem;padding:1.25rem;max-width:1200px;margin:auto}header,.meta{display:flex;justify-content:space-between;gap:1rem;align-items:center}h1{margin:.25rem 0}.panel,.state{background:var(--surface-card,#fff);border:1px solid var(--surface-border,#ddd);border-radius:14px;padding:1rem}.chooser{display:grid;gap:.4rem;max-width:620px}select,input,textarea{font:inherit;border:1px solid var(--surface-border,#bbb);border-radius:8px;padding:.65rem;width:100%;box-sizing:border-box}.slots{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:.6rem;margin:1rem 0}.slots label{display:grid;grid-template-columns:auto 1fr;gap:.5rem;padding:.75rem;border:1px solid var(--surface-border,#ddd);border-radius:10px}.slots input{width:auto}.slots small{grid-column:2}.conflict{border-inline-start:4px solid #b45309!important}.form-row{display:grid;grid-template-columns:minmax(180px,1fr) minmax(260px,2fr);gap:1rem;margin:1rem 0}.form-row label{display:grid;gap:.35rem}.error{border-color:#b91c1c}.error button{margin-inline-start:.5rem}@media(max-width:650px){header,.meta{align-items:stretch;flex-direction:column}.form-row{grid-template-columns:1fr}}
  `]
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

  constructor() { this.loadTeachers(); }
  get canSave(): boolean { return !!this.aggregate()?.rowVersion && this.reason.valid && this.effectiveFrom.valid && !this.saving(); }
  refresh(): void { this.teacherId.value ? this.loadTeacher(true) : this.loadTeachers(); }
  loadTeachers(): void {
    this.loading.set(true); this.errorMessage.set('');
    this.api.getSchoolInstructorOptions().pipe(finalize(() => this.loading.set(false))).subscribe({
      next: response => response.isSuccess && response.data ? this.teachers.set(response.data) : this.errorMessage.set(response.errors[0] ?? response.message),
      error: (error: HttpErrorResponse) => this.errorMessage.set(this.error(error, 'تعذر تحميل المعلمين.'))
    });
  }
  loadTeacher(preserveDraft: boolean): void {
    const id = this.teacherId.value; if (!id) { this.aggregate.set(null); return; }
    this.loading.set(true); this.errorMessage.set('');
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
      error: (error: HttpErrorResponse) => this.errorMessage.set(this.error(error, 'تعذر تحميل الساعات المكتبية.'))
    });
  }
  isSelected(key: string): boolean { return this.selectedKeys().has(key); }
  toggle(slot: OfficeHourSlotDto, checked: boolean): void {
    if (!slot.isEligible) return;
    this.selectedKeys.update(current => { const next = new Set(current); checked ? next.add(slot.stableKey) : next.delete(slot.stableKey); return next; });
  }
  save(): void {
    const id = this.teacherId.value, current = this.aggregate(); this.reason.markAsTouched();
    if (!id || !current || !this.canSave || !confirm('هل تريد اعتماد هذا التجاوز الإداري؟ سيُحفظ السبب وسجل التدقيق.')) return;
    this.saving.set(true);
    this.api.overrideTeacherOfficeHours(id, { selectedSlotKeys: [...this.selectedKeys()], effectiveFrom: this.effectiveFrom.value, reason: this.reason.value.trim(), rowVersion: current.rowVersion })
      .pipe(finalize(() => this.saving.set(false))).subscribe({
        next: response => { if (response.isSuccess && response.data) { this.aggregate.set(response.data); this.selectedKeys.set(new Set(response.data.slots.filter(x => x.isSelected).map(x => x.stableKey))); this.reason.setValue(''); this.toast.success('تم اعتماد التجاوز', 'حُفظ التعديل وسجل التدقيق بنجاح.'); } else this.toast.warn('لم يُحفظ التعديل', response.errors[0] ?? response.message); },
        error: (error: HttpErrorResponse) => { if (error.status === 409) { this.toast.warn('تغيرت البيانات', 'جُلب أحدث إصدار مع الاحتفاظ بمسودتك؛ راجعها ثم أعد الحفظ.'); this.loadTeacher(true); } else { this.toast.warn('تعذر تأكيد الحفظ', 'لن نعيد الإرسال تلقائياً. سنجلب حالة الخادم أولاً مع الاحتفاظ بمسودتك.'); this.loadTeacher(true); } }
      });
  }
  sourceLabel(source: OfficeHoursAggregateDto['source']): string { return ({ DerivedFromPublishedTimetable: 'مستخرجة من الجدول المنشور', TeacherSelected: 'اختيار المعلم', ManagerOverride: 'تجاوز مدير المدرسة' })[source]; }
  dayLabel(day: OfficeHourSlotDto['dayOfWeek']): string { return ({ Sunday:'الأحد', Monday:'الاثنين', Tuesday:'الثلاثاء', Wednesday:'الأربعاء', Thursday:'الخميس', Friday:'الجمعة', Saturday:'السبت' })[day]; }
  private error(error: HttpErrorResponse, fallback: string): string { return error.status === 401 ? 'انتهت الجلسة؛ سجّل الدخول مجدداً.' : error.status === 403 ? 'لا تملك صلاحية إدارة الساعات المكتبية.' : fallback; }
}
