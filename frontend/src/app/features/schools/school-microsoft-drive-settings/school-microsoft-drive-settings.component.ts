import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputSwitchModule } from 'primeng/inputswitch';
import { ToastService } from '../../../core/services/toast.service';
import { SchoolMicrosoftDriveService } from '../../../core/services/school-microsoft-drive.service';

@Component({ selector: 'app-school-microsoft-drive-settings', standalone: true, imports: [CommonModule, ReactiveFormsModule, ButtonModule, InputTextModule, InputSwitchModule], templateUrl: './school-microsoft-drive-settings.component.html', styleUrls: ['./school-microsoft-drive-settings.component.css'] })
export class SchoolMicrosoftDriveSettingsComponent implements OnInit {
  private readonly fb = inject(FormBuilder); private readonly service = inject(SchoolMicrosoftDriveService); private readonly toast = inject(ToastService);
  readonly loading = signal(true); readonly saving = signal(false); readonly configured = signal(false);
  readonly form = this.fb.group({
    tenantId: ['', [Validators.required, Validators.pattern(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/)]],
    schoolMicrosoftEmail: ['', [Validators.required, Validators.email, Validators.maxLength(320)]],
    driveId: ['', [Validators.required, Validators.maxLength(256)]], rootItemId: ['', [Validators.required, Validators.maxLength(256)]],
    rootFolderDisplayName: ['', [Validators.required, Validators.maxLength(256)]], isEnabled: [true]
  });
  ngOnInit(): void { this.service.get().subscribe({ next: r => { if (r.isSuccess && r.data) { this.configured.set(r.data.isConfigured); this.form.patchValue(r.data); } this.loading.set(false); }, error: () => this.loading.set(false) }); }
  save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue(); this.saving.set(true);
    this.service.configure({ tenantId: v.tenantId!.trim(), schoolMicrosoftEmail: v.schoolMicrosoftEmail!.trim(), driveId: v.driveId!.trim(), rootItemId: v.rootItemId!.trim(), rootFolderDisplayName: v.rootFolderDisplayName!.trim(), isEnabled: !!v.isEnabled }).subscribe({
      next: r => { this.saving.set(false); if (r.isSuccess && r.data) { this.configured.set(true); this.toast.success(r.message || 'تم تفعيل ملفات الإنجاز للمدرسة.'); } }, error: () => this.saving.set(false)
    });
  }
}
