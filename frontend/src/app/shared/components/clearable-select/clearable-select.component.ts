import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  Output,
  TemplateRef,
  forwardRef,
  signal,
  ChangeDetectorRef,
  HostBinding
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, FormControl, NG_VALUE_ACCESSOR, ReactiveFormsModule } from '@angular/forms';
import { DropdownModule } from 'primeng/dropdown';
import { TranslateModule } from '@ngx-translate/core';

type DropdownChangeEvent = { value: any; originalEvent?: Event };

/**
 * <app-clearable-select>
 * ────────────────────────
 * Generic wrapper around <p-dropdown> that:
 *   1. Uses PrimeNG's own in-field ✕ (showClear) when the field is clearable
 *      and currently holds a value. The theme already reserves padding on
 *      .p-dropdown-label for the chevron + ✕, so the icon sits inside the
 *      control next to the chevron and never covers the selected text.
 *   2. Re-emits (cleared) so the parent can run dependent logic — e.g. reload
 *      a list when a filter is cleared.
 *   3. Implements ControlValueAccessor — works with [(ngModel)] AND
 *      [formControl].
 *
 * This previously rendered a separate "✕ مسح" text button as a flex sibling
 * *outside* the control. That read as a stray link floating beside the field,
 * and because it was a flex sibling it also shrank the dropdown so it no
 * longer filled its grid cell. Required fields set [clearable]="false".
 *
 * Inputs mirror the relevant subset of p-dropdown. Add more inputs
 * here if a new use case needs them.
 */
@Component({
  selector: 'app-clearable-select',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, DropdownModule, TranslateModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => ClearableSelectComponent),
      multi: true
    }
  ],
  template: `
    <span class="clearable-select" [class.has-value]="value() !== null && value() !== undefined">
      <p-dropdown [options]="options"
                  [inputId]="inputId"
                  [optionLabel]="optionLabel"
                  [optionValue]="optionValue"
                  [optionDisabled]="optionDisabled"
                  [placeholder]="placeholder"
                  [filter]="filter"
                  [filterBy]="filterBy"
                  [styleClass]="styleClass"
                  [style]="style"
                  [panelStyle]="panelStyle"
                  [scrollHeight]="scrollHeight"
                  [appendTo]="appendTo"
                  [emptyMessage]="emptyMessage"
                  [loading]="loading"
                  [showClear]="showInFieldClear()"
                  [formControl]="control"
                  (onChange)="onDropdownChange.emit($event)"
                  (onClear)="cleared.emit()"
                  (onFilter)="onFilter.emit($event)">
        <ng-template pTemplate="item" let-opt>
          <ng-container *ngIf="itemTpl; else defaultItem">
            <ng-container *ngTemplateOutlet="itemTpl; context: { $implicit: opt }"></ng-container>
          </ng-container>
          <ng-template #defaultItem>{{ displayLabel(opt) }}</ng-template>
        </ng-template>
        <ng-template pTemplate="selectedItem" let-opt>
          <ng-container *ngIf="selectedItemTpl; else defaultSelectedItem">
            <ng-container *ngTemplateOutlet="selectedItemTpl; context: { $implicit: opt }"></ng-container>
          </ng-container>
          <ng-template #defaultSelectedItem>{{ displayLabel(opt) }}</ng-template>
        </ng-template>
        <ng-template pTemplate="empty">
          <ng-container *ngIf="emptyTpl; else defaultEmpty">
            <ng-container *ngTemplateOutlet="emptyTpl"></ng-container>
          </ng-container>
          <ng-template #defaultEmpty>{{ emptyMessage }}</ng-template>
        </ng-template>
      </p-dropdown>
    </span>
  `,
  styles: [`
    :host {
      display: block;
      width: 100%;
      min-width: 0;
    }
    /* Block, not inline-flex: the control is the only child and must fill its
       grid / toolbar cell exactly. */
    .clearable-select {
      display: block;
      position: relative;
      min-width: 0;
      width: 100%;
      max-width: 100%;
      box-sizing: border-box;
    }
    .clearable-select > .p-dropdown {
      display: flex;
      width: 100%;
      min-width: 0;
      max-width: 100%;
      box-sizing: border-box;
    }
    .clearable-select ::ng-deep .p-dropdown-label {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      min-width: 0;
    }
  `]
})
export class ClearableSelectComponent implements ControlValueAccessor {
  /** Same as p-dropdown: list of options. */
  @Input() options: any[] | null = [];
  @Input() inputId: string | undefined;
  /**
   * The option property to render. Leave undefined so PrimeNG auto-resolves
   * (PrimeNG falls back to "label" then to the value's own string form).
   * Callers can pass any string for richer DTOs (e.g. "fullName").
   */
  @Input() optionLabel: string | undefined = undefined;
  @Input() optionValue: string | undefined = undefined;
  @Input() optionDisabled: string | undefined;
  @Input() placeholder = '';
  @Input() filter = false;
  @Input() filterBy: string | undefined;
  @Input() styleClass = 'w-full';
  @Input() style: { [k: string]: string } | null = null;
  @Input() panelStyle: { [k: string]: string } | null = null;
  @Input() scrollHeight = '240px';
  @Input() appendTo: string | HTMLElement | null = 'body';
  @Input() emptyMessage = '';
  @Input() loading = false;
  /** Required selects still use the unified wrapper but can suppress clearing. */
  @Input() clearable = true;

