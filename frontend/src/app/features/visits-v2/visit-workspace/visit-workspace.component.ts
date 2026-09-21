import { CommonModule } from '@angular/common';
import { Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { finalize } from 'rxjs';
import {
  CreateVisitV2Request, UpdateVisitV2Request, VisitV2ArchiveItem, VisitV2ArchiveQuery,
  VisitV2Dashboard, VisitV2Detail, VisitV2Domain, VisitV2ObservationCard, VisitV2Standard
} from '../../../core/models/visit-v2.models';
import { VISIT_CATEGORIES, VISIT_SEQUENCES } from '../../../core/models/visit.models';
import { AuthService } from '../../../core/services/auth.service';
import { TeachersService } from '../../../core/services/teachers.service';
import { ToastService } from '../../../core/services/toast.service';
import { VisitsV2Service } from '../../../core/services/visits-v2.service';
import { applyQuickScore, liveTotals, suggestedScore } from '../visit-v2-calculator';

type WorkspaceTab = 'card' | 'archive' | 'dashboard' | 'report';

@Component({
  selector: 'app-visit-v2-workspace',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule],
  templateUrl: './visit-workspace.component.html',
  styleUrls: ['./visit-workspace.component.css']
})
export class VisitWorkspaceComponent implements OnInit {
  private readonly visits = inject(VisitsV2Service);
  private readonly teachers = inject(TeachersService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  readonly enabled = signal<boolean | null>(null);
  readonly busy = signal(false);
  readonly dirty = signal(false);
  readonly tab = signal<WorkspaceTab>('card');
  readonly card = signal<VisitV2ObservationCard | null>(null);
  readonly domains = signal<VisitV2Domain[]>([]);
  readonly detail = signal<VisitV2Detail | null>(null);
  readonly instructors = signal<{ userId: string; fullName: string }[]>([]);
  readonly archive = signal<VisitV2ArchiveItem[]>([]);
  readonly archiveTotal = signal(0);
  readonly evaluators = signal<{ userId: string; displayName: string }[]>([]);
  readonly dashboard = signal<VisitV2Dashboard | null>(null);
  readonly cardError = signal(false);
  readonly archiveError = signal(false);
  readonly dashboardError = signal(false);
  readonly categories = VISIT_CATEGORIES;
  readonly sequences = VISIT_SEQUENCES;
  readonly scoreValues = [1, 2, 3, 4];
  readonly archiveStatuses = [
    { value: 1, key: 'VISITS_V2.STATUS_DRAFT' },
    { value: 2, key: 'VISITS_V2.STATUS_REJECTED' },
    { value: 3, key: 'VISITS_V2.STATUS_PENDING' },
    { value: 4, key: 'VISITS_V2.STATUS_APPROVED' },
    { value: 5, key: 'VISITS_V2.STATUS_REOPENED' }
  ];
  readonly isInstructor = computed(() => this.auth.roles().includes('Instructor') && !this.canManage());
  readonly total = computed(() => liveTotals(this.domains()));
  readonly standardsCount = computed(() => this.domains().reduce((sum, domain) => sum + domain.standards.length, 0));
  readonly indicatorsCount = computed(() => this.domains().reduce(
    (sum, domain) => sum + domain.standards.reduce((domainSum, standard) => domainSum + standard.indicators.length, 0), 0));
  readonly isReadOnly = computed(() => this.detail()?.isReadOnly ?? false);

  model: CreateVisitV2Request = this.emptyModel();
  query: VisitV2ArchiveQuery = { page: 1, pageSize: 20 };

  ngOnInit(): void {
    this.visits.availability().subscribe({
      next: response => {
        const available = !!response.data?.isEnabled;
        this.enabled.set(available);
        if (!available) return;
        if (this.isInstructor()) {
          this.tab.set('archive');
          this.loadArchive();
        } else {
          this.loadCard();
          this.loadTeachers();
        }
      },
      error: () => this.enabled.set(false)
    });
  }

  canManage(): boolean {
    return this.auth.roles().some(role => ['SchoolManager', 'Moderator', 'MainManager', 'SuperAdmin'].includes(role));
  }

  canEditTreatments(visit: VisitV2Detail): boolean {
    return this.canManage();
  }

  selectTab(tab: WorkspaceTab): void {
    this.tab.set(tab);
    if (tab === 'archive') this.loadArchive();
    if (tab === 'dashboard' && this.canManage()) this.loadDashboard();
  }

  loadCard(): void {
    this.busy.set(true);
    this.cardError.set(false);
    this.visits.observationCard().pipe(finalize(() => this.busy.set(false))).subscribe({
      next: response => {
        if (!response.data) return;
        this.card.set(response.data);
        this.domains.set(this.cloneDomains(response.data.domains));
      },
      error: () => this.cardError.set(true)
    });
  }

  loadTeachers(): void {
    this.teachers.list({ page: 1, pageSize: 100 }).subscribe(response => {
      this.instructors.set((response.data?.items ?? []).filter(x => x.isActive).map(x => ({ userId: x.userId, fullName: x.fullName })));
    });
  }

  teacherChanged(): void {
    this.markDirty();
    if (!this.model.instructorId) return;
    this.teachers.getTeaching(this.model.instructorId, true).subscribe(response => {
      if (!response.data) return;
      this.model.subject = response.data.subject ?? this.model.subject;
      this.model.gradeClass = response.data.classes?.[0] ?? this.model.gradeClass;
    });
  }

  indicatorChanged(standard: VisitV2Standard): void {
    standard.score = suggestedScore(standard.indicators.filter(x => x.isObserved).length, standard.indicators.length);
    this.domains.update(domains => [...domains]);
    this.markDirty();
  }

  quickScore(standard: VisitV2Standard, score: number): void {
    const updated = applyQuickScore(standard, score);
    Object.assign(standard, updated);
    this.domains.update(domains => [...domains]);
    this.markDirty();
  }

  quickScoreAll(score: number): void {
    this.domains.update(domains => domains.map(domain => ({
      ...domain,
      standards: domain.standards.map(standard => applyQuickScore(standard, score))
    })));
    this.markDirty();
  }

  domainTotal(domain: VisitV2Domain): number {
    return domain.standards.reduce((sum, standard) => sum + standard.score, 0);
  }

  scoreKey(score: number): string { return `VISITS_V2.SCORE_${score}`; }

  save(finalizeVisit = false): void {
    if (!this.valid()) {
      this.toast.warn('VISITS_V2.VALIDATION');
      return;
    }
    this.busy.set(true);
    const currentId = this.detail()?.id;
    if (currentId) {
      this.updateThen(currentId, finalizeVisit);
      return;
    }
    this.visits.create(this.model).subscribe({
      next: response => {
        if (!response.data) { this.busy.set(false); return; }
        this.detail.set(response.data);
        this.updateThen(response.data.id, finalizeVisit);
      },
      error: () => this.busy.set(false)
    });
  }

  private updateThen(id: number, finalizeVisit: boolean): void {
    this.visits.update(id, this.updateRequest()).subscribe({
      next: response => {
        if (!response.data) { this.busy.set(false); return; }
        this.applyDetail(response.data);
        this.dirty.set(false);
        if (!finalizeVisit) {
          this.busy.set(false);
          this.toast.success('VISITS_V2.SAVED');
          return;
        }
        this.visits.finalize(id).pipe(finalize(() => this.busy.set(false))).subscribe(finalized => {
          if (!finalized.data) return;
          this.applyDetail(finalized.data);
          this.tab.set('report');
          this.toast.success('VISITS_V2.FINALIZED');
        });
      },
      error: () => this.busy.set(false)
    });
  }

  newVisit(): void {
    if (this.hasUnsavedChanges() && !window.confirm(this.translate.instant('VISITS_V2.DISCARD_CONFIRM'))) return;
    this.detail.set(null);
    this.model = this.emptyModel();
    this.dirty.set(false);
    this.tab.set('card');
    this.loadCard();
  }

  openVisit(id: number): void {
    this.busy.set(true);
    this.visits.get(id).pipe(finalize(() => this.busy.set(false))).subscribe(response => {
      if (!response.data) return;
      this.applyDetail(response.data);
      this.tab.set(response.data.isReadOnly ? 'report' : 'card');
      this.dirty.set(false);
    });
  }

  loadArchive(): void {
    this.busy.set(true);
    this.archiveError.set(false);
    this.visits.list(this.query).pipe(finalize(() => this.busy.set(false))).subscribe({
      next: response => {
        if (!response.data) return;
        this.archive.set([...response.data.page.items]);
        this.archiveTotal.set(response.data.page.totalCount);
        this.evaluators.set(response.data.evaluators);
      },
      error: () => this.archiveError.set(true)
    });
  }

  changePage(delta: number): void {
    this.query.page = Math.max(1, (this.query.page ?? 1) + delta);
    this.loadArchive();
  }

  loadDashboard(): void {
    this.busy.set(true);
    this.dashboardError.set(false);
    this.visits.dashboard().pipe(finalize(() => this.busy.set(false))).subscribe({
      next: response => this.dashboard.set(response.data ?? null),
      error: () => this.dashboardError.set(true)
    });
  }

  deleteVisit(item: VisitV2ArchiveItem): void {
    if (!window.confirm(this.translate.instant('VISITS_V2.DELETE_CONFIRM'))) return;
    this.visits.softDelete(item.id).subscribe(() => {
      this.toast.success('VISITS_V2.DELETED');
      this.loadArchive();
    });
  }

  approve(): void { this.workflow(() => this.visits.approve(this.detail()!.id)); }
  reject(): void {
    const reason = window.prompt(this.translate.instant('VISITS_V2.REJECTION_REASON'));
    if (reason?.trim()) this.workflow(() => this.visits.reject(this.detail()!.id, reason.trim()));
  }
  reopen(): void {
    const reason = window.prompt(this.translate.instant('VISITS_V2.REOPEN_REASON'));
    if (reason?.trim()) this.workflow(() => this.visits.reopen(this.detail()!.id, reason.trim()));
  }

  private workflow(request: () => ReturnType<VisitsV2Service['approve']>): void {
    this.busy.set(true);
    request().pipe(finalize(() => this.busy.set(false))).subscribe(response => {
      if (response.data) this.applyDetail(response.data);
    });
  }

  saveTreatments(): void {
    const visit = this.detail();
    if (!visit) return;
    if (visit.treatments.some(item => !item.domainNameAr.trim() || !item.goal.trim() || !item.actions.trim() || !item.successIndicators.trim())) {
      this.toast.warn('VISITS_V2.TREATMENT_VALIDATION');
      return;
    }
    this.visits.updateTreatments(visit.id, visit.treatments).subscribe(response => {
      if (response.data) this.detail.update(current => current ? ({ ...current, treatments: response.data! }) : current);
      this.toast.success('VISITS_V2.TREATMENTS_SAVED');
    });
  }

  addTreatment(): void {
    this.detail.update(visit => visit ? ({
      ...visit,
      treatments: [...visit.treatments, {
        id: 0,
        rubricDomainId: null,
        domainNameAr: this.translate.instant('VISITS_V2.CUSTOM_GOAL'),
        goal: '',
        actions: '',
        successIndicators: '',
        source: 2,
        sortOrder: visit.treatments.length + 1
      }]
    }) : visit);
  }

  removeTreatment(index: number): void {
    this.detail.update(visit => visit ? ({
      ...visit,
      treatments: visit.treatments.filter((_, itemIndex) => itemIndex !== index)
    }) : visit);
  }

  downloadCsv(): void {
    this.visits.exportCsv(this.query).subscribe(response => this.download(response.body, 'classroom-visits-v2.csv'));
  }

  downloadPdf(): void {
    const visit = this.detail();
    if (visit) this.visits.exportPdf(visit.id).subscribe(response => this.download(response.body, `visit-${visit.id}.pdf`));
  }

  markDirty(): void { if (!this.isReadOnly()) this.dirty.set(true); }
  hasUnsavedChanges(): boolean { return this.dirty(); }

  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent): void {
    if (this.hasUnsavedChanges()) event.preventDefault();
  }

