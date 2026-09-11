import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { ProgressBarModule } from 'primeng/progressbar';
import { SkeletonModule } from 'primeng/skeleton';
import { TagModule } from 'primeng/tag';
import { finalize } from 'rxjs';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import {
  TimetableSettingsOverview,
  TimetableSetupProfile,
  TimetableSetupStep,
  TimetableStepStatus
} from '../../../core/models/timetable-settings.models';
import { TimetableSettingsService } from '../../../core/services/timetable-settings.service';
import { ToastService } from '../../../core/services/toast.service';
import { HasUnsavedTimetableSettings } from '../../../core/guards/unsaved-timetable-settings.guard';

interface SelectOption<T> {
  readonly label: string;
  readonly value: T;
}

@Component({
  selector: 'app-timetable-settings',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    ButtonModule,
    CardModule,
    DropdownModule,
    InputTextModule,
    ProgressBarModule,
    SkeletonModule,
    TagModule
  ],
  templateUrl: './timetable-settings.component.html',
  styleUrl: './timetable-settings.component.css'
})
export class TimetableSettingsComponent implements OnInit, HasUnsavedTimetableSettings {
  private readonly api = inject(TimetableSettingsService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  readonly overview = signal<TimetableSettingsOverview | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly creating = signal(false);
  readonly guideDismissed = signal(false);
  readonly conflictMessage = signal('');
  readonly conflictDraft = signal<string | null>(null);

  readonly semesterOptions: readonly SelectOption<number>[] = [
    { label: 'الفصل الدراسي الأول', value: 1 },
    { label: 'الفصل الدراسي الثاني', value: 2 }
  ];

  readonly contextForm = new FormGroup({
    academicYearId: new FormControl<number | null>(null, Validators.required),
    semester: new FormControl<number>(1, { nonNullable: true, validators: Validators.required }),
    profileId: new FormControl<number | null>(null)
  });

  readonly profileForm = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(120)]
    })
  });

  readonly canManage = computed(() => this.overview()?.canManage ?? false);
  readonly selectedProfile = computed(() => this.overview()?.selectedProfile ?? null);
  readonly completedSteps = computed(() => this.overview()?.steps.filter(step => step.status === 'complete').length ?? 0);
  readonly profileOptions = computed<readonly SelectOption<number>[]>(() =>
    (this.overview()?.profiles ?? []).map(profile => ({ label: profile.name, value: profile.id })));
  readonly yearOptions = computed<readonly SelectOption<number>[]>(() =>
    (this.overview()?.academicYears ?? []).map(year => ({
      label: `${year.nameAr}${year.isActive ? ' — النشط' : ''}`,
      value: year.id
    })));

  ngOnInit(): void {
    this.loadOverview();
  }

  hasUnsavedChanges(): boolean {
    return this.profileForm.dirty && this.canManage();
  }

  @HostListener('window:beforeunload', ['$event'])
  warnBeforeUnload(event: BeforeUnloadEvent): void {
    if (!this.hasUnsavedChanges()) return;
    event.preventDefault();
    event.returnValue = '';
  }

  changeAcademicContext(): void {
    if (!this.confirmDiscard()) {
      this.restoreContextControls();
      return;
    }
    this.contextForm.controls.profileId.setValue(null, { emitEvent: false });
    this.loadOverview(
      this.contextForm.controls.academicYearId.value ?? undefined,
      this.contextForm.controls.semester.value);
  }

  changeProfile(): void {
    if (!this.confirmDiscard()) {
      this.restoreContextControls();
      return;
    }
    this.loadOverview(
      this.contextForm.controls.academicYearId.value ?? undefined,
      this.contextForm.controls.semester.value,
      this.contextForm.controls.profileId.value ?? undefined);
  }

  startCreate(): void {
    if (!this.confirmDiscard()) return;
    this.creating.set(true);
    this.conflictMessage.set('');
    this.conflictDraft.set(null);
    this.contextForm.controls.profileId.setValue(null, { emitEvent: false });
    this.profileForm.reset({ name: '' });
    this.profileForm.markAsPristine();
  }

  cancelCreate(): void {
    if (!this.confirmDiscard()) return;
    this.applyProfile(this.selectedProfile());
  }

  save(): void {
    this.profileForm.markAllAsTouched();
    if (this.profileForm.invalid || this.saving() || !this.canManage()) return;

    const name = this.profileForm.controls.name.value.trim();
    const profile = this.selectedProfile();
    const academicYearId = this.contextForm.controls.academicYearId.value;
    const semester = this.contextForm.controls.semester.value;
    if (!academicYearId) return;

    this.saving.set(true);
    const request$ = this.creating() || !profile
      ? this.api.createProfile({ academicYearId, semester, name })
      : this.api.updateProfile(profile.id, { name, revision: profile.revision });

    request$.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) {
          this.toast.error('تعذر حفظ إعدادات الجدول', response.errors[0] ?? response.message ?? '');
          return;
        }
        this.toast.success('تم حفظ إعدادات الجدول', response.message || 'تم حفظ أحدث نسخة بنجاح.');
        this.loadOverview(academicYearId, semester, response.data.id);
      },
      error: (error: HttpErrorResponse) => {
        if (error.status === 409) {
          this.conflictDraft.set(name);
          this.conflictMessage.set('عُدّلت هذه الإعدادات من مستخدم آخر. حمّلنا أحدث نسخة واحتفظنا بمسودتك للمراجعة.');
          this.loadOverview(academicYearId, semester, profile?.id, true);
          return;
        }
        this.toast.error('تعذر حفظ إعدادات الجدول', extractHttpErrorMessage(error) ?? 'تحقق من البيانات وحاول مرة أخرى.');
      }
    });
  }

  restoreDraft(): void {
    const draft = this.conflictDraft();
    if (draft === null) return;
    this.profileForm.controls.name.setValue(draft);
    this.profileForm.markAsDirty();
    this.conflictDraft.set(null);
  }

  reloadLatest(): void {
    const overview = this.overview();
    if (!overview) return;
    this.conflictDraft.set(null);
    this.conflictMessage.set('');
    this.loadOverview(overview.selectedAcademicYearId, overview.selectedSemester, overview.selectedProfile?.id);
  }

  continueSetup(): void {
    const step = this.overview()?.steps.find(item => item.status !== 'complete');
    if (step) void this.router.navigateByUrl(step.route);
  }

  dismissGuide(): void {
    this.guideDismissed.set(true);
  }

  statusLabel(status: TimetableStepStatus): string {
    return {
      'not-started': 'لم يبدأ',
      incomplete: 'غير مكتمل',
      complete: 'مكتمل',
      blocked: 'متوقف'
    }[status];
  }

  statusIcon(status: TimetableStepStatus): string {
    return {
      'not-started': 'pi pi-circle',
      incomplete: 'pi pi-exclamation-circle',
      complete: 'pi pi-check-circle',
      blocked: 'pi pi-lock'
    }[status];
  }

  stepNumber(step: TimetableSetupStep): number {
    return (this.overview()?.steps.indexOf(step) ?? 0) + 1;
  }

  formatUpdatedAt(value: string): string {
    return new Intl.DateTimeFormat('ar-EG', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
  }

  private loadOverview(
    academicYearId?: number,
    semester?: number,
    profileId?: number,
    preserveConflict = false): void {
    this.loading.set(true);
    this.api.getOverview(academicYearId, semester, profileId)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: response => {
          if (!response.isSuccess || !response.data) {
            this.toast.error('تعذر تحميل إعدادات الجدول', response.errors[0] ?? response.message ?? '');
            return;
          }
          this.overview.set(response.data);
          this.contextForm.patchValue({
            academicYearId: response.data.selectedAcademicYearId || null,
            semester: response.data.selectedSemester,
            profileId: response.data.selectedProfile?.id ?? null
          }, { emitEvent: false });
          this.applyProfile(response.data.selectedProfile);
          if (!preserveConflict) {
            this.conflictMessage.set('');
            this.conflictDraft.set(null);
          }
        },
        error: (error: HttpErrorResponse) =>
          this.toast.error('تعذر تحميل إعدادات الجدول', extractHttpErrorMessage(error) ?? 'حاول مرة أخرى.')
      });
  }

  private applyProfile(profile: TimetableSetupProfile | null): void {
    this.creating.set(profile === null);
    this.profileForm.reset({ name: profile?.name ?? '' });
    this.profileForm.markAsPristine();
  }

  private confirmDiscard(): boolean {
    return !this.hasUnsavedChanges()
      || window.confirm('لديك تغييرات غير محفوظة. هل تريد تجاهلها؟');
  }

  private restoreContextControls(): void {
    const overview = this.overview();
    if (!overview) return;
    this.contextForm.patchValue({
      academicYearId: overview.selectedAcademicYearId,
      semester: overview.selectedSemester,
      profileId: overview.selectedProfile?.id ?? null
    }, { emitEvent: false });
  }
}
