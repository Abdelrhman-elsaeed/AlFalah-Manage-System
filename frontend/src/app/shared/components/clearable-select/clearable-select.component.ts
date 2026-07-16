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
 *   1. Renders the internal PrimeNG ✕ off (showClear=false).
 *   2. Shows a SMALL external "مسح" button (pi pi-times + label),
 *      inline-end (LEFT in RTL), ONLY when the value is non-null.
 *   3. Clears the bound value on click and fires (cleared) so the parent
 *      can also run dependent UI logic (e.g. reload a list when a filter
 *      is cleared).
 *   4. Implements ControlValueAccessor — works with [(ngModel)] AND
 *      [formControl].
 *
 * Use this for every application dropdown. Optional fields and filters keep
 * the default external clear action; required fields set [clearable]="false".
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
                  [showClear]="false"
                  [formControl]="control"
                  (onChange)="onDropdownChange.emit($event)"
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

      <button *ngIf="clearable && !disabled && value() !== null && value() !== undefined"
              type="button"
              class="clearable-select__btn"
              [attr.aria-label]="'COMMON.CLEAR' | translate"
              [attr.title]="'COMMON.CLEAR' | translate"
              (click)="clear($event)">
        <i class="pi pi-times"></i>
        <span class="clearable-select__label">{{ 'COMMON.CLEAR' | translate }}</span>
      </button>
    </span>
  `,
  styles: [`
    :host {
      display: block;
      width: 100%;
      min-width: 0;
    }
    .clearable-select {
      display: inline-flex;
      align-items: center;
      gap: 0.3rem;
      position: relative;
      min-width: 0;
      width: 100%;
      max-width: 100%;
      box-sizing: border-box;
    }
    .clearable-select > .p-dropdown {
      flex: 1;
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
    .clearable-select__btn {
      display: inline-flex;
      align-items: center;
      gap: 0.25rem;
      background: transparent;
      border: 0;
      color: var(--text-muted);
      font-size: 0.74rem;
      padding: 0.2rem 0.38rem;
      border-radius: var(--radius-sm);
      cursor: pointer;
      white-space: nowrap;
      transition: background var(--duration-fast), color var(--duration-fast), opacity var(--duration-fast);
      opacity: 0.85;
    }
    .clearable-select__btn:hover,
    .clearable-select__btn:focus-visible {
      background: var(--danger-bg);
      color: var(--danger);
      opacity: 1;
      outline: none;
    }
    .clearable-select__btn .pi {
      font-size: 0.72rem;
      line-height: 1;
    }
    [dir="rtl"] .clearable-select__label {
      font-family: inherit;
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

  // CVA callbacks — registered by Angular via writeValue/registerOnChange/registerOnTouched.
  private onChange = (_: any) => {};
  private onTouched = () => {};
  protected disabled = false;

  constructor(private cdr: ChangeDetectorRef) {
    this.control.valueChanges.subscribe(val => {
      const next = val ?? null;
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
    // p-dropdown treats `null` / `undefined` as "nothing selected"; preserve that.
    const next = (val === undefined ? null : val) ?? null;
    this.value.set(next);
    this.control.setValue(next, { emitEvent: false });
    this.cdr.markForCheck();
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