  private valid(): boolean {
    return !!this.model.instructorId && !!this.model.subject.trim() && !!this.model.gradeClass.trim()
      && !!this.model.lessonTitle.trim() && this.model.classroomPeriod >= 1 && this.model.classroomPeriod <= 7
      && this.model.presentCount >= 0 && this.model.absentCount >= 0;
  }

  private updateRequest(): UpdateVisitV2Request {
    const { instructorId: _, ...metadata } = this.model;
    return {
      ...metadata,
      scores: this.domains().flatMap(domain => domain.standards.map(standard => ({
        rubricStandardId: standard.id,
        score: standard.score,
        evidenceNote: standard.evidenceNote,
        observedIndicatorIds: standard.indicators.filter(x => x.isObserved).map(x => x.id)
      })))
    };
  }

  private applyDetail(detail: VisitV2Detail): void {
    this.detail.set(detail);
    this.domains.set(this.cloneDomains(detail.domains));
    this.model = {
      instructorId: detail.instructorId, visitCategory: detail.visitCategory, visitSequence: detail.visitSequence,
      visitDate: detail.visitDate.substring(0, 10), classroomPeriod: detail.classroomPeriod,
      subject: detail.subject, gradeClass: detail.gradeClass, lessonTitle: detail.lessonTitle,
      presentCount: detail.presentCount, absentCount: detail.absentCount, notes: detail.notes
    };
  }

  private cloneDomains(domains: VisitV2Domain[]): VisitV2Domain[] {
    return domains.map(domain => ({ ...domain, standards: domain.standards.map(standard => ({
      ...standard, indicators: standard.indicators.map(indicator => ({ ...indicator }))
    })) }));
  }

  private emptyModel(): CreateVisitV2Request {
    return {
      instructorId: '', visitCategory: 2, visitSequence: 1, visitDate: new Date().toISOString().substring(0, 10),
      classroomPeriod: 1, subject: '', gradeClass: '', lessonTitle: '', presentCount: 0, absentCount: 0, notes: ''
    };
  }

  private download(blob: Blob | null, filename: string): void {
    if (!blob) return;
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url; anchor.download = filename; anchor.click(); URL.revokeObjectURL(url);
  }
}
