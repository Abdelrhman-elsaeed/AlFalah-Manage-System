import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { ClearableSelectComponent } from '../../../shared/components/clearable-select/clearable-select.component';
import { InputTextModule } from 'primeng/inputtext';
import { AuthService } from '../../../core/services/auth.service';
import { SchoolsService } from '../../../core/services/schools.service';
import { ToastService } from '../../../core/services/toast.service';
import { UsersService } from '../../../core/services/users.service';
import { PhaseTwoRole, SchoolStage, UserDetail } from '../../../core/models/phase2.models';

@Component({
  selector: 'app-user-form',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, RouterLink, TranslateModule,
    ButtonModule, ClearableSelectComponent, InputTextModule
  ],
  templateUrl: './user-form.component.html',
  styleUrls: ['./user-form.component.css']
})
export class UserFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly usersService = inject(UsersService);
  private readonly schoolsService = inject(SchoolsService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);

  readonly isEdit = signal(false);
  readonly saving = signal(false);
  readonly passwordVisible = signal(false);
  readonly userId = signal<string | null>(null);
  readonly isInstructor = signal(false);
  readonly isSecretary = signal(false);
  readonly isSchoolManager = computed(() =>
    this.auth.hasRole('SchoolManager')
    && !this.auth.hasRole('SuperAdmin')
    && !this.auth.hasRole('MainManager'));

  readonly form: FormGroup = this.fb.group({
    username: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(256)]],
    password: ['', [Validators.minLength(8)]],
    fullName: [''],
    firstName: ['', [Validators.required, Validators.maxLength(100)]],
    lastName: ['', [Validators.required, Validators.maxLength(100)]],
    email: ['', Validators.email],
    phoneNumber: [''],
    preferredLanguage: ['ar'],
    role: ['Moderator' as PhaseTwoRole, Validators.required],
    schoolId: [null],
    employeeNumber: [''],
    subject: [''],
    stage: [null]
  });

  readonly roleOptions = [
    { label: this.translate.instant('USERS.ROLE_SCHOOL_MANAGER'), value: 'SchoolManager' },
    { label: 'السكرتير', value: 'Secretary' },
    { label: this.translate.instant('USERS.ROLE_MODERATOR'), value: 'Moderator' },
    { label: this.translate.instant('USERS.ROLE_INSTRUCTOR'), value: 'Instructor' }
  ];
  readonly stageOptions: { label: string; value: SchoolStage }[] = [
    { label: this.translate.instant('SCHOOLS.STAGE.PRIMARY'), value: 'Primary' },
    { label: this.translate.instant('SCHOOLS.STAGE.INTERMEDIATE'), value: 'Intermediate' },
    { label: this.translate.instant('SCHOOLS.STAGE.SECONDARY'), value: 'Secondary' }
  ];
  readonly languageOptions = [
    { label: this.translate.instant('USERS.LANGUAGE_ARABIC'), value: 'ar' },
    { label: this.translate.instant('USERS.LANGUAGE_ENGLISH'), value: 'en' }
  ];

  readonly availableSchools = signal<{ id: number; name: string }[]>([]);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    const requestedRole = this.route.snapshot.queryParamMap.get('role') as PhaseTwoRole | null;
    if (id) {
      this.isEdit.set(true);
      this.userId.set(id);
      this.form.controls['role'].disable({ emitEvent: false });
      this.form.controls['password'].clearValidators();
      this.form.controls['password'].updateValueAndValidity();
      this.loadUser(id);
    } else {
      this.form.controls['password'].setValidators([Validators.required, Validators.minLength(8)]);
      this.form.controls['password'].updateValueAndValidity();
      if (requestedRole === 'Instructor') this.form.patchValue({ role: requestedRole });
      this.setRoleMode(this.form.controls['role'].value as PhaseTwoRole);
    }

    this.form.controls['role'].valueChanges.subscribe(role => this.setRoleMode(role as PhaseTwoRole));
    this.form.controls['employeeNumber'].valueChanges.subscribe(() => this.syncInstructorDefaultPassword());
    this.loadSchools();
  }

  loadUser(id: string): void {
    this.usersService.getById(id).subscribe({
      next: response => {
        if (response.isSuccess && response.data) this.applyForm(response.data);
      }
    });
  }

  applyForm(user: UserDetail): void {
    this.form.patchValue({
      username: user.username,
      fullName: user.fullName,
      firstName: user.firstName,
      lastName: user.lastName,
      email: user.email ?? '',
      phoneNumber: user.phoneNumber ?? '',
      preferredLanguage: user.preferredLanguage ?? 'ar',
      role: (user.roles[0] as PhaseTwoRole) ?? 'Moderator',
      schoolId: user.schools[0]?.schoolId ?? null,
      employeeNumber: user.employeeNumber ?? '',
      subject: user.subject ?? '',
      stage: this.normalizeStage(user.stage)
    });
    this.setRoleMode((user.roles[0] as PhaseTwoRole) ?? 'Moderator');
  }

  loadSchools(): void {
    this.schoolsService.list({ isActive: true, page: 1, pageSize: 200 }).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) return;
        this.availableSchools.set(response.data.items.map(s => ({ id: s.id, name: `${s.name} - ${s.city}` })));
        const activeSchoolId = this.auth.activeSchoolId();
        if (this.isSchoolManager() && activeSchoolId) {
          this.form.patchValue({ schoolId: activeSchoolId });
          this.form.controls['schoolId'].disable({ emitEvent: false });
        }
      }
    });
  }

  onSubmit(): void {
    this.syncNameParts();
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.saving.set(true);
    const teacherFields = this.isInstructor()
      ? {
          fullName: value.fullName.trim(),
          employeeNumber: value.employeeNumber.trim(),
          subject: value.subject.trim(),
          stage: this.stageToApiValue(value.stage),
          schoolId: value.schoolId ?? undefined
        }
      : {};

    if (this.isEdit()) {
      this.usersService.update(this.userId()!, {
        firstName: value.firstName.trim(),
        lastName: value.lastName.trim(),
        email: value.email || undefined,
        phoneNumber: value.phoneNumber || undefined,
        preferredLanguage: value.preferredLanguage ?? 'ar',
        ...teacherFields
      }).subscribe({
        next: response => {
          this.saving.set(false);
          if (response.isSuccess) {
            this.toast.success(this.translate.instant('TEACHERS.SAVE_SUCCESS_TITLE'), response.message || '');
            this.router.navigate(this.isInstructor() ? ['/teachers'] : ['/users']);
          }
        },
        error: () => this.saving.set(false)
      });
      return;
    }

    this.usersService.create({
      username: value.username.trim(),
      password: value.password,
      firstName: value.firstName.trim(),
      lastName: value.lastName.trim(),
      email: value.email || undefined,
      phoneNumber: value.phoneNumber || undefined,
      preferredLanguage: value.preferredLanguage ?? 'ar',
      role: value.role as PhaseTwoRole,
      schoolId: value.schoolId ?? undefined,
      ...teacherFields
    }).subscribe({
      next: response => {
        this.saving.set(false);
        if (response.isSuccess) {
          this.toast.success(this.translate.instant('TEACHERS.SAVE_SUCCESS_TITLE'), response.message || '');
          this.router.navigate(this.isInstructor() ? ['/teachers'] : ['/users']);
        }
      },
      error: () => this.saving.set(false)
    });
  }

  private setInstructorMode(instructor: boolean): void {
    this.isInstructor.set(instructor);
    const required = instructor ? [Validators.required] : [];
    this.form.controls['fullName'].setValidators(instructor ? [Validators.required, Validators.maxLength(200)] : []);
    this.form.controls['employeeNumber'].setValidators(instructor ? [Validators.required, Validators.maxLength(50)] : []);
    this.form.controls['subject'].setValidators(instructor ? [Validators.required, Validators.maxLength(200)] : []);
    this.form.controls['stage'].setValidators(required);
    this.form.controls['schoolId'].setValidators(instructor || this.isSecretary() ? [Validators.required] : []);
    this.form.controls['firstName'].setValidators(instructor ? [] : [Validators.required, Validators.maxLength(100)]);
    this.form.controls['lastName'].setValidators(instructor ? [] : [Validators.required, Validators.maxLength(100)]);
    if (!this.isEdit()) {
      this.form.controls['password'].setValidators(
        instructor ? [Validators.required] : [Validators.required, Validators.minLength(8)]
      );
      this.syncInstructorDefaultPassword();
    }
    Object.values(this.form.controls).forEach(control => control.updateValueAndValidity({ emitEvent: false }));
  }

  private setRoleMode(role: PhaseTwoRole): void {
    this.isSecretary.set(role === 'Secretary');
    this.setInstructorMode(role === 'Instructor');
  }

  private syncInstructorDefaultPassword(): void {
    if (this.isEdit() || !this.isInstructor()) return;
    const employeeNumber = String(this.form.controls['employeeNumber'].value ?? '').trim();
    this.form.controls['password'].setValue(employeeNumber, { emitEvent: false });
  }

  private normalizeStage(stage: SchoolStage | number | null | undefined): SchoolStage | null {
    if (typeof stage === 'string') return stage;
    return stage === 1 ? 'Primary' : stage === 2 ? 'Intermediate' : stage === 3 ? 'Secondary' : null;
  }

  private stageToApiValue(stage: SchoolStage | number | null | undefined): number | undefined {
    if (typeof stage === 'number') return stage;
    return stage === 'Primary' ? 1 : stage === 'Intermediate' ? 2 : stage === 'Secondary' ? 3 : undefined;
  }

  private syncNameParts(): void {
    if (!this.isInstructor()) return;
    const parts = String(this.form.controls['fullName'].value ?? '').trim().split(/\s+/).filter(Boolean);
    if (parts.length < 2) {
      this.form.controls['fullName'].setErrors({ fullName: true });
      return;
    }
    this.form.patchValue({ firstName: parts[0], lastName: parts.slice(1).join(' ') }, { emitEvent: false });
  }
}
