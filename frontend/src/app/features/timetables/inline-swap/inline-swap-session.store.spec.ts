import { TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';
import { ApiResponse } from '../../../core/models/api-response.model';
import { InlineSwapCandidates } from '../../../core/models/timetable-substitution.models';
import { TimetableSubstitutionService } from '../../../core/services/timetable-substitution.service';
import { InlineSwapSessionStore } from './inline-swap-session.store';

describe('InlineSwapSessionStore', () => {
  let store: InlineSwapSessionStore;
  let responses: Subject<ApiResponse<InlineSwapCandidates>>;
  let api: jasmine.SpyObj<TimetableSubstitutionService>;

  beforeEach(() => {
    responses = new Subject<ApiResponse<InlineSwapCandidates>>();
    api = jasmine.createSpyObj<TimetableSubstitutionService>('TimetableSubstitutionService', ['inlineCandidates', 'executeInline']);
    api.inlineCandidates.and.returnValue(responses);
    TestBed.configureTestingModule({ providers: [InlineSwapSessionStore, { provide: TimetableSubstitutionService, useValue: api }] });
    store = TestBed.inject(InlineSwapSessionStore);
    store.begin({
      entry: { id: 10, instructorProfileId: 1, day: 2, period: 1, entryType: 1, classLabel: '1/A', subject: 'Math' },
      teacherName: 'Teacher', dayLabel: 'Sunday', periodLabel: '1'
    });
  });

  it('ignores an analysis response after cancellation', () => {
    store.analyse(1, '2026-09-20', 'SameDay');
    expect(store.phase()).toBe('analysing');

    store.cancel();
    responses.next(successResult());

    expect(store.phase()).toBe('idle');
    expect(store.result()).toBeNull();
  });

  it('maps server cells and opens red candidates for explanation', () => {
    store.analyse(1, '2026-09-20', 'SameDay');
    responses.next(successResult());

    const cell = store.cellFor(20)!;
    store.review(cell);

    expect(store.phase()).toBe('reviewing');
    expect(store.selectedProposal()?.color).toBe('Red');
    expect(store.alternativeProposals().map(item => item.id)).toEqual(['three-way']);
  });

  it('requests and retains the whole-timetable search scope', () => {
    store.analyse(1, '2026-09-20', 'WholeTimetable');
    responses.next(successResult('WholeTimetable'));

    expect(api.inlineCandidates).toHaveBeenCalledOnceWith(1, '2026-09-20', 10, 'WholeTimetable');
    expect(store.scope()).toBe('WholeTimetable');
    expect(store.result()?.scope).toBe('WholeTimetable');
    expect(store.phase()).toBe('selecting');
  });

  it('does not present a red alternative as an executable suggestion', () => {
    store.analyse(1, '2026-09-20', 'SameDay');
    const response = successResult();
    response.data!.cells[0].alternativeProposalIds.push('blocked-three-way');
    response.data!.proposals.push({
      id: 'blocked-three-way', kind: 'ThreeWaySwap', color: 'Red', label: 'Blocked',
      errors: ['collision'], warnings: [], preview: []
    });
    responses.next(response);

    const cell = store.cellFor(20)!;
    store.review(cell);

    expect(store.alternativeProposals().map(item => item.id)).toEqual(['three-way']);
    expect(store.hasAvailableAlternative(cell)).toBeTrue();
    expect(store.selectProposal('blocked-three-way')).toBeNull();
    expect(store.selectedProposal()?.id).toBe('direct');
  });

  function successResult(scope: InlineSwapCandidates['scope'] = 'SameDay'): ApiResponse<InlineSwapCandidates> {
    return {
      isSuccess: true,
      message: '',
      errors: [],
      data: {
        timetableId: 1, revision: 2, date: '2026-09-20', sourceEntryId: 10, sourceEntryIds: [10],
        scope, expiresAt: '2099-01-01T00:00:00Z', canOverride: true,
        cells: [{ anchorEntryId: 20, entryIds: [20], teacherId: 2, day: 2, period: 2, color: 'Red',
          directProposalId: 'direct', alternativeProposalIds: ['three-way'], reasonSummary: 'collision' }],
        proposals: [
          { id: 'direct', kind: 'DirectSwap', color: 'Red', label: 'Direct', errors: ['collision'], warnings: [], preview: [] },
          { id: 'three-way', kind: 'ThreeWaySwap', color: 'Green', label: 'Three way', errors: [], warnings: [], preview: [] }
        ]
      }
    };
  }
});
