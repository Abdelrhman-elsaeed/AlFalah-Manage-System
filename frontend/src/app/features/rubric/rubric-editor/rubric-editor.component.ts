import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormArray, FormControl, Validators, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { InputNumberModule } from 'primeng/inputnumber';
import { RubricService } from '../../../core/services/rubric.service';
import {
  RubricVersionDto, RubricDomainDto, RubricStandardDto,
  CreateRubricVersionDto, RubricDomainWriteDto, RubricStandardWriteDto
} from '../../../core/models/rubric.models';
import { ToastService } from '../../../core/services/toast.service';

interface StandardForm {
  code: FormControl<string>;
  textAr: FormControl<string>;
  sortOrder: FormControl<number>;
}

interface DomainForm {
  code: FormControl<string>;
  nameAr: FormControl<string>;
  sortOrder: FormControl<number>;
  standards: FormArray<FormGroup<StandardForm>>;
}

@Component({
  selector: 'app-rubric-editor',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ReactiveFormsModule, TranslateModule,
    ProgressSpinnerModule, ButtonModule, InputTextModule, InputTextareaModule, InputNumberModule
  ],
  templateUrl: './rubric-editor.component.html',
  styleUrls: ['./rubric-editor.component.css']
})
export class RubricEditorComponent implements OnInit {
  private readonly rubricService = inject(RubricService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly sourceVersion = signal<RubricVersionDto | null>(null);

  readonly notesControl = new FormControl<string>('', { nonNullable: true, validators: [Validators.maxLength(2000)] });

  readonly form: FormGroup<{ domains: FormArray<FormGroup<DomainForm>> }> = this.fb.group({
    domains: this.fb.array<FormGroup<DomainForm>>([])
  });

  readonly totalStandards = computed(() => {
    let total = 0;
    for (const g of this.form.controls.domains.controls) {
      total += g.controls.standards.length;
    }
    return total;
  });

  ngOnInit(): void {
    this.loadActive();
  }

  get domains(): FormArray<FormGroup<DomainForm>> {
    return this.form.controls.domains;
  }

  standardsFor(domainIndex: number): FormArray<FormGroup<StandardForm>> {
    return this.domains.at(domainIndex).controls.standards;
  }

  trackDomain(_idx: number, _g: FormGroup<DomainForm>): number { return _idx; }
  trackStandard(_idx: number, _g: FormGroup<StandardForm>): number { return _idx; }

  private loadActive(): void {
    this.loading.set(true);
    this.rubricService.getActive().subscribe({
      next: (res) => {
        if (res.isSuccess && res.data) {
          this.sourceVersion.set(res.data);
          this.notesControl.setValue(res.data.notes ?? '');
          this.populateForm(res.data);
        } else {
          this.toast.error(
            this.translate.instant('RUBRIC.LOAD_FAILED'),
            res.message || this.translate.instant('ERRORS.SERVER_ERROR')
          );
        }
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.error(
          this.translate.instant('RUBRIC.LOAD_FAILED'),
          this.translate.instant('ERRORS.NETWORK_ERROR')
        );
      }
    });
  }

  private populateForm(v: RubricVersionDto): void {
    // Reset array
    while (this.domains.length > 0) this.domains.removeAt(0);

    for (const d of v.domains) {
      const standardsArr = this.fb.array<FormGroup<StandardForm>>(
        d.standards.map(s => this.fb.group<StandardForm>({
          code: this.fb.nonNullable.control(s.code, [Validators.required, Validators.maxLength(20)]),
          textAr: this.fb.nonNullable.control(s.textAr, [Validators.required, Validators.maxLength(1000)]),
          sortOrder: this.fb.nonNullable.control(s.sortOrder, [Validators.required, Validators.min(1)])
        }))
      );
      const domainGroup = this.fb.group<DomainForm>({
        code: this.fb.nonNullable.control(d.code, [Validators.required, Validators.maxLength(20)]),
        nameAr: this.fb.nonNullable.control(d.nameAr, [Validators.required, Validators.maxLength(300)]),
        sortOrder: this.fb.nonNullable.control(d.sortOrder, [Validators.required, Validators.min(1)]),
        standards: standardsArr
      });
      this.domains.push(domainGroup);
    }
  }

  moveDomainUp(index: number): void {
    if (index <= 0) return;
    const g = this.domains.at(index);
    this.domains.removeAt(index);
    this.domains.insert(index - 1, g);
    this.renumberDomainSortOrders();
  }

  moveDomainDown(index: number): void {
    if (index >= this.domains.length - 1) return;
    const g = this.domains.at(index);
    this.domains.removeAt(index);
    this.domains.insert(index + 1, g);
    this.renumberDomainSortOrders();
  }

  moveStandardUp(domainIndex: number, stdIndex: number): void {
    const arr = this.standardsFor(domainIndex);
    if (stdIndex <= 0) return;
    const g = arr.at(stdIndex);
    arr.removeAt(stdIndex);
    arr.insert(stdIndex - 1, g);
    this.renumberStandardSortOrders(domainIndex);
  }

  moveStandardDown(domainIndex: number, stdIndex: number): void {
    const arr = this.standardsFor(domainIndex);
    if (stdIndex >= arr.length - 1) return;
    const g = arr.at(stdIndex);
    arr.removeAt(stdIndex);
    arr.insert(stdIndex + 1, g);
    this.renumberStandardSortOrders(domainIndex);
  }

  private renumberDomainSortOrders(): void {
    this.domains.controls.forEach((g, idx) => g.controls.sortOrder.setValue(idx + 1));
  }

  private renumberStandardSortOrders(domainIndex: number): void {
    const arr = this.standardsFor(domainIndex);
    arr.controls.forEach((g, idx) => g.controls.sortOrder.setValue(idx + 1));
  }

  cancel(): void {
    this.router.navigate(['/rubric']);
  }

  saveNewVersion(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.toast.error(
        this.translate.instant('RUBRIC.VALIDATION_FAILED'),
        this.translate.instant('RUBRIC.VALIDATION_FAILED_DESC')
      );
      return;
    }

    const payload: CreateRubricVersionDto = {
      notes: this.notesControl.value || undefined,
      domains: this.domains.controls.map< RubricDomainWriteDto>((d, dIdx) => {
        const dVal = d.getRawValue();
        return {
          code: dVal.code.trim(),
          nameAr: dVal.nameAr.trim(),
          sortOrder: dIdx + 1,
          standards: dVal.standards.map<RubricStandardWriteDto>((s, sIdx) => ({
            code: s.code.trim(),
            textAr: s.textAr.trim(),
            sortOrder: sIdx + 1
          }))
        };
      })
    };

    this.saving.set(true);
    this.rubricService.createVersion(payload).subscribe({
      next: (res) => {
        this.saving.set(false);
        if (res.isSuccess && res.data) {
          this.toast.success(
            this.translate.instant('RUBRIC.SAVE_SUCCESS_TITLE'),
            this.translate.instant('RUBRIC.SAVE_SUCCESS_DESC', { version: res.data.versionNumber })
          );
          this.router.navigate(['/rubric']);
        } else {
          const errs = res.errors?.length ? res.errors.join(' / ') : res.message;
          this.toast.error(this.translate.instant('RUBRIC.SAVE_FAILED'), errs || this.translate.instant('ERRORS.SERVER_ERROR'));
        }
      },
      error: () => {
        this.saving.set(false);
        this.toast.error(
          this.translate.instant('RUBRIC.SAVE_FAILED'),
          this.translate.instant('ERRORS.NETWORK_ERROR')
        );
      }
    });
  }
}