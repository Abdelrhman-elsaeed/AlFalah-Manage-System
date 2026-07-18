import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, FormGroup, FormArray, FormControl, Validators, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { InputNumberModule } from 'primeng/inputnumber';
import { ClearableSelectComponent } from '../../../shared/components/clearable-select/clearable-select.component';
import { CalendarModule } from 'primeng/calendar';
import { TooltipModule } from 'primeng/tooltip';
import { ProgressBarModule } from 'primeng/progressbar';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';
import { ToastService } from '../../../core/services/toast.service';
import { VisitsService } from '../../../core/services/visits.service';
import { AuthService } from '../../../core/services/auth.service';
import { TeachersService } from '../../../core/services/teachers.service';
import {
  VISIT_CATEGORIES, VISIT_SEQUENCES,
  VisitDetail, VisitScoreInput, VisitScore, CreateVisitRequest, UpdateVisitRequest,
  VisitCategoryOption, VisitSequenceOption
} from '../../../core/models/visit.models';

interface DomainGroup {
  domainId: number;
  domainCode: string;
  domainNameAr: string;
  scores: Array<{
    control: FormControl<number | null>;
    noteControl: FormControl<string>;
    standardCode: string;
    standardTextAr: string;
    rubricStandardId: number;
  }>;
}

