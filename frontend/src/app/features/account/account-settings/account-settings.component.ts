import { AfterViewInit, Component, ElementRef, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { HttpClient } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { ChipsModule } from 'primeng/chips';
import { InputTextModule } from 'primeng/inputtext';
import { environment } from '../../../../environments/environment';
import SignaturePad from 'signature_pad';
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
export class AccountSettingsComponent implements OnInit, AfterViewInit {
  @ViewChild('signaturePad', { static: true }) signaturePadElement!: ElementRef<HTMLCanvasElement>;

  private signaturePad!: SignaturePad;
  isSaving = false;
  existingSignatureDataUrl: string | null = null;

  private readonly fb = inject(FormBuilder);
  private readonly http = inject(HttpClient);
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
    newPassword: ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', [Validators.required, Validators.minLength(8)]]
  });
  readonly passwordSaving = signal(false);

  ngOnInit(): void {
    if (this.isInstructor()) this.loadTeaching();
  }

  ngAfterViewInit(): void {
    this.signaturePad = new SignaturePad(this.signaturePadElement.nativeElement, {
      backgroundColor: 'rgba(255, 255, 255, 0)',
      penColor: '#000000'
    });
    this.loadSignature();
  }

  loadSignature(): void {
    this.http.get<any>(`${environment.apiUrl}/api/v1/account/signature`).subscribe({
      next: res => {
        if (res.isSuccess && res.data?.signatureDrawnData) {
          this.existingSignatureDataUrl = res.data.signatureDrawnData;
          this.signaturePad.fromDataURL(res.data.signatureDrawnData);
        }
      },
      error: err => console.error('Failed to load signature', err)
    });
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

  clearSignature(): void {
    this.signaturePad.clear();
  }

  saveSignature(): void {
    const dataUrl = this.signaturePad.isEmpty() ? null : this.signaturePad.toDataURL('image/png');
    this.isSaving = true;

    this.http.put<any>(`${environment.apiUrl}/api/v1/account/signature`, { signatureDrawnData: dataUrl }).subscribe({
      next: res => {
        this.isSaving = false;
        if (res.isSuccess) {
          this.existingSignatureDataUrl = dataUrl;
          this.toast.success(res.message || this.translate.instant('ACCOUNT.SIGNATURE.SAVE_SUCCESS'));
        } else {
          this.toast.error(res.errors?.[0] || this.translate.instant('ACCOUNT.SIGNATURE.SAVE_FAILED'));
        }
      },
      error: err => {
        this.isSaving = false;
        this.toast.error(this.translate.instant('ACCOUNT.SIGNATURE.SAVE_ERROR'));
        console.error(err);
      }
    });
  }
}
