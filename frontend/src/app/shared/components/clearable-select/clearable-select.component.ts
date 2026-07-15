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
import { ControlValueAccessor, FormsModule, NG_VALUE_ACCESSOR } from '@angular/forms';
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
  imports: [CommonModule, FormsModule, DropdownModule, TranslateModule],
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
                  [appendTo]="appendTo"
                  [emptyMessage]="emptyMessage"
                  [loading]="loading"
                  [disabled]="disabled"
                  [showClear]="false"
                  [ngModel]="value()"
                  (ngModelChange)="writeValue($event); onChangeInternal($event)"
                  (onChange)="onDropdownChange.emit($event)"
                  (onFilter)="onFilter.emit($event)">
        <ng-template *ngIf="itemTpl" pTemplate="item" let-opt>
          <ng-container *ngTemplateOutlet="itemTpl; context: { $implicit: opt }"></ng-container>
        </ng-template>
        <ng-template *ngIf="selectedItemTpl" pTemplate="selectedItem" let-opt>
          <ng-container *ngTemplateOutlet="selectedItemTpl; context: { $implicit: opt }"></ng-container>
        </ng-template>
        <ng-template *ngIf="emptyTpl" pTemplate="empty">
          <ng-container *ngTemplateOutlet="emptyTpl"></ng-container>
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
      gap: 0.4rem;
      position: relative;
      min-width: 12rem;
      width: 100%;
    }
    .clearable-select > .p-dropdown {
      flex: 1;
      min-width: 0;
    }
    .clearable-select__btn {
      display: inline-flex;
      align-items: center;
      gap: 0.25rem;
      background: transparent;
      border: 0;
      color: var(--text-muted);
      font-size: 0.8rem;
      padding: 0.25rem 0.45rem;
      border-radius: var(--radius-sm);
      cursor: pointer;
      white-space: nowrap;
      transition: background 0.15s, color 0.15s, opacity 0.15s;
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
      font-size: 0.8rem;
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

  // CVA callbacks — registered by Angular via writeValue/registerOnChange/registerOnTouched.
  private onChange = (_: any) => {};
  private onTouched = () => {};
  protected disabled = false;

  constructor(private cdr: ChangeDetectorRef) {}

  @HostBinding('attr.dir')
  get hostDir(): string | null {
    return null;
  }

  // ─── ControlValueAccessor ────────────────────────────────────────────────

  writeValue(val: any): void {
    // p-dropdown treats `null` / `undefined` as "nothing selected"; preserve that.
    const next = (val === undefined ? null : val) ?? null;
    this.value.set(next);
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
    this.cdr.markForCheck();
  }

  /** Called by (ngModelChange) on the internal p-dropdown. */
  onChangeInternal(val: any): void {
    this.value.set(val ?? null);
    this.onChange(val ?? null);
    this.onTouched();
    this.cdr.markForCheck();
  }

  /** Called when the external clear button is pressed. */
  clear(event?: Event): void {
    if (event) event.stopPropagation();
    if (this.disabled) return;
    this.value.set(null);
    this.onChange(null);
    this.onTouched();
    this.cleared.emit();
    this.cdr.markForCheck();
  }
}
