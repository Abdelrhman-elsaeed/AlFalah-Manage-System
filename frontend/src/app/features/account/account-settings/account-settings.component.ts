import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { ClassroomSelectorComponent } from '../../../shared/components/classroom-selector/classroom-selector.component';
import { TeacherTeaching } from '../../../core/models/teacher.models';
import { InputTextModule } from 'primeng/inputtext';
import { AccountTeachingService } from '../../../core/services/account-teaching.service';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-account-settings',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslateModule, ButtonModule, ClassroomSelectorComponent, InputTextModule],
  templateUrl: './account-settings.component.html',
  styleUrls: ['./account-settings.component.css']
})
export class AccountSettingsComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly teachingService = inject(AccountTeachingService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  readonly isInstructor = computed(() => this.auth.hasRole('Instructor'));
  readonly teachingLoading = signal(false);
  readonly teachingSaving = signal(false);
  readonly teachingLoaded = signal(false);
  readonly teaching = signal<TeacherTeaching | null>(null);
  readonly catalogReady = signal(false);
  readonly teachingForm = this.fb.group({
    subject: ['', [Validators.required, Validators.maxLength(200)]],
    classroomIds: [[] as number[]]
  });
  readonly passwordForm = this.fb.group({
    currentPassword: ['', [Validators.required]],
    newPassword: ['', [Validators.required, Validators.minLength(6)]],
    confirmPassword: ['', [Validators.required, Validators.minLength(6)]]
  });
  readonly passwordSaving = signal(false);

  ngOnInit(): void {
    if (this.isInstructor()) this.loadTeaching();
  }

  loadTeaching(): void {
    this.teachingLoading.set(true);
    this.teachingService.getMyTeaching().subscribe({
      next: response => {
        this.teachingLoading.set(false);
        if (response.isSuccess && response.data) {
          this.teaching.set(response.data);
          this.teachingForm.patchValue({
            subject: response.data.subject ?? '',
            classroomIds: response.data.classroomIds ?? []
          });
        }
        this.teachingLoaded.set(true);
      },
      error: () => {
        this.teachingLoading.set(false);
        this.teachingLoaded.set(true);
      }
    });
  }

  saveTeaching(): void {
    if (!this.catalogReady() || !this.teaching() || this.teachingSaving()) return;
    if (this.teachingForm.invalid) {
      this.teachingForm.markAllAsTouched();
      return;
    }

    const value = this.teachingForm.getRawValue();
    this.teachingSaving.set(true);
    this.teachingService.updateMyTeaching({
      subject: value.subject!.trim(),
      classroomIds: value.classroomIds ?? [],
      academicYearId: this.teaching()!.academicYearId,
      teachingRevision: this.teaching()!.teachingRevision
    }).subscribe({
      next: response => {
        this.teachingSaving.set(false);
        if (response.isSuccess && response.data) {
          this.teaching.set(response.data);
          this.teachingForm.patchValue({ subject: response.data.subject ?? '', classroomIds: response.data.classroomIds ?? [] });
          this.toast.success(this.translate.instant('TEACHING.SAVE_SUCCESS'), response.message || '');
        }
      },
      error: () => this.teachingSaving.set(false)
    });
  }

  changePassword(): void {
    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }
    const value = this.passwordForm.getRawValue();
    if (value.newPassword !== value.confirmPassword) {
      this.passwordForm.controls.confirmPassword.setErrors({ mismatch: true });
      return;
    }
    this.passwordSaving.set(true);
    this.auth.changePassword(value.currentPassword!, value.newPassword!).subscribe({
      next: response => {
        this.passwordSaving.set(false);
        if (response.isSuccess) {
          this.passwordForm.reset();
          this.toast.success(this.translate.instant('ACCOUNT.PASSWORD.SUCCESS'));
        }
      },
      error: () => this.passwordSaving.set(false)
    });
  }

}
