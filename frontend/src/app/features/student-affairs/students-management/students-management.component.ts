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
import { InputTextModule } from 'primeng/inputtext';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { finalize } from 'rxjs';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import {
  ClassroomDto,
  StudentDetailsDto,
  StudentListItemDto
} from '../../../core/models/daily-operations.models';
import { DailyOperationsService } from '../../../core/services/daily-operations.service';
import { ClearableSelectComponent } from '../../../shared/components/clearable-select/clearable-select.component';

interface ClassroomOption {
  readonly label: string;
  readonly value: number;
}

@Component({
  selector: 'app-students-management',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    ButtonModule,
    CardModule,
    CheckboxModule,
    ConfirmDialogModule,
    DialogModule,
    InputTextModule,
    ProgressSpinnerModule,
    TableModule,
    TagModule,
    ClearableSelectComponent
  ],
  providers: [ConfirmationService],
  templateUrl: './students-management.component.html',
  styleUrl: './students-management.component.css'
})
export class StudentsManagementComponent {
  private readonly api = inject(DailyOperationsService);
  private readonly messages = inject(MessageService);
  private readonly confirmation = inject(ConfirmationService);

  readonly students = signal<readonly StudentListItemDto[]>([]);
  readonly classrooms = signal<readonly ClassroomDto[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly loadingDetails = signal(false);
  readonly dialogVisible = signal(false);
  readonly editing = signal<StudentDetailsDto | null>(null);
  readonly search = signal('');
  readonly errorMessage = signal('');

  readonly form = new FormGroup({
    studentNumber: new FormControl('', [Validators.required, Validators.maxLength(50)]),
    identityNumber: new FormControl('', [Validators.required, Validators.maxLength(50)]),
    firstName: new FormControl('', [Validators.required, Validators.maxLength(100)]),
    lastName: new FormControl('', [Validators.required, Validators.maxLength(100)]),
    classroomId: new FormControl<number | null>(null),
    isActive: new FormControl(true, { nonNullable: true })
  });

  constructor() {
    this.loadClassrooms();
    this.loadStudents();
  }

  get filteredStudents(): readonly StudentListItemDto[] {
    const term = this.search().trim().toLocaleLowerCase('ar');
    if (!term) return this.students();

    return this.students().filter(item =>
      item.student.displayName.toLocaleLowerCase('ar').includes(term)
      || item.student.studentNumber.toLocaleLowerCase('ar').includes(term)
      || (item.student.identityNumber?.toLocaleLowerCase('ar').includes(term) ?? false)
      || (item.student.classLabel?.toLocaleLowerCase('ar').includes(term) ?? false));
  }

  get classroomOptions(): readonly ClassroomOption[] {
    return this.classrooms()
      .filter(classroom => classroom.isActive)
      .map(classroom => ({
        label: `${classroom.label} — ${classroom.academicYearLabel}`,
        value: classroom.id
      }));
  }

  openCreate(): void {
    this.editing.set(null);
    this.form.reset({
      studentNumber: '',
      identityNumber: '',
      firstName: '',
      lastName: '',
      classroomId: null,
      isActive: true
    });
    this.dialogVisible.set(true);
  }

  openEdit(item: StudentListItemDto): void {
    if (this.loadingDetails()) return;

    this.loadingDetails.set(true);
    this.api.getStudent(item.student.id)
      .pipe(finalize(() => this.loadingDetails.set(false)))
      .subscribe({
        next: response => {
          if (!response.isSuccess || !response.data) {
            this.showError(response.errors[0] ?? response.message ?? 'تعذر تحميل بيانات الطالب.');
            return;
          }

          this.editing.set(response.data);
          this.form.reset({
            studentNumber: response.data.student.studentNumber,
            identityNumber: response.data.identityNumber || response.data.student.identityNumber || '',
            firstName: response.data.firstName,
            lastName: response.data.lastName,
            classroomId: response.data.currentEnrollment?.classroom.id ?? null,
            isActive: response.data.student.isActive
          });
          this.dialogVisible.set(true);
        },
        error: (error: HttpErrorResponse) => this.showError(
          extractHttpErrorMessage(error) ?? 'تعذر تحميل بيانات الطالب.'
        )
      });
  }

  closeDialog(): void {
    if (!this.saving()) this.dialogVisible.set(false);
  }

  save(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.saving()) return;

    const value = this.form.getRawValue();
    const editing = this.editing();
    const commonRequest = {
      studentNumber: value.studentNumber!.trim(),
      identityNumber: value.identityNumber!.trim(),
      firstName: value.firstName!.trim(),
      middleName: editing?.middleName ?? null,
      lastName: value.lastName!.trim(),
      nationalId: editing?.nationalId ?? null,
      dateOfBirth: editing?.dateOfBirth ?? null,
      gender: editing?.gender ?? null,
      classroomId: value.classroomId,
      rollNumber: editing?.currentEnrollment?.rollNumber ?? null
    };

    this.saving.set(true);
    const request$ = editing
      ? this.api.updateStudent(editing.student.id, {
          ...commonRequest,
          isActive: value.isActive,
          rowVersion: editing.rowVersion
        })
      : this.api.createStudent(commonRequest);

    request$.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) {
          this.showError(response.errors[0] ?? response.message ?? 'تعذر حفظ بيانات الطالب.');
          return;
        }

