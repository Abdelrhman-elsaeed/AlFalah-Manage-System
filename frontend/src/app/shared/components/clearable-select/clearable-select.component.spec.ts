import { CommonModule } from '@angular/common';
import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
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
