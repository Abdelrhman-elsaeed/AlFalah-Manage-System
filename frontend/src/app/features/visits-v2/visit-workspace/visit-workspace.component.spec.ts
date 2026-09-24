import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { Observable, Subject, of, throwError } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { ComplaintsService } from '../../../core/services/complaints.service';
import { TeachersService } from '../../../core/services/teachers.service';
import { ToastService } from '../../../core/services/toast.service';
import { VisitsV2Service } from '../../../core/services/visits-v2.service';
import { VisitWorkspaceComponent } from './visit-workspace.component';

describe('VisitWorkspaceComponent interaction stability', () => {
  let fixture: ComponentFixture<VisitWorkspaceComponent>;
  let component: VisitWorkspaceComponent;
  let visits: jasmine.SpyObj<VisitsV2Service>;
  let toast: jasmine.SpyObj<ToastService>;

  const card = {
    rubricVersionId: 1,
    rubricVersionNumber: 1,
    scoreLabels: [],
    domains: [{
      id: 1,
      code: 'D1',
      nameAr: 'المجال',
      sortOrder: 1,
      standards: [{
        id: 1,
        code: 'D1-S1',
        textAr: 'المعيار',
        sortOrder: 1,
        score: 1,
        evidenceNote: '',
        indicators: [{ id: 1, code: 'I1', textAr: 'المؤشر', sortOrder: 1, isObserved: false }]
      }]
    }]
  };

  const archiveResponse = {
    isSuccess: true,
    message: '',
    errors: [],
    data: {
      page: { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 },
      evaluators: []
    }
  } as any;

  beforeEach(async () => {
    visits = jasmine.createSpyObj<VisitsV2Service>('VisitsV2Service', [
      'availability', 'observationCard', 'list', 'dashboard', 'get', 'create', 'update',
      'finalize', 'approve', 'reject', 'reopen', 'softDelete', 'updateTreatments',
      'exportCsv', 'exportZip', 'exportPdf'
    ]);
    visits.availability.and.returnValue(of({ data: { isEnabled: true } } as any));
    visits.observationCard.and.returnValue(of({ data: card } as any));
    visits.list.and.returnValue(of(archiveResponse));
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'warn', 'error']);

    await TestBed.configureTestingModule({
      imports: [VisitWorkspaceComponent, TranslateModule.forRoot()],
      providers: [
        { provide: VisitsV2Service, useValue: visits },
        { provide: ComplaintsService, useValue: jasmine.createSpyObj('ComplaintsService', ['create']) },
        {
          provide: TeachersService,
          useValue: {
            list: () => of({ data: { items: [] } }),
            getTeaching: () => of({ data: null })
          }
        },
        { provide: AuthService, useValue: { roles: signal<readonly string[]>(['SchoolManager']) } },
        { provide: ToastService, useValue: toast },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(VisitWorkspaceComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('keeps AJAX feedback local instead of covering the application shell', () => {
    const pending = new Subject<any>();
    visits.list.and.returnValue(pending as Observable<any>);

    component.selectTab('archive');
    fixture.detectChanges();

    const loader = fixture.nativeElement.querySelector('[role="status"]') as HTMLElement | null;
    expect(loader).withContext('a local progress status should be rendered').not.toBeNull();
    expect(getComputedStyle(loader!).position)
      .withContext('the visits loader must never cover the fixed application shell')
      .not.toBe('fixed');
  });

  it('does not refetch a tab that was already loaded', () => {
    component.selectTab('archive');
    component.selectTab('card');
    component.selectTab('archive');

    expect(visits.list).toHaveBeenCalledTimes(1);
  });

  it('does not pin the scoring actions over standards on short viewports', () => {
    component.cardStep.set(2);
    fixture.detectChanges();

    const actions = fixture.nativeElement.querySelector('.actions') as HTMLElement;
    expect(getComputedStyle(actions).position).toBe('static');
  });

  it('keeps evidence notes collapsed until the evaluator asks to add one', () => {
    component.cardStep.set(2);
    fixture.detectChanges();

    const toggle = fixture.nativeElement.querySelector('.evidence-toggle') as HTMLButtonElement;
    expect(toggle).not.toBeNull();
    expect(toggle.getAttribute('aria-expanded')).toBe('false');
    expect(fixture.nativeElement.querySelector('.evidence-field textarea')).toBeNull();

    toggle.click();
    fixture.detectChanges();

    expect(toggle.getAttribute('aria-expanded')).toBe('true');
    expect(fixture.nativeElement.querySelector('.evidence-field textarea')).not.toBeNull();
  });

  it('shows a compact saved-evidence state while an existing note is collapsed', () => {
    const standard = component.domains()[0].standards[0];
    standard.evidenceNote = 'شاهد محفوظ';
    component.cardStep.set(2);
    fixture.detectChanges();

    const disclosure = fixture.nativeElement.querySelector('.evidence-disclosure') as HTMLElement;
    expect(disclosure.classList).toContain('has-value');
    expect(disclosure.classList).not.toContain('expanded');
    expect(disclosure.querySelector('textarea')).toBeNull();
  });

  it('does not report a false PDF failure when an external download manager takes over the request', async () => {
    visits.exportPdf.and.returnValue(throwError(() => new HttpErrorResponse({
      status: 0,
      statusText: 'Unknown Error',
      url: 'http://localhost:5264/api/v2/visits/4026/report/pdf'
    })));

    component.downloadPdf(4026);
    await fixture.whenStable();

    expect(toast.error).not.toHaveBeenCalled();
    expect(component.pdfDownloadingId()).toBeNull();
  });

  it('ignores a stale archive response when a newer AJAX request finishes first', () => {
    const older = new Subject<any>();
    const newer = new Subject<any>();
    visits.list.and.returnValues(older, newer);

    component.loadArchive();
    component.loadArchive();
    newer.next(archiveWithVisit(2));
    newer.complete();
    older.next(archiveWithVisit(1));
    older.complete();

    expect(component.archive().map(item => item.id)).toEqual([2]);
    expect(component.archiveLoading()).toBeFalse();
  });

  it('toggles the floating page control between the bottom and the top', () => {
    const container = document.createElement('div');
    Object.defineProperty(container, 'scrollHeight', { configurable: true, value: 1600 });
    Object.defineProperty(container, 'clientHeight', { configurable: true, value: 600 });
    const scrollTo = spyOn(container, 'scrollTo');
    (component as any).scrollContainer = container;

    component.atPageTop.set(true);
    component.togglePageEdge();
    let destination = scrollTo.calls.mostRecent().args[0] as unknown as ScrollToOptions;
    expect(destination.top).toBe(1000);
    expect(destination.behavior).toBe('smooth');
    expect(component.atPageTop()).toBeFalse();

    component.togglePageEdge();
    destination = scrollTo.calls.mostRecent().args[0] as unknown as ScrollToOptions;
    expect(destination.top).toBe(0);
    expect(destination.behavior).toBe('smooth');
    expect(component.atPageTop()).toBeTrue();
  });

  it('updates the floating button label and arrow for the next destination', () => {
    let button = fixture.nativeElement.querySelector('.page-jump') as HTMLButtonElement;
    expect(button.getAttribute('aria-label')).toContain('GO_TO_BOTTOM');
    expect(button.querySelector('i')?.classList).toContain('pi-arrow-down');

    component.atPageTop.set(false);
    fixture.detectChanges();

    button = fixture.nativeElement.querySelector('.page-jump') as HTMLButtonElement;
    expect(button.getAttribute('aria-label')).toContain('GO_TO_TOP');
    expect(button.querySelector('i')?.classList).toContain('pi-arrow-up');
  });
});

function archiveWithVisit(id: number): any {
  return {
    data: {
      page: {
        items: [{
          id,
          visitDate: '2026-09-25',
          classroomPeriod: 1,
          instructorName: `Teacher ${id}`,
          subject: 'Subject',
          gradeClass: 'Class',
          lessonTitle: 'Lesson',
          visitCategoryLabelAr: 'Category',
          visitSequenceLabelAr: 'Sequence',
          evaluatorName: 'Evaluator',
          evaluatorRole: 'SchoolManager',
          status: 1,
          statusLabelAr: 'Draft'
        }],
        page: 1,
        pageSize: 20,
        totalCount: 1,
        totalPages: 1
      },
      evaluators: []
    }
  };
}