@Component({
  selector: 'app-visit-form',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ReactiveFormsModule, TranslateModule,
    ButtonModule, InputTextModule, InputTextareaModule, InputNumberModule, ClearableSelectComponent, CalendarModule,
    TooltipModule, ProgressBarModule, ConfirmDialogModule
  ],
  providers: [ConfirmationService],
  templateUrl: './visit-form.component.html',
  styleUrls: ['./visit-form.component.css']
})
export class VisitFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly visitsService = inject(VisitsService);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmationService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly teachersService = inject(TeachersService);
  private readonly translate = inject(TranslateService);

  readonly categories = VISIT_CATEGORIES.map(option => ({
    ...option,
    label: this.translate.instant(option.labelKey)
  }));
  readonly sequences = VISIT_SEQUENCES.map(option => ({
    ...option,
    label: this.translate.instant(option.labelKey)
  }));
  readonly completedSequences = signal<Set<number>>(new Set());
  readonly instructorVisitCount = signal(0);
  readonly availableSequences = computed(() => this.sequences.map(option => ({
    ...option,
    disabled: this.completedSequences().has(option.value)
      || (option.value === 7 && this.instructorVisitCount() < 6),
    label: this.completedSequences().has(option.value)
      ? `${option.label} (${this.translate.instant('VISITS.SEQUENCE_COMPLETED')})`
      : (option.value === 7 && this.instructorVisitCount() < 6
        ? `${option.label} (${this.translate.instant('VISITS.SEQUENCE_FOLLOWUP_AFTER_SIX')})`
        : option.label)
  })));

  readonly isEdit = signal(false);
  readonly visitId = signal<number | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly submitting = signal(false);

  readonly visit = signal<VisitDetail | null>(null);
  readonly instructors = signal<{ userId: string; fullName: string }[]>([]);
  readonly instructorsLoading = signal(false);
  readonly teacherLocked = signal(false);
  readonly rubricVersionNumber = signal<number | null>(null);
  readonly isReadOnly = signal(false);
  readonly teachingLoading = signal(false);
  readonly teachingLoaded = signal(false);
  readonly teacherSubject = signal<string | null>(null);
  readonly teacherClasses = signal<string[]>([]);
  readonly teachingUnavailable = signal(false);
  readonly hasTeacherSubject = computed(() => !!this.teacherSubject());
  readonly hasTeacherClasses = computed(() => this.teacherClasses().length > 0);
  readonly classOptions = computed(() => this.teacherClasses().map(label => ({ label, value: label })));
  readonly showTeachingFallback = computed(() =>
    this.teachingLoaded() && (!this.hasTeacherSubject() || !this.hasTeacherClasses()));

  // Phase 5: rejection / reopen banners (visible when editing a returned visit)
  readonly rejectionReason = signal<string | null>(null);
  readonly reopenReason = signal<string | null>(null);
  readonly visitStatusNumber = signal<number>(0);
  readonly isRejected = computed(() => this.visitStatusNumber() === 5);
  readonly isReopened = computed(() => this.visitStatusNumber() === 6);

  readonly form: FormGroup = this.fb.group({
    instructorId: ['', Validators.required],
    visitCategory: [2, Validators.required],
    visitSequence: [1, Validators.required],
    visitDate: [new Date(), Validators.required],
    subject: ['', [Validators.required, Validators.maxLength(200)]],
    gradeClass: ['', [Validators.required, Validators.maxLength(100)]],
    lessonTitle: ['', [Validators.required, Validators.maxLength(300)]],
    presentCount: [null, [Validators.required, Validators.min(0)]],
    absentCount: [null, Validators.min(0)],
    notes: ['']
  });

  // Domain-grouped scores from the visit's immutable rubric snapshot.
  readonly domainsGrouped = signal<DomainGroup[]>([]);
  readonly expandedNoteIds = signal<Set<number>>(new Set());
  // FormControl values are not Angular signals; bump this revision whenever
  // a score changes so the progress bar and submit action recompute.
  readonly scoreRevision = signal(0);
  readonly allScoreControls = computed(() => this.domainsGrouped().flatMap(d => d.scores.map(s => s.control)));

  readonly scoredCount = computed(() => {
    this.scoreRevision();
    return this.allScoreControls().filter(c => c.value !== null && c.value !== undefined).length;
  });
  readonly totalCount = computed(() => this.allScoreControls().length);
  readonly allScored = computed(() => this.scoredCount() === this.totalCount() && this.totalCount() > 0);
  readonly progressPercent = computed(() => {
    const t = this.totalCount();
    return t === 0 ? 0 : Math.round((this.scoredCount() / t) * 100);
  });

  // Score labels (0..4) sourced from existing RUBRIC.SCORE_LABEL_0..4 i18n keys
  // (verbatim Arabic from docs/09; English mirrors in en.json). No new keys.
  readonly scoreLabels: string[] = [
    this.translate.instant('RUBRIC.SCORE_LABEL_1'),
    this.translate.instant('RUBRIC.SCORE_LABEL_2'),
    this.translate.instant('RUBRIC.SCORE_LABEL_3'),
    this.translate.instant('RUBRIC.SCORE_LABEL_4')
  ];
  readonly scoreValues: number[] = [1, 2, 3, 4];

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.isEdit.set(true);
      this.visitId.set(Number(idParam));
      this.loadExistingVisit(this.visitId()!);
    } else {
      this.form.controls['instructorId'].valueChanges.subscribe(userId => {
        this.loadTeachingForInstructor(userId);
        this.loadVisitHistory(userId);
      });
      this.loadInstructors();
    }
  }

  loadInstructors(): void {
    this.instructorsLoading.set(true);
    // Use the teacher directory endpoint rather than the general users
    // endpoint. Moderators are allowed to create visits and have
    // `Instructor.View`, but intentionally do not have `User.View`.
    // Calling /users here caused a 403 and the global interceptor redirected
    // the otherwise-authorized moderator to the Unauthorized page.
    this.teachersService.list({ page: 1, pageSize: 100 }).subscribe({
      next: (resp) => {
        this.instructorsLoading.set(false);
        if (resp.isSuccess && resp.data) {
          this.instructors.set(
            resp.data.items
              .filter(teacher => teacher.isActive)
              .map(teacher => ({ userId: teacher.userId, fullName: teacher.fullName }))
          );
          const preselectedInstructorId = this.route.snapshot.queryParamMap.get('instructorId');
          if (!this.isEdit() && preselectedInstructorId) {
            this.form.controls['instructorId'].setValue(preselectedInstructorId);
            this.form.controls['instructorId'].disable({ emitEvent: false });
            this.teacherLocked.set(true);
            this.loadVisitHistory(preselectedInstructorId);
          }
        }
      },
      error: () => this.instructorsLoading.set(false)
    });
  }

  private loadVisitHistory(userId: string | null): void {
    this.completedSequences.set(new Set());
    this.instructorVisitCount.set(0);
    if (!userId) return;

    this.teachersService.getVisits(userId).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) return;
        const activeVisits = response.data.filter(v => v.status !== 5 && v.status !== 8);
        this.instructorVisitCount.set(activeVisits.length);
        this.completedSequences.set(new Set(activeVisits.map(v => v.visitSequence)));
        const selected = Number(this.form.controls['visitSequence'].value);
        if (this.completedSequences().has(selected)) {
          const next = this.sequences.find(option => !this.completedSequences().has(option.value));
          this.form.controls['visitSequence'].setValue(next?.value ?? 4);
        }
      }
    });
  }

  private loadTeachingForInstructor(userId: string | null): void {
    this.teacherSubject.set(null);
    this.teacherClasses.set([]);
    this.teachingLoaded.set(false);
    this.teachingUnavailable.set(false);
    if (!userId) return;

    this.teachingLoading.set(true);
    // A rejected background auto-fill must leave the visit form usable. The
    // error handler below exposes the existing manual subject/class fallback.
    this.teachersService.getTeaching(userId, true).subscribe({
      next: response => {
        this.teachingLoading.set(false);
        if (this.form.controls['instructorId'].value !== userId) return;

        const teaching = response.isSuccess ? response.data : null;
        const subject = teaching?.subject?.trim() || null;
        this.teacherSubject.set(subject);
        const classes = teaching?.classes ?? [];
        this.teacherClasses.set(classes);
        this.teachingLoaded.set(true);
        this.teachingUnavailable.set(!response.isSuccess);
        this.form.patchValue({ subject: subject ?? '', gradeClass: classes[0] ?? '' }, { emitEvent: false });
      },
      error: () => {
        if (this.form.controls['instructorId'].value !== userId) return;
        this.teachingLoading.set(false);
        this.teachingLoaded.set(true);
        this.teachingUnavailable.set(true);
      }
    });
  }

  loadExistingVisit(id: number): void {
    this.loading.set(true);
    this.visitsService.getById(id).subscribe({
      next: (resp) => {
        if (resp.isSuccess && resp.data) {
          const v = resp.data;
          this.visit.set(v);
          this.isReadOnly.set(v.isReadOnly);
          this.rubricVersionNumber.set(v.rubricVersionNumber);

          // Phase 5: capture rejection / reopen banners
          this.rejectionReason.set(v.rejectionReason ?? null);
          this.reopenReason.set(v.reopenReason ?? null);
          this.visitStatusNumber.set(Number(v.status));

          this.form.patchValue({
            instructorId: v.instructorId,
            visitCategory: Number(v.visitCategory),
            visitSequence: Number(v.visitSequence),
            visitDate: new Date(v.visitDate),
            subject: v.subject ?? '',
            gradeClass: v.gradeClass ?? '',
            lessonTitle: v.lessonTitle ?? '',
            presentCount: v.presentCount,
            absentCount: v.absentCount,
            notes: v.notes ?? ''
          });

          this.buildScoreGroups(v.scores);

          // Load instructor list (for completeness — disabled anyway)
          this.loadInstructors();

          if (v.isReadOnly) {
            this.form.disable();
          }
        }
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  buildScoreGroups(scores: VisitScore[]): void {
    // Group by domain code
    const groups = new Map<string, DomainGroup>();
    for (const s of scores) {
      if (!groups.has(s.domainCode)) {
        groups.set(s.domainCode, {
          domainId: s.rubricDomainId,
          domainCode: s.domainCode,
          domainNameAr: s.domainNameAr,
          scores: []
        });
      }
      groups.get(s.domainCode)!.scores.push({
        // Zero is the legacy "not observed" value; reviewers now choose 1–4.
        control: new FormControl<number | null>(s.score === 0 ? null : s.score, { nonNullable: false }),
        noteControl: new FormControl<string>(s.evidenceNote ?? '', { nonNullable: true }),
        standardCode: s.standardCode,
        standardTextAr: s.standardTextAr,
        rubricStandardId: s.rubricStandardId
      });
    }
    const list = Array.from(groups.values())
      .sort((a, b) => a.domainCode.localeCompare(b.domainCode))
      .map(g => ({ ...g, scores: g.scores.sort((x, y) => x.standardCode.localeCompare(y.standardCode)) }));
    this.expandedNoteIds.set(new Set());
    this.domainsGrouped.set(list);
  }

  // ─── Score button interactions ────────────────────────────────────────────
  setScore(s: { control: FormControl<number | null> }, value: number): void {
    // Toggle: clicking the same value again clears it (lets the user undo).
    s.control.setValue(s.control.value === value ? null : value);
    s.control.markAsDirty();
    this.scoreRevision.update(revision => revision + 1);
  }

  isNoteExpanded(s: { rubricStandardId: number }): boolean {
    return this.expandedNoteIds().has(s.rubricStandardId);
  }

  toggleNote(s: { rubricStandardId: number }): void {
    this.expandedNoteIds.update(current => {
      const next = new Set(current);
      if (next.has(s.rubricStandardId)) next.delete(s.rubricStandardId);
      else next.add(s.rubricStandardId);
      return next;
    });
  }

  noteToggleLabel(s: { rubricStandardId: number; noteControl: FormControl<string> }): string {
    if (this.isNoteExpanded(s)) return 'VISITS.NOTE_HIDE';
    return s.noteControl.value?.trim() ? 'VISITS.NOTE_ADDED' : 'VISITS.ADD_NOTE';
  }

  // ─── Actions ──────────────────────────────────────────────────────────────

  saveDraft(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.toast.warn(
        this.translate.instant('VISITS.FORM_INCOMPLETE_TITLE'),
        this.translate.instant('VISITS.FORM_INCOMPLETE_DESC'));
      return;
    }

    this.saving.set(true);
    const scores = this.collectScores();
    const values = this.form.getRawValue();

    if (this.isEdit()) {
      const body: UpdateVisitRequest = {
        visitCategory: values.visitCategory,
        visitSequence: values.visitSequence,
        visitDate: new Date(values.visitDate).toISOString(),
        subject: values.subject,
        gradeClass: values.gradeClass,
        lessonTitle: values.lessonTitle,
        presentCount: Number(values.presentCount),
        absentCount: values.absentCount === null || values.absentCount === '' ? null : Number(values.absentCount),
        notes: values.notes,
        scores
      };
      this.visitsService.update(this.visitId()!, body).subscribe({
        next: (resp) => this.handleSave(resp, false),
        error: () => this.saving.set(false)
      });
    } else {
      const body: CreateVisitRequest = {
        instructorId: values.instructorId,
        visitCategory: values.visitCategory,
        visitSequence: values.visitSequence,
        visitDate: new Date(values.visitDate).toISOString(),
        subject: values.subject,
        gradeClass: values.gradeClass,
        lessonTitle: values.lessonTitle,
        presentCount: Number(values.presentCount),
        absentCount: values.absentCount === null || values.absentCount === '' ? null : Number(values.absentCount),
        notes: values.notes,
        scores
      };
      this.visitsService.create(body).subscribe({
        next: (resp) => this.handleSave(resp, true),
        error: () => this.saving.set(false)
      });
    }
  }

  handleSave(resp: { isSuccess: boolean; message?: string; data?: VisitDetail }, isCreate: boolean): void {
    this.saving.set(false);
    if (resp.isSuccess && resp.data) {
      this.toast.success(
        this.translate.instant('VISITS.SAVE_SUCCESS_TITLE'),
        resp.message || this.translate.instant('VISITS.SAVE_SUCCESS_DESC'));
      if (isCreate) {
        // After create, redirect to observation mode for the snapshotted standards.
        this.router.navigate(['/visits', resp.data.id, 'edit']);
      } else {
        // Reload to pick up fresh state (in case rubricVersion etc. changed)
        this.visitId.set(resp.data.id);
        this.isEdit.set(true);
        this.visit.set(resp.data);
        this.rubricVersionNumber.set(resp.data.rubricVersionNumber);
        this.buildScoreGroups(resp.data.scores);
        this.isReadOnly.set(resp.data.isReadOnly);
        this.rejectionReason.set(resp.data.rejectionReason ?? null);
        this.reopenReason.set(resp.data.reopenReason ?? null);
        this.visitStatusNumber.set(Number(resp.data.status));
      }
    } else {
      this.toast.error(
        this.translate.instant('VISITS.SAVE_FAILED'),
        resp.message || this.translate.instant('VISITS.SAVE_FAILED'));
    }
  }

  confirmSubmit(): void {
    if (!this.allScored()) {
      this.toast.warn(
        this.translate.instant('VISITS.SUBMIT_BLOCKED_TITLE'),
        this.translate.instant('VISITS.SUBMIT_BLOCKED_DESC', {
          scored: this.scoredCount(),
          total: this.totalCount()
        }));
      return;
    }
    this.confirm.confirm({
      message: this.translate.instant('VISITS.CONFIRM_SUBMIT'),
      header: this.translate.instant('VISITS.SUBMIT_CONFIRM_TITLE'),
      icon: 'pi pi-send',
      acceptLabel: this.translate.instant('VISITS.SUBMIT_ACCEPT'),
      rejectLabel: this.translate.instant('COMMON.CANCEL'),
      accept: () => this.submitNow()
    });
  }

  submitNow(): void {
    // Save first (to ensure latest), then submit
    this.saving.set(true);
    const scores = this.collectScores();
    const values = this.form.getRawValue();
    const body: UpdateVisitRequest = {
      visitCategory: values.visitCategory,
      visitSequence: values.visitSequence,
      visitDate: new Date(values.visitDate).toISOString(),
      subject: values.subject,
      gradeClass: values.gradeClass,
      lessonTitle: values.lessonTitle,
      presentCount: Number(values.presentCount),
      absentCount: values.absentCount === null || values.absentCount === '' ? null : Number(values.absentCount),
      notes: values.notes,
      scores
    };
    this.visitsService.update(this.visitId()!, body).subscribe({
      next: (resp) => {
        this.saving.set(false);
        if (!resp.isSuccess) {
          this.toast.error(
            this.translate.instant('VISITS.SAVE_FAILED'),
            resp.message || this.translate.instant('VISITS.SAVE_BEFORE_SUBMIT_FAILED'));
          return;
        }
        this.submitting.set(true);
        this.visitsService.submit(this.visitId()!).subscribe({
          next: (subResp) => {
            this.submitting.set(false);
            if (subResp.isSuccess && subResp.data) {
              this.toast.success(
                this.translate.instant('VISITS.SUBMIT_SUCCESS_TITLE'),
                subResp.message || this.translate.instant('VISITS.SUBMIT_SUCCESS_DESC'));
              this.router.navigate(['/visits', this.visitId()]);
            } else {
              this.toast.error(
                this.translate.instant('VISITS.SUBMIT_FAILED'),
                subResp.message || this.translate.instant('VISITS.SUBMIT_FAILED'));
            }
          },
          error: () => this.submitting.set(false)
        });
      },
      error: () => this.saving.set(false)
    });
  }

  cancel(): void {
    this.router.navigate(['/visits']);
  }

  collectScores(): VisitScoreInput[] {
    const out: VisitScoreInput[] = [];
    for (const d of this.domainsGrouped()) {
      for (const s of d.scores) {
        const noteVal = (s.noteControl.value ?? '').trim();
        out.push({
          rubricStandardId: s.rubricStandardId,
          score: s.control.value === null || s.control.value === undefined ? null : Number(s.control.value),
          evidenceNote: noteVal.length > 0 ? noteVal : null
        });
      }
    }
    return out;
  }

  // Helper to know how many standards are in a domain (header label).
  domainStandardCount(domain: DomainGroup): number { return domain.scores.length; }
  domainScoredCount(domain: DomainGroup): number {
    return domain.scores.filter(s => s.control.value !== null && s.control.value !== undefined).length;
  }

  selectedScoreLabel(value: number | null): string {
    return value === null || value === undefined ? '' : this.scoreLabels[value - 1] ?? '';
  }

  isEmpty(): boolean { return this.domainsGrouped().length === 0; }

  trackDomain(_idx: number, d: DomainGroup): string { return d.domainCode; }
  trackStandard(_idx: number, s: { rubricStandardId: number }): number { return s.rubricStandardId; }

  // Score-button accessibility helpers
  scoreButtonClass(value: number, current: number | null): string {
    const selected = current === value;
    return `score-btn score-btn--v${value}` + (selected ? ' score-btn--active' : '');
  }
}
