import { Injectable, OnDestroy, computed, inject, signal } from '@angular/core';
import { Subscription, firstValueFrom } from 'rxjs';
import { ApiResponse } from '../../../core/models/api-response.model';
import {
  InlineCandidateCell,
  InlineSwapCandidates,
  SubstitutionHistory,
  SwapCandidate,
  SwapSearchScope
} from '../../../core/models/timetable-substitution.models';
import { TimetableEntry } from '../../../core/models/timetable.models';
import { TimetableSubstitutionService } from '../../../core/services/timetable-substitution.service';

export type InlineSwapPhase = 'idle' | 'choosing' | 'analysing' | 'selecting' | 'reviewing' | 'executing';

export interface InlineSwapSource {
  entry: TimetableEntry;
  teacherName: string;
  dayLabel: string;
  periodLabel: string;
}

@Injectable()
export class InlineSwapSessionStore implements OnDestroy {
  private readonly api = inject(TimetableSubstitutionService);
  private analysisSubscription: Subscription | null = null;
  private requestGeneration = 0;

  readonly phase = signal<InlineSwapPhase>('idle');
  readonly source = signal<InlineSwapSource | null>(null);
  readonly scope = signal<SwapSearchScope>('SameDay');
  readonly result = signal<InlineSwapCandidates | null>(null);
  readonly selectedCell = signal<InlineCandidateCell | null>(null);
  readonly selectedProposalId = signal<string | null>(null);
  readonly error = signal('');
  readonly stale = signal(false);

  readonly active = computed(() => this.phase() !== 'idle');
  readonly selectedProposal = computed(() => this.proposal(this.selectedProposalId()));
  readonly alternativeProposals = computed(() => {
    const cell = this.selectedCell();
    return cell ? cell.alternativeProposalIds.map(id => this.proposal(id)).filter((item): item is SwapCandidate => !!item) : [];
  });
  readonly counts = computed(() => {
    const unique = new Map<number, InlineCandidateCell>();
    for (const cell of this.result()?.cells ?? []) if (!unique.has(cell.anchorEntryId)) unique.set(cell.anchorEntryId, cell);
    const values = [...unique.values()];
    return {
      green: values.filter(cell => cell.color === 'Green').length,
      yellow: values.filter(cell => cell.color === 'Yellow').length,
      red: values.filter(cell => cell.color === 'Red').length
    };
  });

  begin(source: InlineSwapSource): void {
    this.cancelRequest();
    this.source.set(source);
    this.scope.set('SameDay');
    this.result.set(null);
    this.selectedCell.set(null);
    this.selectedProposalId.set(null);
    this.error.set('');
    this.stale.set(false);
    this.phase.set('choosing');
  }

  analyse(timetableId: number, date: string, scope: SwapSearchScope): void {
    const sourceEntryId = this.source()?.entry.id;
    if (!sourceEntryId) return;
    this.cancelRequest();
    const generation = ++this.requestGeneration;
    this.scope.set(scope);
    this.result.set(null);
    this.selectedCell.set(null);
    this.selectedProposalId.set(null);
    this.error.set('');
    this.stale.set(false);
    this.phase.set('analysing');
    this.analysisSubscription = this.api.inlineCandidates(timetableId, date, sourceEntryId, scope).subscribe({
      next: response => {
        if (generation !== this.requestGeneration) return;
        try {
          this.result.set(this.unwrap(response));
          this.phase.set('selecting');
        } catch (error) {
          this.error.set(error instanceof Error ? error.message : 'تعذر تحليل بدائل التبديل.');
          this.phase.set('choosing');
        }
      },
      error: error => {
        if (generation !== this.requestGeneration) return;
        this.error.set(error?.error?.errors?.[0] ?? error?.error?.message ?? 'تعذر الاتصال بالخادم.');
        this.phase.set('choosing');
      }
    });
  }

  cellFor(entryId: number | null): InlineCandidateCell | null {
    if (!entryId) return null;
    return this.result()?.cells.find(cell => cell.entryIds.includes(entryId)) ?? null;
  }

  isSource(entryId: number | null): boolean {
    return !!entryId && (this.result()?.sourceEntryIds.includes(entryId) ?? this.source()?.entry.id === entryId);
  }

  review(cell: InlineCandidateCell): void {
    const proposalId = cell.directProposalId ?? cell.alternativeProposalIds[0];
    if (!proposalId) return;
    this.selectedCell.set(cell);
    this.selectedProposalId.set(proposalId);
    this.error.set('');
    this.stale.set(false);
    this.phase.set('reviewing');
  }

  selectProposal(proposalId: string): void {
    if (!this.proposal(proposalId)) return;
    this.selectedProposalId.set(proposalId);
    this.error.set('');
  }

  backToGrid(): void {
    this.selectedProposalId.set(null);
    this.selectedCell.set(null);
    this.error.set('');
    this.stale.set(false);
    if (this.result()) this.phase.set('selecting');
  }

  async execute(overrideReason: string | null): Promise<SubstitutionHistory> {
    const result = this.result();
    const proposalId = this.selectedProposalId();
    if (!result || !proposalId) throw new Error('اختر اقتراحًا صالحًا أولًا.');
    this.phase.set('executing');
    this.error.set('');
    try {
      return this.unwrap(await firstValueFrom(this.api.executeInline(result, proposalId, crypto.randomUUID(), overrideReason)));
    } catch (error: any) {
      this.stale.set(error?.status === 409);
      this.error.set(error?.status === 409
        ? 'تغير الجدول أو انتهت صلاحية الاقتراح. حدّث الجدول ثم أعد فحص البدائل.'
        : error?.error?.errors?.[0] ?? error?.error?.message ?? error?.message ?? 'تعذر تنفيذ التبديل.');
      this.phase.set('reviewing');
      throw error;
    }
  }

  cancel(): void {
    this.cancelRequest();
    this.requestGeneration++;
    this.phase.set('idle');
    this.source.set(null);
    this.result.set(null);
    this.selectedCell.set(null);
    this.selectedProposalId.set(null);
    this.error.set('');
    this.stale.set(false);
  }

  isExpired(): boolean {
    const expiresAt = this.result()?.expiresAt;
    return !!expiresAt && Date.parse(expiresAt) <= Date.now();
  }

  ngOnDestroy(): void {
    this.cancelRequest();
  }

  private proposal(id: string | null): SwapCandidate | null {
    return this.result()?.proposals.find(item => item.id === id) ?? null;
  }

  private unwrap<T>(response: ApiResponse<T>): T {
    if (!response.isSuccess || response.data == null) throw new Error(response.errors?.[0] ?? response.message);
    return response.data;
  }

  private cancelRequest(): void {
    this.analysisSubscription?.unsubscribe();
    this.analysisSubscription = null;
  }
}
