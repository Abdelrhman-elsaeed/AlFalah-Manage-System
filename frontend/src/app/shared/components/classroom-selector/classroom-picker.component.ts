import { CommonModule } from '@angular/common';
import { Component, DestroyRef, EventEmitter, Input, OnChanges, OnInit, Output, forwardRef, inject } from '@angular/core';
import { ControlValueAccessor, FormsModule, NG_VALUE_ACCESSOR } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DropdownModule } from 'primeng/dropdown';
import { ClassroomCatalogService, ClassroomOption } from '../../../core/services/classroom-catalog.service';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';

@Component({
  selector: 'app-classroom-picker', standalone: true, imports: [CommonModule, FormsModule, DropdownModule],
  providers: [{provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => ClassroomPickerComponent), multi: true}],
  template: `<p-dropdown [options]="groups" [group]="true" optionGroupLabel="label" optionGroupChildren="items"
    optionLabel="label" optionValue="id" optionDisabled="disabled" [filter]="true" filterBy="label"
    [ngModel]="value" (ngModelChange)="select($event)" (onBlur)="touch()" [showClear]="true" appendTo="body"
    [disabled]="disabled || loading || !!error" placeholder="اختر فصلًا من سجل المدرسة" ariaLabel="فصل الزيارة أو الحصة"
    [style]="{width:'100%'}"></p-dropdown>
    <small *ngIf="loading" role="status">جارٍ تحميل الفصول…</small>
    <p *ngIf="error" role="alert">{{error}} <button type="button" (click)="load()">إعادة المحاولة</button></p>
    <small *ngIf="!loading && !error && !options.length">لم تُضف فصول لهذا العام. راجع «إدارة الفصول».</small>
    <small *ngIf="!loading && !error && value && !hasSelectedOption">الفصل المحفوظ غير متاح في هذا السياق. اختر الفصل الصحيح قبل الحفظ.</small>`,
  styles: [':host{display:block;width:100%;min-width:0}small{display:block;margin-top:8px;color:var(--text-muted);line-height:1.7;font-size:.75rem}p{color:#a5352e;font-size:.8rem}button{font:inherit;min-height:44px}']
})
export class ClassroomPickerComponent implements OnInit, OnChanges, ControlValueAccessor {
  private readonly api = inject(ClassroomCatalogService);
  private readonly destroy = inject(DestroyRef);
  private sequence = 0;
  private initialized = false;
  @Input() academicYearId?: number | null;
  @Input() onDate?: string;
  @Output() ready = new EventEmitter<boolean>();
  @Output() selected = new EventEmitter<ClassroomOption | null>();
  options: ClassroomOption[] = [];
  value: number | null = null;
  disabled = false;
  loading = true;
  error = '';
  private change: (value: number | null) => void = () => {};
  touch: () => void = () => {};
  get hasSelectedOption(): boolean { return this.options.some(c => c.id === this.value); }
  get groups() {
    const groups = new Map<string, {label: string; items: (ClassroomOption & {disabled: boolean})[]}>();
    for (const option of this.options) {
      const key = `${option.stage}-${option.gradeLevel}`;
      if (!groups.has(key)) groups.set(key, {label: this.api.gradeLabel(option.stage, option.gradeLevel), items: []});
      groups.get(key)!.items.push({...option, label: option.label + (option.isActive ? '' : ' (غير نشط)'), disabled: !option.isActive && option.id !== this.value});
    }
    return [...groups.values()];
  }
  ngOnInit(): void { this.initialized = true; this.load(); }
  ngOnChanges(): void { if (this.initialized) this.load(); }
  load(): void {
    const request = ++this.sequence;
    this.loading = true; this.error = ''; this.ready.emit(false);
    this.api.all(this.academicYearId || undefined, this.onDate || undefined).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: data => {
        if (request !== this.sequence) return;
        this.options = data.items; this.loading = false;
        this.ready.emit(!this.value || this.hasSelectedOption);
      }, error: error => {
        if (request !== this.sequence) return;
        this.loading = false; this.error = extractHttpErrorMessage(error) || 'تعذر تحميل الفصول. أعد المحاولة قبل الحفظ.';
      }
    });
  }
  writeValue(value: number | null): void {
    this.value = value ?? null;
    // A saved visit can change without changing its date/year inputs.
    queueMicrotask(() => this.ready.emit(!this.loading && !this.error && (!this.value || this.hasSelectedOption)));
  }
  registerOnChange(fn: (value: number | null) => void): void { this.change = fn; }
  registerOnTouched(fn: () => void): void { this.touch = fn; }
  setDisabledState(disabled: boolean): void { this.disabled = disabled; }
  select(value: number | null): void {
    this.value = value; this.change(value); this.touch();
    this.selected.emit(this.options.find(c => c.id === value) ?? null);
    this.ready.emit(!this.loading && !this.error && (!value || this.hasSelectedOption));
  }
}
