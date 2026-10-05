import { Component, DestroyRef, OnInit, computed, effect, inject, signal, untracked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { InputSwitchModule } from 'primeng/inputswitch';
import { DialogModule } from 'primeng/dialog';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ToastService } from '../../../core/services/toast.service';
import { SchoolGoogleDriveService } from '../../../core/services/school-google-drive.service';
import { AuthService } from '../../../core/services/auth.service';
import { StorageApiService } from '../../storage/storage-api.service';
import { ConfigureSchoolGoogleDriveRequest, GoogleDriveCredentialType, SchoolDriveFolderPage, SchoolGoogleDriveSettings } from '../../../core/models/school-google-drive.models';

@Component({
  selector: 'app-school-google-drive-settings', standalone: true,
  imports: [CommonModule, ReactiveFormsModule, ButtonModule, InputTextModule, InputTextareaModule, InputSwitchModule, DialogModule, RouterLink],
  templateUrl: './school-google-drive-settings.component.html',
  styleUrls: ['./school-google-drive-settings.component.css']
})
export class SchoolGoogleDriveSettingsComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(SchoolGoogleDriveService);
  private readonly auth = inject(AuthService);
  private readonly storage = inject(StorageApiService);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private savedSettings: SchoolGoogleDriveSettings | null = null;
  private savedConnection = '';
  private browseRevision = 0;
  private scopeRevision = 0;
  readonly credentialTypes = GoogleDriveCredentialType;
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly connecting = signal(false);
  readonly configured = signal(false);
  readonly hasStoredCredential = signal(false);
  readonly hasStoredClientSecret = signal(false);
  readonly connectionDirty = signal(false);
  readonly enabled = signal(false);
  readonly libraryState = signal('Checking');
  readonly error = signal('');
  readonly selectedType = signal<GoogleDriveCredentialType>(GoogleDriveCredentialType.OAuthRefreshToken);
  readonly isServiceAccount = computed(() => this.selectedType() === GoogleDriveCredentialType.ServiceAccount);
  readonly canConnect = computed(() => !this.isServiceAccount() && this.configured() && !this.connectionDirty());
  readonly canBrowse = computed(() => this.hasStoredCredential() && !this.connectionDirty());
  readonly pickerVisible = signal(false);
  readonly browsing = signal(false);
  readonly browseError = signal('');
  readonly folderPage = signal<SchoolDriveFolderPage | null>(null);
  readonly importedFile = signal('');
  readonly form = this.fb.group({
    credentialType: [GoogleDriveCredentialType.OAuthRefreshToken as GoogleDriveCredentialType, Validators.required],
    schoolGoogleEmail: ['', [Validators.required, Validators.email, Validators.maxLength(320)]],
    serviceAccountJson: [''], impersonatedUserEmail: ['', [Validators.email, Validators.maxLength(320)]],
    oAuthClientId: ['', Validators.maxLength(512)], oAuthClientSecret: [''],
    sharedDriveId: ['', Validators.maxLength(256)], rootFolderId: ['', Validators.maxLength(256)],
    rootFolderDisplayName: ['', Validators.maxLength(256)], isEnabled: [false]
  });
  constructor() {
    let schoolId = this.auth.activeSchoolId();
    effect(() => {
      const next = this.auth.activeSchoolId();
      if (next !== schoolId) { schoolId = next; untracked(() => { this.clearState(); this.loadSettings(); }); }
    }, { allowSignalWrites: true });
  }
  ngOnInit(): void {
    this.form.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.selectedType.set(this.form.controls.credentialType.value ?? GoogleDriveCredentialType.OAuthRefreshToken);
      this.connectionDirty.set(this.connectionSignature() !== this.savedConnection);
      if (this.connectionDirty()) this.closePicker();
    });
    this.reportConsentOutcome();
    this.loadSettings();
  }
  private loadSettings(): void {
    const revision = ++this.scopeRevision; this.loading.set(true);
    this.service.get().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: r => { if (revision !== this.scopeRevision) return; this.loading.set(false); if (r.isSuccess && r.data) this.applySettings(r.data); else this.error.set(r.message || 'تعذر تحميل إعدادات الاتصال.'); },
      error: e => { if (revision !== this.scopeRevision) return; this.loading.set(false); this.error.set(this.message(e, 'تعذر تحميل إعدادات الاتصال.')); }
    });
  }
  async importOAuthClient(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement; const file = input.files?.[0]; input.value = '';
    if (!file) return;
    const revision = this.scopeRevision;
    try {
      if (file.size > 64 * 1024 || !file.name.toLowerCase().endsWith('.json')) throw new Error();
      const client = JSON.parse(await file.text())?.web;
      if (revision !== this.scopeRevision) return;
      if (!client || typeof client.client_id !== 'string' || !client.client_id.endsWith('.apps.googleusercontent.com') || client.client_id.length > 512 ||
          typeof client.client_secret !== 'string' || !client.client_secret.trim() || client.client_secret.length > 2048) throw new Error();
      this.form.patchValue({ credentialType: GoogleDriveCredentialType.OAuthRefreshToken, oAuthClientId: client.client_id, oAuthClientSecret: client.client_secret });
      this.importedFile.set(file.name); this.error.set(''); this.toast.success('تمت قراءة ملف OAuth. احفظ الاتصال ثم اربط حساب Google.');
    } catch { if (revision === this.scopeRevision) this.error.set('اختر ملف OAuth JSON من نوع Web application صالحًا، بحجم لا يتجاوز 64 كيلوبايت.'); }
  }
  saveConnection(): void {
    if (!this.validateConnection()) return;
    const body = this.request(false);
    body.rootFolderId = this.savedSettings?.rootFolderId ?? ''; body.rootFolderDisplayName = this.savedSettings?.rootFolderDisplayName ?? '';
    this.persist(body, 'تم حفظ الاتصال. أكمل ربط الحساب ثم اختر مجلد المدرسة.');
  }
  save(): void {
    if (this.connectionDirty()) { this.error.set('احفظ بيانات الاتصال أولًا، ثم أكمل الربط واختيار المجلد.'); return; }
    if (!this.hasStoredCredential()) { this.error.set('اربط الحساب قبل تفعيل الملفات.'); return; }
    if (!this.form.controls.rootFolderId.value?.trim() || !this.form.controls.rootFolderDisplayName.value?.trim()) {
      this.error.set('اختر مجلد المدرسة من ملفات الحساب أولًا.'); return;
    }
    this.persist(this.request(!!this.form.controls.isEnabled.value), 'تم حفظ مجلد المدرسة وإعدادات الملفات.');
  }
  connect(): void {
    if (!this.canConnect()) return;
    this.connecting.set(true); this.error.set(''); const revision = this.scopeRevision;
    this.service.authUrl().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: r => { if (revision !== this.scopeRevision) return; if (r.isSuccess && r.data?.authorizationUrl) { window.location.assign(r.data.authorizationUrl); return; } this.connecting.set(false); this.error.set(r.message || 'تعذر بدء ربط الحساب.'); },
      error: e => { if (revision !== this.scopeRevision) return; this.connecting.set(false); this.error.set(this.message(e, 'تعذر بدء ربط الحساب.')); }
    });
  }
  openPicker(): void { if (this.canBrowse()) { this.pickerVisible.set(true); this.browse(); } }
  closePicker(): void { ++this.browseRevision; this.pickerVisible.set(false); this.folderPage.set(null); this.browsing.set(false); this.browseError.set(''); }
  browse(parent?: string, nextPage = false, search?: string): void {
    const previous = this.folderPage(); const revision = ++this.browseRevision;
    this.browsing.set(true); this.browseError.set(''); if (!nextPage) this.folderPage.set(null);
    this.service.folders(parent, nextPage ? previous?.nextPageToken ?? undefined : undefined, search).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: r => {
        if (revision !== this.browseRevision || !this.pickerVisible()) return;
        this.browsing.set(false);
        if (!r.isSuccess || !r.data) { this.folderPage.set(null); this.browseError.set(r.message || 'تعذر استعراض الملفات.'); return; }
        this.folderPage.set(nextPage && previous ? { ...r.data, items: [...previous.items, ...r.data.items] } : r.data);
      },
      error: e => { if (revision !== this.browseRevision) return; this.browsing.set(false); this.folderPage.set(null); this.browseError.set(this.message(e, 'تعذر استعراض الحساب. أعد ربط Google إذا انتهت صلاحية الاتصال.')); }
    });
  }
  selectFolder(itemId: string, name: string): void {
    this.form.patchValue({ rootFolderId: itemId, rootFolderDisplayName: name, isEnabled: true }); this.error.set(''); this.closePicker();
  }
  private applySettings(data: SchoolGoogleDriveSettings): void {
    this.savedSettings = data; this.configured.set(data.isConfigured); this.hasStoredCredential.set(data.hasStoredCredential);
    this.hasStoredClientSecret.set(!!data.hasStoredOAuthClientSecret); this.enabled.set(data.isEnabled);
    this.form.patchValue({ credentialType: data.credentialType ?? GoogleDriveCredentialType.OAuthRefreshToken, schoolGoogleEmail: data.schoolGoogleEmail ?? '',
      impersonatedUserEmail: data.impersonatedUserEmail ?? '', oAuthClientId: data.oAuthClientId ?? '', sharedDriveId: data.sharedDriveId ?? '',
      rootFolderId: data.rootFolderId ?? '', rootFolderDisplayName: data.rootFolderDisplayName ?? '', isEnabled: data.isEnabled,
      serviceAccountJson: '', oAuthClientSecret: '' }, { emitEvent: false });
    this.selectedType.set(this.form.controls.credentialType.value!); this.savedConnection = this.connectionSignature(); this.connectionDirty.set(false); this.importedFile.set('');
    this.libraryState.set('Checking');
    if (data.hasStoredCredential && data.isEnabled) {
      const revision = this.scopeRevision;
      this.storage.contextInfo(false).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: context => { if (revision === this.scopeRevision) this.libraryState.set(context.connectionState || 'Unavailable'); },
        error: () => { if (revision === this.scopeRevision) this.libraryState.set('Unavailable'); }
      });
    }
  }
  private connectionSignature(): string {
    const v = this.form.getRawValue();
    // Compare presence only; secrets never enter browser storage or logs.
    return JSON.stringify([v.credentialType, v.schoolGoogleEmail?.trim(), v.impersonatedUserEmail?.trim(), v.oAuthClientId?.trim(), v.sharedDriveId?.trim(), !!v.oAuthClientSecret, !!v.serviceAccountJson]);
  }
  private validateConnection(): boolean {
    this.form.markAllAsTouched(); this.error.set(''); const v = this.form.getRawValue();
    if (this.form.invalid) { this.error.set('راجع بريد الحساب وبيانات الاتصال.'); return false; }
    if (this.isServiceAccount() && !v.serviceAccountJson?.trim() && !(this.savedSettings?.credentialType === GoogleDriveCredentialType.ServiceAccount && this.hasStoredCredential())) {
      this.error.set('أدخل مفتاح حساب الخدمة.'); return false;
    }
    if (!this.isServiceAccount() && (!v.oAuthClientId?.trim() || !v.oAuthClientSecret?.trim() && !this.hasStoredClientSecret())) {
      this.error.set('استورد ملف OAuth JSON أو أدخل Client ID وClient Secret.'); return false;
    }
    return true;
  }
  private request(enabled: boolean): ConfigureSchoolGoogleDriveRequest {
    const v = this.form.getRawValue(); const serviceAccount = this.isServiceAccount();
    return { credentialType: v.credentialType!, schoolGoogleEmail: v.schoolGoogleEmail!.trim(), serviceAccountJson: serviceAccount ? this.orNull(v.serviceAccountJson) : null,
      impersonatedUserEmail: serviceAccount ? this.orNull(v.impersonatedUserEmail) : null, oAuthClientId: serviceAccount ? null : this.orNull(v.oAuthClientId),
      oAuthClientSecret: serviceAccount ? null : this.orNull(v.oAuthClientSecret), sharedDriveId: this.orNull(v.sharedDriveId),
      rootFolderId: v.rootFolderId?.trim() ?? '', rootFolderDisplayName: v.rootFolderDisplayName?.trim() ?? '', isEnabled: enabled };
  }
  private persist(body: ConfigureSchoolGoogleDriveRequest, message: string): void {
    if (this.saving()) return;
    this.saving.set(true); this.error.set(''); const revision = this.scopeRevision;
    this.service.configure(body).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: r => { if (revision !== this.scopeRevision) return; this.saving.set(false); if (r.isSuccess && r.data) { this.applySettings(r.data); this.toast.success(message); } else this.error.set(r.message || 'تعذر حفظ الإعدادات.'); },
      error: e => { if (revision !== this.scopeRevision) return; this.saving.set(false); this.error.set(this.message(e, 'تعذر حفظ الإعدادات.')); }
    });
  }
  private reportConsentOutcome(): void {
    const outcome = this.route.snapshot.queryParamMap.get('googleDrive'); if (!outcome) return;
    if (outcome === 'connected') this.toast.success('تم ربط Google. اختر الآن مجلد المدرسة من ملفات الحساب.');
    else this.error.set('لم يكتمل ربط Google. تحقق من إعدادات OAuth ثم حاول مرة أخرى.');
    this.router.navigate([], { relativeTo: this.route, queryParams: { googleDrive: null }, queryParamsHandling: 'merge', replaceUrl: true });
  }
  private message(error: { status?: number; error?: { message?: string } }, fallback: string): string {
    if (error?.status === 403) this.clearState(false);
    return error?.error?.message || fallback;
  }
  private clearState(closePicker = true): void {
    if (closePicker) this.closePicker(); this.savedSettings = null; this.savedConnection = '';
    this.configured.set(false); this.hasStoredCredential.set(false); this.hasStoredClientSecret.set(false); this.enabled.set(false);
    this.libraryState.set('Checking');
    this.saving.set(false); this.connecting.set(false); this.error.set(''); this.importedFile.set('');
    this.selectedType.set(GoogleDriveCredentialType.OAuthRefreshToken);
    this.form.reset({ credentialType: GoogleDriveCredentialType.OAuthRefreshToken, isEnabled: false }, { emitEvent: false });
  }
  private orNull(value: string | null | undefined): string | null { return value?.trim() || null; }
}
