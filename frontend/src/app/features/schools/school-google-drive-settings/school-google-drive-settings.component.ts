import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { InputSwitchModule } from 'primeng/inputswitch';
import { ActivatedRoute, Router } from '@angular/router';
import { ToastService } from '../../../core/services/toast.service';
import { SchoolGoogleDriveService } from '../../../core/services/school-google-drive.service';
import { GoogleDriveCredentialType } from '../../../core/models/school-google-drive.models';

@Component({
  selector: 'app-school-google-drive-settings',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, ButtonModule, InputTextModule, InputTextareaModule, InputSwitchModule],
  templateUrl: './school-google-drive-settings.component.html',
  styleUrls: ['./school-google-drive-settings.component.css']
})
export class SchoolGoogleDriveSettingsComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(SchoolGoogleDriveService);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly credentialTypes = GoogleDriveCredentialType;
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly connecting = signal(false);
  readonly configured = signal(false);
  /** Drives the "leave blank to keep the saved secret" hints and relaxes their validators. */
  readonly hasStoredCredential = signal(false);
  readonly selectedType = signal<GoogleDriveCredentialType>(GoogleDriveCredentialType.ServiceAccount);
  readonly isServiceAccount = computed(() => this.selectedType() === GoogleDriveCredentialType.ServiceAccount);

  /**
   * The consent round trip needs the client id and secret to already be on the server — it is
   * the server that builds the URL from them. So the connect button stays disabled until this
   * school has been saved at least once with an OAuth credential type.
   */
  readonly canConnect = computed(() => !this.isServiceAccount() && this.configured());

  readonly form = this.fb.group({
    credentialType: [GoogleDriveCredentialType.ServiceAccount as GoogleDriveCredentialType, Validators.required],
    schoolGoogleEmail: ['', [Validators.required, Validators.email, Validators.maxLength(320)]],
    serviceAccountJson: [''],
    impersonatedUserEmail: ['', [Validators.email, Validators.maxLength(320)]],
    oAuthClientId: ['', Validators.maxLength(512)],
    oAuthClientSecret: [''],
    sharedDriveId: ['', Validators.maxLength(256)],
    rootFolderId: ['', [Validators.required, Validators.maxLength(256)]],
    rootFolderDisplayName: ['', [Validators.required, Validators.maxLength(256)]],
    isEnabled: [true]
  });

  ngOnInit(): void {
    this.form.controls.credentialType.valueChanges.subscribe(value =>
      this.selectedType.set(value ?? GoogleDriveCredentialType.ServiceAccount));

    this.reportConsentOutcome();

    this.service.get().subscribe({
      next: response => {
        if (response.isSuccess && response.data) {
          const data = response.data;
          this.configured.set(data.isConfigured);
          this.hasStoredCredential.set(data.hasStoredCredential);
          // The response never contains the secrets, so only the descriptive fields are
          // patched back — leaving the secret inputs empty means "keep what is stored".
          this.form.patchValue({
            credentialType: data.credentialType ?? GoogleDriveCredentialType.ServiceAccount,
            schoolGoogleEmail: data.schoolGoogleEmail ?? '',
            impersonatedUserEmail: data.impersonatedUserEmail ?? '',
            oAuthClientId: data.oAuthClientId ?? '',
            sharedDriveId: data.sharedDriveId ?? '',
            rootFolderId: data.rootFolderId ?? '',
            rootFolderDisplayName: data.rootFolderDisplayName ?? '',
            isEnabled: data.isEnabled
          });
        }
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    const isServiceAccount = value.credentialType === GoogleDriveCredentialType.ServiceAccount;
    this.saving.set(true);
    this.service.configure({
      credentialType: value.credentialType!,
      schoolGoogleEmail: value.schoolGoogleEmail!.trim(),
      // Send null rather than an empty string for an untouched secret: the server treats
      // null as "keep the stored value" and would otherwise be asked to store "".
      serviceAccountJson: isServiceAccount ? this.orNull(value.serviceAccountJson) : null,
      impersonatedUserEmail: isServiceAccount ? this.orNull(value.impersonatedUserEmail) : null,
      oAuthClientId: isServiceAccount ? null : this.orNull(value.oAuthClientId),
      oAuthClientSecret: isServiceAccount ? null : this.orNull(value.oAuthClientSecret),
      sharedDriveId: this.orNull(value.sharedDriveId),
      rootFolderId: value.rootFolderId!.trim(),
      rootFolderDisplayName: value.rootFolderDisplayName!.trim(),
      isEnabled: !!value.isEnabled
    }).subscribe({
      next: response => {
        this.saving.set(false);
        if (response.isSuccess && response.data) {
          this.configured.set(true);
          this.hasStoredCredential.set(response.data.hasStoredCredential);
          // Clear the secret inputs so a saved credential is never left sitting in the DOM.
          this.form.patchValue({ serviceAccountJson: '', oAuthClientSecret: '' });
          this.toast.success(response.message || 'تم تفعيل ملفات الإنجاز للمدرسة.');
        }
      },
      error: () => this.saving.set(false)
    });
  }

  /**
   * Hands the browser over to Google. This is a full page navigation on purpose: the consent
   * screen sets its own cookies and refuses to be framed, and the callback returns here.
   *
   * `connecting` is never reset on success — the page is being replaced, and clearing it would
   * only flicker the button back to its idle state mid-navigation.
   */
  connect(): void {
    this.connecting.set(true);
    this.service.authUrl().subscribe({
      next: response => {
        if (response.isSuccess && response.data?.authorizationUrl) {
          window.location.href = response.data.authorizationUrl;
          return;
        }
        this.connecting.set(false);
      },
      error: () => this.connecting.set(false)
    });
  }

  /**
   * Surfaces the outcome the callback appended as ?googleDrive=connected|failed, then strips it
   * from the URL so a refresh does not replay a stale toast.
   */
  private reportConsentOutcome(): void {
    const outcome = this.route.snapshot.queryParamMap.get('googleDrive');
    if (!outcome) return;

    if (outcome === 'connected') {
      this.toast.success('تم ربط حساب Google Drive الخاص بالمدرسة.');
    } else {
      this.toast.error('لم يكتمل ربط حساب Google Drive. تحقق من إعدادات OAuth ثم حاول مرة أخرى.');
    }

    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { googleDrive: null },
      queryParamsHandling: 'merge',
      replaceUrl: true
    });
  }

  private orNull(value: string | null | undefined): string | null {
    const trimmed = value?.trim();
    return trimmed ? trimmed : null;
  }
}
