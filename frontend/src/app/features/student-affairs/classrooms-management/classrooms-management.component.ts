import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { CheckboxModule } from 'primeng/checkbox';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { finalize } from 'rxjs';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import {
  AcademicYearLookupDto,
  ClassroomDto,
  SchoolStage
} from '../../../core/models/daily-operations.models';
import { DailyOperationsService } from '../../../core/services/daily-operations.service';
import { ClearableSelectComponent } from '../../../shared/components/clearable-select/clearable-select.component';

interface SelectOption<T> {
  readonly label: string;
  readonly value: T;
}

@Component({
  selector: 'app-classrooms-management',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    ButtonModule,
    CardModule,
    CheckboxModule,
    ConfirmDialogModule,
    DialogModule,
    InputNumberModule,
    InputTextModule,
    ProgressSpinnerModule,
    TableModule,
    TagModule,
    ClearableSelectComponent
  ],
  providers: [ConfirmationService],
  templateUrl: './classrooms-management.component.html',
  styleUrl: './classrooms-management.component.css'
})
export class ClassroomsManagementComponent {
  private readonly api = inject(DailyOperationsService);
  private readonly messages = inject(MessageService);
  private readonly confirmation = inject(ConfirmationService);

  readonly classrooms = signal<readonly ClassroomDto[]>([]);
  readonly academicYears = signal<readonly AcademicYearLookupDto[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly dialogVisible = signal(false);
  readonly editing = signal<ClassroomDto | null>(null);
  readonly search = signal('');
  readonly errorMessage = signal('');

  readonly stageOptions: readonly SelectOption<SchoolStage>[] = [
    { label: 'ابتدائي', value: 'Primary' },
    { label: 'متوسط', value: 'Intermediate' },
    { label: 'ثانوي', value: 'Secondary' }
  ];

  readonly form = new FormGroup({
    academicYearId: new FormControl<number | null>(null, Validators.required),
    stage: new FormControl<SchoolStage | null>(null, Validators.required),
    gradeLevel: new FormControl<number | null>(1, [Validators.required, Validators.min(1), Validators.max(12)]),
    section: new FormControl('', [Validators.required, Validators.maxLength(50)]),
    classLabel: new FormControl('', [Validators.required, Validators.maxLength(50)]),
    isActive: new FormControl(true, { nonNullable: true })
  });

  constructor() {
    this.loadAcademicYears();
    this.loadClassrooms();
  }

  get filteredClassrooms(): readonly ClassroomDto[] {
    const term = this.search().trim().toLocaleLowerCase('ar');
    if (!term) return this.classrooms();
    return this.classrooms().filter(classroom =>
      classroom.label.toLocaleLowerCase('ar').includes(term) ||
      classroom.section.toLocaleLowerCase('ar').includes(term) ||
      classroom.academicYearLabel.toLocaleLowerCase('ar').includes(term));
  }

  get academicYearOptions(): readonly SelectOption<number>[] {
    return this.academicYears().map(year => ({
      label: `${year.nameAr} (${year.code})`,
      value: year.id
    }));
  }

  openCreate(): void {
    this.editing.set(null);
    this.setImmutableFieldsDisabled(false);
    const preferredYear = this.academicYears().find(year => year.isActive) ?? this.academicYears()[0];
    this.form.reset({
      academicYearId: preferredYear?.id ?? null,
      stage: null,
      gradeLevel: 1,
      section: '',
      classLabel: '',
      isActive: true
    });
    this.dialogVisible.set(true);
  }

  openEdit(classroom: ClassroomDto): void {
    this.editing.set(classroom);
    this.form.reset({
      academicYearId: classroom.academicYearId,
      stage: classroom.stage as SchoolStage,
      gradeLevel: classroom.gradeLevel,
      section: classroom.section,
      classLabel: classroom.label,
      isActive: classroom.isActive
    });
    this.setImmutableFieldsDisabled(true);
    this.dialogVisible.set(true);
  }

  closeDialog(): void {
    if (!this.saving()) this.dialogVisible.set(false);
  }

  save(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.saving()) return;

    const value = this.form.getRawValue();
    const editing = this.editing();
    this.saving.set(true);
    const request$ = editing
      ? this.api.updateClassroom(editing.id, {
          classLabel: value.classLabel!.trim(),
          section: value.section!.trim(),
          isActive: value.isActive,
          rowVersion: editing.rowVersion
        })
      : this.api.createClassroom({
          academicYearId: value.academicYearId!,
          stage: value.stage!,
          gradeLevel: value.gradeLevel!,
          section: value.section!.trim(),
          classLabel: value.classLabel!.trim()
        });

    request$.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) {
          this.showError(response.errors[0] ?? response.message ?? 'تعذر حفظ بيانات الفصل.');
          return;
        }
        this.dialogVisible.set(false);
        this.messages.add({
          severity: 'success',
          summary: editing ? 'تم تحديث الفصل' : 'تمت إضافة الفصل',
          detail: `تم حفظ الفصل ${response.data.label} بنجاح.`
        });
        this.loadClassrooms();
      },
      error: (error: HttpErrorResponse) => this.showError(
        extractHttpErrorMessage(error) ?? 'تعذر حفظ بيانات الفصل. تحقق من عدم تكرار اسم الفصل.'
      )
    });
  }

  confirmArchive(classroom: ClassroomDto): void {
    const hasActiveStudents = classroom.activeEnrollmentCount > 0;

    this.confirmation.confirm({
      header: hasActiveStudents ? 'تحذير: الفصل يحتوي على طلاب' : 'تأكيد حذف الفصل',
      message: hasActiveStudents
        ? 'سيتم إخفاء/إلغاء تعيين جميع الطلاب في هذا الفصل. هل أنت متأكد؟'
        : `سيتم حذف الفصل «${classroom.label}» من القوائم النشطة. هل تريد المتابعة؟`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'حذف الفصل',
      rejectLabel: 'إلغاء',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.archive(classroom, hasActiveStudents)
    });
  }

  stageLabel(stage: string): string {
    return this.stageOptions.find(option => option.value === stage)?.label ?? stage;
  }

  setSearch(event: Event): void {
    this.search.set((event.target as HTMLInputElement).value);
  }

  private loadAcademicYears(): void {
    this.api.getAcademicYears().subscribe({
      next: response => {
        if (response.isSuccess && response.data) this.academicYears.set(response.data);
        else this.showError(response.errors[0] ?? response.message ?? 'تعذر تحميل الأعوام الدراسية.');
      },
      error: (error: HttpErrorResponse) => this.showError(
        extractHttpErrorMessage(error) ?? 'تعذر تحميل الأعوام الدراسية.'
      )
    });
  }

  loadClassrooms(): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.api.getClassrooms().pipe(finalize(() => this.loading.set(false))).subscribe({
      next: response => {
        if (response.isSuccess && response.data) this.classrooms.set(response.data.items);
        else this.errorMessage.set(response.errors[0] ?? response.message ?? 'تعذر تحميل الفصول.');
      },
      error: (error: HttpErrorResponse) => {
        this.errorMessage.set(extractHttpErrorMessage(error) ?? 'تعذر تحميل الفصول.');
      }
    });
  }

  private archive(classroom: ClassroomDto, forceDelete: boolean): void {
    this.api.deleteClassroom(classroom.id, {
      reason: 'حذف من شاشة إدارة الفصول',
      rowVersion: classroom.rowVersion,
      forceDelete
    }).subscribe({
      next: response => {
        if (!response.isSuccess || response.data !== true) {
          this.showError(response.errors[0] ?? response.message ?? 'تعذر حذف الفصل.');
          return;
        }
        this.messages.add({ severity: 'success', summary: 'تم حذف الفصل', detail: classroom.label });
        this.loadClassrooms();
      },
      error: (error: HttpErrorResponse) => this.showError(
        extractHttpErrorMessage(error) ?? 'تعذر حذف الفصل.'
      )
    });
  }

  private showError(detail: string): void {
    this.messages.add({ severity: 'error', summary: 'تعذر تنفيذ العملية', detail });
  }

  private setImmutableFieldsDisabled(disabled: boolean): void {
    const controls = [this.form.controls.academicYearId, this.form.controls.stage, this.form.controls.gradeLevel];
    controls.forEach(control => disabled
      ? control.disable({ emitEvent: false })
      : control.enable({ emitEvent: false }));
  }
}