        this.dialogVisible.set(false);
        this.messages.add({
          severity: 'success',
          summary: editing ? 'تم تحديث الطالب' : 'تمت إضافة الطالب',
          detail: `تم حفظ بيانات ${response.data.student.displayName} بنجاح.`
        });
        this.loadStudents();
      },
      error: (error: HttpErrorResponse) => this.showError(
        extractHttpErrorMessage(error) ?? 'تعذر حفظ بيانات الطالب. تحقق من عدم تكرار رقم الطالب أو رقم الهوية.'
      )
    });
  }

  confirmDelete(item: StudentListItemDto): void {
    this.confirmation.confirm({
      header: 'تأكيد حذف الطالب',
      message: `سيتم إخفاء الطالب «${item.student.displayName}» مع الاحتفاظ بسجلات الحضور والاستئذانات التاريخية. هل تريد المتابعة؟`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'حذف الطالب',
      rejectLabel: 'إلغاء',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.deleteStudent(item)
    });
  }

  setSearch(event: Event): void {
    this.search.set((event.target as HTMLInputElement).value);
  }

  loadStudents(): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.api.getStudents().pipe(finalize(() => this.loading.set(false))).subscribe({
      next: response => {
        if (response.isSuccess && response.data) this.students.set(response.data.items);
        else this.errorMessage.set(response.errors[0] ?? response.message ?? 'تعذر تحميل الطلاب.');
      },
      error: (error: HttpErrorResponse) => {
        this.errorMessage.set(extractHttpErrorMessage(error) ?? 'تعذر تحميل الطلاب.');
      }
    });
  }

  private loadClassrooms(): void {
    this.api.getClassrooms().subscribe({
      next: response => {
        if (response.isSuccess && response.data) this.classrooms.set(response.data.items);
        else this.showError(response.errors[0] ?? response.message ?? 'تعذر تحميل الفصول.');
      },
      error: (error: HttpErrorResponse) => this.showError(
        extractHttpErrorMessage(error) ?? 'تعذر تحميل الفصول.'
      )
    });
  }

  private deleteStudent(item: StudentListItemDto): void {
    this.api.deleteStudent(item.student.id, {
      reason: 'حذف من شاشة إدارة الطلاب',
      rowVersion: ''
    }).subscribe({
      next: response => {
        if (!response.isSuccess || response.data !== true) {
          this.showError(response.errors[0] ?? response.message ?? 'تعذر حذف الطالب.');
          return;
        }

        this.messages.add({ severity: 'success', summary: 'تم حذف الطالب', detail: item.student.displayName });
        this.loadStudents();
      },
      error: (error: HttpErrorResponse) => this.showError(
        extractHttpErrorMessage(error) ?? 'تعذر حذف الطالب.'
      )
    });
  }

  private showError(detail: string): void {
    this.messages.add({ severity: 'error', summary: 'تعذر تنفيذ العملية', detail });
  }
}