  /** Optional PrimeNG pTemplate bodies. Pass via @ViewChild + let-property. */
  @Input() itemTpl: TemplateRef<any> | null = null;
  @Input() selectedItemTpl: TemplateRef<any> | null = null;
  @Input() emptyTpl: TemplateRef<any> | null = null;

  /** Re-emits the p-dropdown onChange event for the parent's convenience. */
  @Output() onDropdownChange = new EventEmitter<DropdownChangeEvent>();
  /** Re-emits the p-dropdown onFilter event. */
  @Output() onFilter = new EventEmitter<Event>();
  /** Emits when the user clicks the external clear button. */
  @Output() cleared = new EventEmitter<void>();

  /** Holds the bound value (drives ControlValueAccessor). */
  readonly value = signal<any>(null);
  readonly control = new FormControl<any>(null);

  /**
   * PrimeNG only paints its ✕ when `showClear` is on AND a value is present,
   * but it also reserves nothing when disabled — so gate on both here and let
   * the theme handle the placement inside the field.
   */
  showInFieldClear(): boolean {
    return this.clearable && !this.disabled;
  }

  // CVA callbacks — registered by Angular via writeValue/registerOnChange/registerOnTouched.
  private onChange = (_: any) => {};
  private onTouched = () => {};
  protected disabled = false;

  constructor(private cdr: ChangeDetectorRef) {
    this.control.valueChanges.subscribe(val => {
      const next = ClearableSelectComponent.normalize(val);
      this.value.set(next);
      this.onChange(next);
      this.onTouched();
      this.cdr.markForCheck();
    });
  }

  @HostBinding('attr.dir')
  get hostDir(): string | null {
    return null;
  }

  // ─── ControlValueAccessor ────────────────────────────────────────────────

  writeValue(val: any): void {
    const next = ClearableSelectComponent.normalize(val);
    this.value.set(next);
    this.control.setValue(next, { emitEvent: false });
    this.cdr.markForCheck();
  }

  /**
   * Collapses every "nothing selected" representation to `null`.
   *
   * `''` matters: most forms in the app declare a select as
   * `field: ['', Validators.required]`, and `'' ?? null` keeps the empty
   * string. PrimeNG then paints its in-field ✕ — `isVisibleClearIcon` is
   * `modelValue() != null && hasSelectedOption()`, and `'' != null` is true —
   * so an untouched required select showed a clear button next to its own
   * "اختر" placeholder, offering to clear a value that was never chosen.
   */
  private static normalize(val: any): any {
    if (val === undefined || val === null) return null;
    if (typeof val === 'string' && val.trim() === '') return null;
    return val;
  }

  registerOnChange(fn: (val: any) => void): void {
    this.onChange = fn;
  }
  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }
  setDisabledState(isDisabled: boolean): void {
    this.disabled = isDisabled;
    if (isDisabled) {
      this.control.disable({ emitEvent: false });
    } else {
      this.control.enable({ emitEvent: false });
    }
    this.cdr.markForCheck();
  }

  displayLabel(option: any): any {
    if (option === null || option === undefined) return '';
    return this.optionLabel ? option?.[this.optionLabel] : (option?.label ?? option);
  }

  /** Called when the external clear button is pressed. */
  clear(event?: Event): void {
    if (event) event.stopPropagation();
    if (this.disabled) return;
    this.control.setValue(null);
    this.cleared.emit();
    this.cdr.markForCheck();
  }
}
