import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, FormGroup, FormArray, FormControl, Validators, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { DropdownModule } from 'primeng/dropdown';
import { CalendarModule } from 'primeng/calendar';
import { TooltipModule } from 'primeng/tooltip';
import { ProgressBarModule } from 'primeng/progressbar';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';
import { ToastService } from '../../../core/services/toast.service';
import { VisitsService } from '../../../core/services/visits.service';
import { UsersService } from '../../../core/services/users.service';
import { AuthService } from '../../../core/services/auth.service';
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
    ButtonModule, InputTextModule, InputTextareaModule, DropdownModule, CalendarModule,
    TooltipModule, ProgressBarModule, ConfirmDialogModule
  ],
  providers: [ConfirmationService],
  templateUrl: './visit-form.component.html',
  styleUrls: ['./visit-form.component.css']
})
export class VisitFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly visitsService = inject(VisitsService);
  private readonly usersService = inject(UsersService);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmationService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly translate = inject(TranslateService);

  readonly categories = VISIT_CATEGORIES;
  readonly sequences = VISIT_SEQUENCES;

  readonly isEdit = signal(false);
  readonly visitId = signal<number | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly submitting = signal(false);

  readonly visit = signal<VisitDetail | null>(null);
  readonly instructors = signal<{ userId: string; fullName: string }[]>([]);
  readonly instructorsLoading = signal(false);
  readonly rubricVersionNumber = signal<number | null>(null);
  readonly isReadOnly = signal(false);

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
    subject: [''],
    gradeClass: [''],
    notes: ['']
  });

  // Domain-grouped scores (5 domains × {6,4,6,3,6} standards = 25)
  readonly domainsGrouped = signal<DomainGroup[]>([]);
  readonly allScoreControls = computed(() => this.domainsGrouped().flatMap(d => d.scores.map(s => s.control)));

  readonly scoredCount = computed(() => this.allScoreControls().filter(c => c.value !== null && c.value !== undefined).length);
  readonly totalCount = computed(() => this.allScoreControls().length);
  readonly allScored = computed(() => this.scoredCount() === this.totalCount() && this.totalCount() > 0);
  readonly progressPercent = computed(() => {
    const t = this.totalCount();
    return t === 0 ? 0 : Math.round((this.scoredCount() / t) * 100);
  });

  // Score labels (0..4) sourced from existing RUBRIC.SCORE_LABEL_0..4 i18n keys
  // (verbatim Arabic from docs/09; English mirrors in en.json). No new keys.
  readonly scoreLabels: string[] = [
    this.translate.instant('RUBRIC.SCORE_LABEL_0'),
    this.translate.instant('RUBRIC.SCORE_LABEL_1'),
    this.translate.instant('RUBRIC.SCORE_LABEL_2'),
    this.translate.instant('RUBRIC.SCORE_LABEL_3'),
    this.translate.instant('RUBRIC.SCORE_LABEL_4')
  ];
  readonly scoreValues: number[] = [0, 1, 2, 3, 4];

  // Tracks which standards have their evidence note expanded (collapsible per row).
  readonly noteExpanded = signal<Set<number>>(new Set());

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.isEdit.set(true);
      this.visitId.set(Number(idParam));
      this.loadExistingVisit(this.visitId()!);
    } else {
      this.loadInstructors();
    }
  }

  loadInstructors(): void {
    this.instructorsLoading.set(true);
    this.usersService.list({ role: 'Instructor', isActive: true, pageSize: 100 }).subscribe({
      next: (resp) => {
        this.instructorsLoading.set(false);
        if (resp.isSuccess && resp.data) {
          this.instructors.set(
            resp.data.items.map(u => ({ userId: u.userId, fullName: u.fullName }))
          );
        }
      },
      error: () => this.instructorsLoading.set(false)
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
    // Auto-expand any standard that already has an evidence note (round-trip edit).
    const autoExpanded = new Set<number>();
    for (const s of scores) {
      if (s.evidenceNote && s.evidenceNote.trim().length > 0) {
        autoExpanded.add(s.rubricStandardId);
      }
    }
    this.noteExpanded.set(autoExpanded);

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
        control: new FormControl<number | null>(s.score, { nonNullable: false }),
        noteControl: new FormControl<string>(s.evidenceNote ?? '', { nonNullable: true }),
        standardCode: s.standardCode,
        standardTextAr: s.standardTextAr,
        rubricStandardId: s.rubricStandardId
      });
    }
    const list = Array.from(groups.values())
      .sort((a, b) => a.domainCode.localeCompare(b.domainCode))
      .map(g => ({ ...g, scores: g.scores.sort((x, y) => x.standardCode.localeCompare(y.standardCode)) }));
    this.domainsGrouped.set(list);
  }

  // ─── Score button interactions ────────────────────────────────────────────
  setScore(s: { control: FormControl<number | null> }, value: number): void {
    // Toggle: clicking the same value again clears it (lets the user undo).
    s.control.setValue(s.control.value === value ? null : value);
    s.control.markAsDirty();
  }

  isNoteOpen(rubricStandardId: number): boolean {
    return this.noteExpanded().has(rubricStandardId);
  }

  toggleNote(rubricStandardId: number): void {
    const next = new Set(this.noteExpanded());
    if (next.has(rubricStandardId)) {
      next.delete(rubricStandardId);
    } else {
      next.add(rubricStandardId);
    }
    this.noteExpanded.set(next);
  }

  hasNoteContent(s: { noteControl: FormControl<string> }): boolean {
    const v = s.noteControl.value;
    return !!v && v.trim().length > 0;
  }

  // ─── Actions ──────────────────────────────────────────────────────────────

  saveDraft(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.toast.warn('بيانات ناقصة', 'يرجى تعبئة جميع الحقول الإلزامية.');
      return;
    }

    this.saving.set(true);
    const scores = this.collectScores();

    if (this.isEdit()) {
      const body: UpdateVisitRequest = {
        visitCategory: this.form.value.visitCategory,
        visitSequence: this.form.value.visitSequence,
        visitDate: new Date(this.form.value.visitDate).toISOString(),
        subject: this.form.value.subject,
        gradeClass: this.form.value.gradeClass,
        notes: this.form.value.notes,
        scores
      };
      this.visitsService.update(this.visitId()!, body).subscribe({
        next: (resp) => this.handleSave(resp, false),
        error: () => this.saving.set(false)
      });
    } else {
      const body: CreateVisitRequest = {
        instructorId: this.form.value.instructorId,
        visitCategory: this.form.value.visitCategory,
        visitSequence: this.form.value.visitSequence,
        visitDate: new Date(this.form.value.visitDate).toISOString(),
        subject: this.form.value.subject,
        gradeClass: this.form.value.gradeClass,
        notes: this.form.value.notes,
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
      this.toast.success('تم الحفظ', resp.message || 'تم حفظ المسودة.');
      if (isCreate) {
        // After create, redirect to edit page so the user can score all 25 standards
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
      this.toast.error('فشل الحفظ', resp.message || 'تعذر حفظ المسودة.');
    }
  }

  confirmSubmit(): void {
    if (!this.allScored()) {
      this.toast.warn('لا يمكن الإرسال', `تم تقييم ${this.scoredCount()} من ${this.totalCount()} معياراً. يجب تقييم جميع المعايير الـ 25 أولاً.`);
      return;
    }
    this.confirm.confirm({
      message: 'هل تريد إرسال الزيارة للاعتماد؟ لن تتمكن من تعديلها بعد الإرسال.',
      header: 'تأكيد الإرسال',
      icon: 'pi pi-send',
      acceptLabel: 'نعم، أرسل',
      rejectLabel: 'إلغاء',
      accept: () => this.submitNow()
    });
  }

  submitNow(): void {
    // Save first (to ensure latest), then submit
    this.saving.set(true);
    const scores = this.collectScores();
    const body: UpdateVisitRequest = {
      visitCategory: this.form.value.visitCategory,
      visitSequence: this.form.value.visitSequence,
      visitDate: new Date(this.form.value.visitDate).toISOString(),
      subject: this.form.value.subject,
      gradeClass: this.form.value.gradeClass,
      notes: this.form.value.notes,
      scores
    };
    this.visitsService.update(this.visitId()!, body).subscribe({
      next: (resp) => {
        this.saving.set(false);
        if (!resp.isSuccess) {
          this.toast.error('فشل الحفظ', resp.message || 'تعذر حفظ المسودة قبل الإرسال.');
          return;
        }
        this.submitting.set(true);
        this.visitsService.submit(this.visitId()!).subscribe({
          next: (subResp) => {
            this.submitting.set(false);
            if (subResp.isSuccess && subResp.data) {
              this.toast.success('تم الإرسال', subResp.message || 'تم إرسال الزيارة للاعتماد.');
              this.router.navigate(['/visits', this.visitId()]);
            } else {
              this.toast.error('فشل الإرسال', subResp.message || 'تعذر إرسال الزيارة.');
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

  isEmpty(): boolean { return this.domainsGrouped().length === 0; }

  trackDomain(_idx: number, d: DomainGroup): string { return d.domainCode; }
  trackStandard(_idx: number, s: { rubricStandardId: number }): number { return s.rubricStandardId; }

  // Score-button accessibility helpers
  scoreButtonClass(value: number, current: number | null): string {
    const selected = current === value;
    return `score-btn score-btn--v${value}` + (selected ? ' score-btn--active' : '');
  }
}