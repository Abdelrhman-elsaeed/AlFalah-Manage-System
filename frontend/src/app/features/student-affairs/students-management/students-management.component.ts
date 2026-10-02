import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
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
import { finalize, forkJoin } from 'rxjs';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import {
  ClassroomDto,
  GuardianDirectoryOptionDto,
  GuardianRelationshipType,
  StudentDetailsDto,
  StudentGuardianLinkDto,
  StudentListItemDto
} from '../../../core/models/daily-operations.models';
import { DailyOperationsService } from '../../../core/services/daily-operations.service';
import { ClearableSelectComponent } from '../../../shared/components/clearable-select/clearable-select.component';

interface ClassroomOption {
  readonly label: string;
  readonly value: number;
}

interface SelectOption<T> {
  readonly label: string;
  readonly value: T;
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
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly students = signal<readonly StudentListItemDto[]>([]);
  readonly classrooms = signal<readonly ClassroomDto[]>([]);
  readonly loading = signal(true);
  readonly loadingClassrooms = signal(true);
  readonly saving = signal(false);
  readonly loadingDetails = signal(false);
  readonly dialogVisible = signal(false);
  readonly guardianDialogVisible = signal(false);
  readonly transferDialogVisible = signal(false);
  readonly editing = signal<StudentDetailsDto | null>(null);
  readonly guardianStudent = signal<StudentListItemDto | null>(null);
  readonly transferStudentDetails = signal<StudentDetailsDto | null>(null);
  readonly guardianOptions = signal<readonly GuardianDirectoryOptionDto[]>([]);
  readonly guardianLinks = signal<readonly StudentGuardianLinkDto[]>([]);
  readonly loadingGuardians = signal(false);
  readonly guardianSaving = signal(false);
  readonly transferSaving = signal(false);
  readonly search = signal('');
  readonly errorMessage = signal('');
  readonly listVisible = signal(false);
  readonly selectedClassroomId = signal<number | null>(null);
  readonly totalStudents = signal(0);
  readonly selectedStudentsTotal = signal(0);

  readonly form = new FormGroup({
    studentNumber: new FormControl('', [Validators.required, Validators.maxLength(50)]),
    identityNumber: new FormControl('', [Validators.required, Validators.maxLength(50)]),
    firstName: new FormControl('', [Validators.required, Validators.maxLength(100)]),
    lastName: new FormControl('', [Validators.required, Validators.maxLength(100)]),
    classroomId: new FormControl<number | null>(null),
    isActive: new FormControl(true, { nonNullable: true })
  });

  readonly guardianForm = new FormGroup({
    guardianProfileId: new FormControl<number | null>(null, Validators.required),
    relationship: new FormControl<GuardianRelationshipType>('Father', { nonNullable: true, validators: Validators.required }),
    isPrimary: new FormControl(false, { nonNullable: true }),
    receivesNotifications: new FormControl(true, { nonNullable: true }),
    canSubmitExcuses: new FormControl(true, { nonNullable: true }),
    canRequestGatePass: new FormControl(true, { nonNullable: true })
  });

  readonly transferForm = new FormGroup({
    classroomId: new FormControl<number | null>(null, Validators.required),
    effectiveOn: new FormControl(this.today(), { nonNullable: true, validators: Validators.required })
  });

  readonly relationshipOptions: readonly SelectOption<GuardianRelationshipType>[] = [
    { label: 'الأب', value: 'Father' },
    { label: 'الأم', value: 'Mother' },
    { label: 'الوصي القانوني', value: 'LegalGuardian' },
    { label: 'صلة أخرى', value: 'Other' }
  ];

