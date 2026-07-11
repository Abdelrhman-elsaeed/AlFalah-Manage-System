import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { DropdownModule } from 'primeng/dropdown';
import { ToastService } from '../../../core/services/toast.service';
import { UsersService } from '../../../core/services/users.service';
import { SchoolsService } from '../../../core/services/schools.service';
import { UserDetail, PhaseTwoRole } from '../../../core/models/phase2.models';

@Component({
  selector: 'app-user-form',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, RouterLink, TranslateModule,
    ButtonModule, InputTextModule, PasswordModule, DropdownModule
  ],
  templateUrl: './user-form.component.html',
  styleUrls: ['./user-form.component.css']
})
export class UserFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly usersService = inject(UsersService);
  private readonly schoolsService = inject(SchoolsService);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  isEdit = signal(false);
  saving = signal(false);
  userId = signal<string | null>(null);

  form: FormGroup = this.fb.group({
    username: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(256)]],
    password: ['', [Validators.minLength(8)]],
    firstName: ['', [Validators.required, Validators.maxLength(100)]],
    lastName: ['', [Validators.required, Validators.maxLength(100)]],
    email: ['', Validators.email],
    phoneNumber: [''],
    preferredLanguage: ['ar'],
    role: ['Moderator' as PhaseTwoRole, Validators.required],
    schoolId: [null]
  });

  readonly roleOptions = [
    { label: 'مدير المدرسة', value: 'SchoolManager' },
    { label: 'مشرف', value: 'Moderator' },
    { label: 'معلم', value: 'Instructor' }
  ];

  readonly languageOptions = [
    { label: 'العربية', value: 'ar' },
    { label: 'English', value: 'en' }
  ];

  availableSchools = signal<{ id: number; name: string }[]>([]);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.userId.set(id);
      // Password is optional on edit.
      this.form.get('password')?.clearValidators();
      this.form.get('password')?.updateValueAndValidity();
      this.loadUser(id);
    } else {
      // Password required on create.
      this.form.get('password')?.setValidators([Validators.required, Validators.minLength(8)]);
      this.form.get('password')?.updateValueAndValidity();
    }
    this.loadSchools();
  }

  loadUser(id: string): void {
    this.usersService.getById(id).subscribe({
      next: (response) => {
        if (response.isSuccess && response.data) this.applyForm(response.data);
      }
    });
  }

  applyForm(u: UserDetail): void {
    this.form.patchValue({
      username: u.username,
      firstName: u.firstName,
      lastName: u.lastName,
      email: u.email ?? '',
      phoneNumber: u.phoneNumber ?? '',
      preferredLanguage: u.preferredLanguage ?? 'ar',
      role: (u.roles[0] as PhaseTwoRole) ?? 'Moderator',
      schoolId: u.schools[0]?.schoolId ?? null
    });
  }

  loadSchools(): void {
    this.schoolsService.list({ isActive: true, page: 1, pageSize: 200 }).subscribe({
      next: (response) => {
        if (response.isSuccess && response.data) {
          this.availableSchools.set(
            response.data.items.map(s => ({ id: s.id, name: `${s.name} — ${s.city}` }))
          );
        }
      }
    });
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.value;
    this.saving.set(true);

    if (this.isEdit()) {
      const body = {
        firstName: v.firstName.trim(),
        lastName: v.lastName.trim(),
        email: v.email || undefined,
        phoneNumber: v.phoneNumber || undefined,
        preferredLanguage: v.preferredLanguage ?? 'ar'
      };
      this.usersService.update(this.userId()!, body).subscribe({
        next: (response) => {
          if (response.isSuccess) {
            this.toast.success('تم الحفظ', response.message || 'تم تحديث المستخدم بنجاح.');
            this.router.navigate(['/users']);
          }
          this.saving.set(false);
        },
        error: () => this.saving.set(false)
      });
    } else {
      const body = {
        username: v.username.trim(),
        password: v.password,
        firstName: v.firstName.trim(),
        lastName: v.lastName.trim(),
        email: v.email || undefined,
        phoneNumber: v.phoneNumber || undefined,
        preferredLanguage: v.preferredLanguage ?? 'ar',
        role: v.role as PhaseTwoRole,
        schoolId: v.schoolId ?? undefined
      };
      this.usersService.create(body).subscribe({
        next: (response) => {
          if (response.isSuccess) {
            this.toast.success('تم الإنشاء', response.message || 'تم إنشاء المستخدم بنجاح.');
            this.router.navigate(['/users']);
          }
          this.saving.set(false);
        },
        error: () => this.saving.set(false)
      });
    }
  }
}