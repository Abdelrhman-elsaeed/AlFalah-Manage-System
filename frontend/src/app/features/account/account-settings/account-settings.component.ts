import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { ChipsModule } from 'primeng/chips';
import { InputTextModule } from 'primeng/inputtext';
import { AccountTeachingService } from '../../../core/services/account-teaching.service';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-account-settings',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslateModule, ButtonModule, ChipsModule, InputTextModule],
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
  readonly teachingForm = this.fb.group({
    subject: ['', [Validators.required, Validators.maxLength(200)]],
    classes: [[] as string[]]
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
          this.teachingForm.patchValue({
            subject: response.data.subject ?? '',
            classes: response.data.classes ?? []
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
    if (this.teachingForm.invalid) {
      this.teachingForm.markAllAsTouched();
      return;
    }

    const value = this.teachingForm.getRawValue();
    this.teachingSaving.set(true);
    this.teachingService.updateMyTeaching({
      subject: value.subject!.trim(),
      classes: (value.classes ?? []).map(c => c.trim()).filter(Boolean)
    }).subscribe({
      next: response => {
        this.teachingSaving.set(false);
        if (response.isSuccess && response.data) {
          this.teachingForm.patchValue({ subject: response.data.subject ?? '', classes: response.data.classes ?? [] });
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