  constructor() {
    const listMode = this.route.snapshot.data['studentListMode'] as 'all' | 'classroom' | undefined;
    if (listMode === 'all') {
      this.listVisible.set(true);
      this.selectedClassroomId.set(null);
    } else if (listMode === 'classroom') {
      const classroomId = Number(this.route.snapshot.paramMap.get('classroomId'));
      if (Number.isInteger(classroomId) && classroomId > 0) {
        this.listVisible.set(true);
        this.selectedClassroomId.set(classroomId);
      }
    }

    this.loadClassrooms();
    this.loadStudents(this.selectedClassroomId());
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

  get activeClassrooms(): readonly ClassroomDto[] {
    return this.classrooms().filter(classroom => classroom.isActive);
  }

  get selectedListTitle(): string {
    const classroomId = this.selectedClassroomId();
    if (classroomId === null) return 'جميع طلاب المدرسة';
    return this.classrooms().find(classroom => classroom.id === classroomId)?.label ?? 'طلاب الفصل';
  }

  get classroomOptions(): readonly ClassroomOption[] {
    return this.classrooms()
      .filter(classroom => classroom.isActive)
      .map(classroom => ({
        label: `${classroom.label} — ${classroom.academicYearLabel}`,
        value: classroom.id
      }));
  }

  get transferClassroomOptions(): readonly ClassroomOption[] {
    const currentClassroomId = this.transferStudentDetails()?.currentEnrollment?.classroom.id;
    return this.classroomOptions.filter(option => option.value !== currentClassroomId);
  }

  get availableGuardianOptions(): readonly SelectOption<number>[] {
    const linkedIds = new Set(this.guardianLinks().map(link => link.guardian.id));
    return this.guardianOptions()
      .filter(option => !linkedIds.has(option.id))
      .map(option => ({
        label: `${option.displayName} — ${option.username}${option.phoneNumber ? ` — ${option.phoneNumber}` : ''}`,
        value: option.id
      }));
  }

  openCreate(classroomId: number | null = null, event?: Event): void {
    event?.stopPropagation();
    this.editing.set(null);
    this.form.reset({
      studentNumber: '',
      identityNumber: '',
      firstName: '',
      lastName: '',
      classroomId,
      isActive: true
    });
    this.dialogVisible.set(true);
  }

  openStudentList(classroomId: number | null): void {
    const commands = classroomId === null
      ? ['/student-affairs/students/all']
      : ['/student-affairs/students/classroom', classroomId];
    void this.router.navigate(commands);
  }

  backToClassrooms(): void {
    void this.router.navigate(['/student-affairs/students']);
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

  openGuardians(item: StudentListItemDto): void {
    if (this.loadingGuardians()) return;

    this.guardianStudent.set(item);
    this.guardianDialogVisible.set(true);
    this.loadingGuardians.set(true);
    this.guardianForm.reset({
      guardianProfileId: null,
      relationship: 'Father',
      isPrimary: false,
      receivesNotifications: true,
      canSubmitExcuses: true,
      canRequestGatePass: true
    });

    forkJoin({
      options: this.api.getGuardianOptions(),
      links: this.api.getStudentGuardians(item.student.id)
    }).pipe(finalize(() => this.loadingGuardians.set(false))).subscribe({
      next: ({ options, links }) => {
        if (!options.isSuccess || !options.data || !links.isSuccess || !links.data) {
          this.showError(options.errors[0] ?? links.errors[0] ?? options.message ?? links.message ?? 'تعذر تحميل بيانات أولياء الأمور.');
          return;
        }
        this.guardianOptions.set(options.data);
        this.guardianLinks.set(links.data);
        this.guardianForm.controls.isPrimary.setValue(!links.data.some(link => link.isActive && link.guardian.isPrimary));
      },
      error: (error: HttpErrorResponse) => this.showError(
        extractHttpErrorMessage(error) ?? 'تعذر تحميل بيانات أولياء الأمور.'
      )
    });
  }

  closeGuardianDialog(): void {
    if (!this.guardianSaving()) this.guardianDialogVisible.set(false);
  }

  linkGuardian(): void {
    this.guardianForm.markAllAsTouched();
    const student = this.guardianStudent();
    const value = this.guardianForm.getRawValue();
    if (!student || this.guardianForm.invalid || value.guardianProfileId === null || this.guardianSaving()) return;

    this.guardianSaving.set(true);
    this.api.linkStudentGuardian(student.student.id, {
      guardianProfileId: value.guardianProfileId,
      relationship: value.relationship,
      isPrimary: value.isPrimary,
      receivesNotifications: value.receivesNotifications,
      canSubmitExcuses: value.canSubmitExcuses,
      canRequestGatePass: value.canRequestGatePass,
      validFrom: this.today(),
      validTo: null
    }).pipe(finalize(() => this.guardianSaving.set(false))).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) {
          this.showError(response.errors[0] ?? response.message ?? 'تعذر ربط ولي الأمر بالطالب.');
          return;
        }
        this.guardianLinks.update(links => [...links, response.data!]);
        this.guardianForm.reset({
          guardianProfileId: null,
          relationship: 'Father',
          isPrimary: false,
          receivesNotifications: true,
          canSubmitExcuses: true,
          canRequestGatePass: true
        });
        this.messages.add({ severity: 'success', summary: 'تم ربط ولي الأمر', detail: response.data.guardian.displayName });
      },
      error: (error: HttpErrorResponse) => this.showError(
        extractHttpErrorMessage(error) ?? 'تعذر ربط ولي الأمر بالطالب.'
      )
    });
  }

  confirmRevokeGuardian(link: StudentGuardianLinkDto): void {
    const student = this.guardianStudent();
    if (!student) return;
    this.confirmation.confirm({
      header: 'إلغاء ربط ولي الأمر',
      message: `هل تريد إلغاء ربط «${link.guardian.displayName}» بالطالب «${student.student.displayName}»؟`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'إلغاء الربط',
      rejectLabel: 'تراجع',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.revokeGuardian(link)
    });
  }

  openTransfer(item: StudentListItemDto): void {
    if (this.loadingDetails()) return;
    this.loadingDetails.set(true);
    this.api.getStudent(item.student.id)
      .pipe(finalize(() => this.loadingDetails.set(false)))
      .subscribe({
        next: response => {
          if (!response.isSuccess || !response.data) {
            this.showError(response.errors[0] ?? response.message ?? 'تعذر تحميل بيانات تسجيل الطالب.');
            return;
          }
          if (!response.data.currentEnrollment) {
            this.showError('الطالب غير مسجل في فصل حاليًا، استخدم تعديل الطالب لإسناده إلى فصل.');
            return;
          }
          this.transferStudentDetails.set(response.data);
          this.transferForm.reset({ classroomId: null, effectiveOn: this.today() });
          this.transferDialogVisible.set(true);
        },
        error: (error: HttpErrorResponse) => this.showError(
          extractHttpErrorMessage(error) ?? 'تعذر تحميل بيانات تسجيل الطالب.'
        )
      });
  }

  closeTransferDialog(): void {
    if (!this.transferSaving()) this.transferDialogVisible.set(false);
  }

  transferStudent(): void {
    this.transferForm.markAllAsTouched();
    const details = this.transferStudentDetails();
    const enrollment = details?.currentEnrollment;
    const value = this.transferForm.getRawValue();
    if (!details || !enrollment || this.transferForm.invalid || value.classroomId === null || this.transferSaving()) return;

    this.transferSaving.set(true);
    this.api.transferStudent(details.student.id, enrollment.id, {
      status: 'Active',
      classroomId: value.classroomId,
      effectiveOn: value.effectiveOn,
      reason: 'نقل الطالب إلى فصل آخر من شاشة إدارة الطلاب',
      rowVersion: enrollment.rowVersion
    }).pipe(finalize(() => this.transferSaving.set(false))).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) {
          this.showError(response.errors[0] ?? response.message ?? 'تعذر نقل الطالب إلى الفصل المحدد.');
          return;
        }
        this.transferDialogVisible.set(false);
        this.messages.add({
          severity: 'success',
          summary: 'تم نقل الطالب',
          detail: `تم نقل ${details.student.displayName} إلى ${response.data.classroom.label}.`
        });
        this.refreshStudentView();
      },
      error: (error: HttpErrorResponse) => this.showError(
        extractHttpErrorMessage(error) ?? 'تعذر نقل الطالب إلى الفصل المحدد.'
      )
    });
  }

  guardianRelationshipLabel(relationship: string): string {
    return this.relationshipOptions.find(option => option.value === relationship)?.label ?? relationship;
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
        this.refreshStudentView();
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

  loadStudents(classroomId: number | null = this.selectedClassroomId()): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.api.getStudents(classroomId === null ? undefined : { classroomId })
      .pipe(finalize(() => this.loading.set(false))).subscribe({
      next: response => {
        if (response.isSuccess && response.data) {
          this.students.set(response.data.items);
          this.selectedStudentsTotal.set(response.data.totalCount);
          if (classroomId === null) this.totalStudents.set(response.data.totalCount);
        }
        else this.errorMessage.set(response.errors[0] ?? response.message ?? 'تعذر تحميل الطلاب.');
      },
      error: (error: HttpErrorResponse) => {
        this.errorMessage.set(extractHttpErrorMessage(error) ?? 'تعذر تحميل الطلاب.');
      }
    });
  }

  private loadClassrooms(): void {
    this.loadingClassrooms.set(true);
    this.api.getClassrooms().pipe(finalize(() => this.loadingClassrooms.set(false))).subscribe({
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
        this.refreshStudentView();
      },
      error: (error: HttpErrorResponse) => this.showError(
        extractHttpErrorMessage(error) ?? 'تعذر حذف الطالب.'
      )
    });
  }

  private revokeGuardian(link: StudentGuardianLinkDto): void {
    const student = this.guardianStudent();
    if (!student || this.guardianSaving()) return;
    this.guardianSaving.set(true);
    this.api.revokeStudentGuardian(student.student.id, link.id, {
      reason: 'إلغاء الربط من شاشة إدارة الطلاب',
      rowVersion: link.rowVersion
    }).pipe(finalize(() => this.guardianSaving.set(false))).subscribe({
      next: response => {
        if (!response.isSuccess || response.data !== true) {
          this.showError(response.errors[0] ?? response.message ?? 'تعذر إلغاء ربط ولي الأمر.');
          return;
        }
        this.guardianLinks.update(links => links.map(item => item.id === link.id
          ? { ...item, isActive: false, validTo: this.today() }
          : item));
        if (link.guardian.isPrimary) this.guardianForm.controls.isPrimary.setValue(true);
        this.messages.add({ severity: 'success', summary: 'تم إلغاء الربط', detail: link.guardian.displayName });
      },
      error: (error: HttpErrorResponse) => this.showError(
        extractHttpErrorMessage(error) ?? 'تعذر إلغاء ربط ولي الأمر.'
      )
    });
  }

  private refreshStudentView(): void {
    this.loadClassrooms();
    this.loadStudents(this.selectedClassroomId());
    if (this.selectedClassroomId() !== null) this.loadSchoolStudentCount();
  }

  private loadSchoolStudentCount(): void {
    this.api.getStudents({ pageSize: 1 }).subscribe({
      next: response => {
        if (response.isSuccess && response.data) this.totalStudents.set(response.data.totalCount);
      }
    });
  }

  private showError(detail: string): void {
    this.messages.add({ severity: 'error', summary: 'تعذر تنفيذ العملية', detail });
  }

  private today(): string {
    const now = new Date();
    return new Date(now.getTime() - now.getTimezoneOffset() * 60_000).toISOString().slice(0, 10);
  }
}
