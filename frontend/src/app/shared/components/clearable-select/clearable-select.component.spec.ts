import { CommonModule } from '@angular/common';
import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { By } from '@angular/platform-browser';
import { TranslateFakeCompiler, TranslateFakeLoader, TranslateModule } from '@ngx-translate/core';

import { ClearableSelectComponent } from './clearable-select.component';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, ClearableSelectComponent],
  template: `
    <ng-template #optionTemplate let-option>
      <span class="selected-label">{{ option?.labelKey }}</span>
    </ng-template>

    <app-clearable-select
      [options]="options"
      optionLabel="labelKey"
      optionValue="value"
      [ngModel]="selected()"
      (ngModelChange)="selected.set($event)"
      [clearable]="false"
      [itemTpl]="optionTemplate"
      [selectedItemTpl]="optionTemplate">
    </app-clearable-select>
  `
})
class SignalSelectHostComponent {
  readonly selected = signal('all');
  readonly options = [
    { labelKey: 'كل الحالات', value: 'all' },
    { labelKey: 'نشطة', value: 'active' }
  ];
}

describe('ClearableSelectComponent signal binding', () => {
  let fixture: ComponentFixture<SignalSelectHostComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [
        SignalSelectHostComponent,
        TranslateModule.forRoot({
          loader: { provide: TranslateFakeLoader, useClass: TranslateFakeLoader },
          compiler: { provide: TranslateFakeCompiler, useClass: TranslateFakeCompiler }
        })
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SignalSelectHostComponent);
    fixture.detectChanges();
  });

  it('renders the initial selected option when ngModel is explicitly bound to a writable signal value', async () => {
    await fixture.whenStable();
    fixture.detectChanges();
    const select = fixture.debugElement.query(By.directive(ClearableSelectComponent))
      .componentInstance as ClearableSelectComponent;
    const label = fixture.debugElement.query(By.css('.selected-label'));

    expect(select.value()).withContext('the CVA must unwrap the writable signal value').toBe('all');
    expect(label).withContext('the status filter must not render as an empty dropdown').not.toBeNull();
    expect(label?.nativeElement.textContent.trim()).toBe('كل الحالات');
  });
});

/**
 * Regression host for the phantom clear button.
 *
 * Forms across the app declare a required select as
 * `field: ['', Validators.required]`. PrimeNG shows its in-field ✕ whenever
 * `modelValue() != null`, and `''` satisfies that — so an untouched required
 * select rendered a clear button beside its own "اختر" placeholder, offering to
 * clear a value the user had never picked.
 */
@Component({
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, ClearableSelectComponent],
  template: `
    <app-clearable-select
      [options]="options"
      optionLabel="labelKey"
      optionValue="value"
      [formControl]="control">
    </app-clearable-select>
  `
})
class EmptyStringHostComponent {
  readonly control = new FormControl<string>('', { nonNullable: true });
  readonly options = [
    { labelKey: 'نشطة', value: 'active' },
    { labelKey: 'مغلقة', value: 'closed' }
  ];
}

describe('ClearableSelectComponent empty-value normalisation', () => {
  let fixture: ComponentFixture<EmptyStringHostComponent>;
  let select: ClearableSelectComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [
        EmptyStringHostComponent,
        TranslateModule.forRoot({
          loader: { provide: TranslateFakeLoader, useClass: TranslateFakeLoader },
          compiler: { provide: TranslateFakeCompiler, useClass: TranslateFakeCompiler }
        })
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(EmptyStringHostComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    select = fixture.debugElement.query(By.directive(ClearableSelectComponent))
      .componentInstance as ClearableSelectComponent;
  });

  it('treats an empty-string form value as "nothing selected"', () => {
    expect(select.value())
      .withContext('an empty string must not read as a chosen value')
      .toBeNull();
  });

  it('does not render a clear button while nothing is selected', () => {
    expect(fixture.debugElement.query(By.css('.p-dropdown-clear-icon')))
      .withContext('the ✕ must not offer to clear a value the user never picked')
      .toBeNull();
  });

  it('renders the clear button once a real option is chosen', async () => {
    fixture.componentInstance.control.setValue('active');
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(select.value()).toBe('active');
    expect(fixture.debugElement.query(By.css('.p-dropdown-clear-icon')))
      .withContext('a chosen value must be clearable')
      .not.toBeNull();
  });

  it('normalises a whitespace-only value to null', () => {
    fixture.componentInstance.control.setValue('   ');
    fixture.detectChanges();
    expect(select.value()).toBeNull();
  });
});
