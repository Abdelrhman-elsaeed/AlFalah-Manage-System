import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
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
    ButtonModule, DropdownModule, InputTextModule, PasswordModule
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

  readonly isEdit = signal(false);
  readonly saving = signal(false);
  readonly userId = signal<string | null>(null);
  readonly isInstructor = signal(false);
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
    { label: 'مدير المدرسة', value: 'SchoolManager' },
    { label: 'مشرف', value: 'Moderator' },
    { label: 'معلم', value: 'Instructor' }
  ];
  readonly stageOptions: { label: string; value: SchoolStage }[] = [
    { label: 'ابتدائي', value: 'Primary' },
    { label: 'متوسط', value: 'Intermediate' },
    { label: 'ثانوي', value: 'Secondary' }
  ];
  readonly languageOptions = [
    { label: 'العربية', value: 'ar' },
    { label: 'English', value: 'en' }
  ];

  readonly availableSchools = signal<{ id: number; name: string }[]>([]);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    const requestedRole = this.route.snapshot.queryParamMap.get('role') as PhaseTwoRole | null;
    if (id) {
      this.isEdit.set(true);
      this.userId.set(id);
      this.form.controls['password'].clearValidators();
      this.form.controls['password'].updateValueAndValidity();
      this.loadUser(id);
    } else {
      this.form.controls['password'].setValidators([Validators.required, Validators.minLength(8)]);
      this.form.controls['password'].updateValueAndValidity();
      if (requestedRole === 'Instructor') this.form.patchValue({ role: requestedRole });
      this.setInstructorMode(this.form.controls['role'].value === 'Instructor');
    }

    this.form.controls['role'].valueChanges.subscribe(role => this.setInstructorMode(role === 'Instructor'));
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
    const instructor = user.roles.includes('Instructor');
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
      stage: user.stage ?? null
    });
    this.setInstructorMode(instructor);
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
          stage: value.stage as SchoolStage,
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
            this.toast.success('TEACHERS.SAVE_SUCCESS_TITLE', response.message || '');
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
          this.toast.success('TEACHERS.SAVE_SUCCESS_TITLE', response.message || '');
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
    this.form.controls['schoolId'].setValidators(required);
    this.form.controls['firstName'].setValidators(instructor ? [] : [Validators.required, Validators.maxLength(100)]);
    this.form.controls['lastName'].setValidators(instructor ? [] : [Validators.required, Validators.maxLength(100)]);
    Object.values(this.form.controls).forEach(control => control.updateValueAndValidity({ emitEvent: false }));
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
