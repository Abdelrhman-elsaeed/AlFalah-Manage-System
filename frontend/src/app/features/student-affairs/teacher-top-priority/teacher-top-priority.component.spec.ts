import { HttpErrorResponse } from '@angular/common/http';
import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { Subject, of, throwError } from 'rxjs';
import { ApiResponse } from '../../../core/models/api-response.model';
import { TeacherTopPriorityDto } from '../../../core/models/student-affairs-dashboard.models';
import { AuthService } from '../../../core/services/auth.service';
import { StudentAffairsDashboardService } from '../../../core/services/student-affairs-dashboard.service';
import { TeacherTopPriorityComponent } from './teacher-top-priority.component';

describe('TeacherTopPriorityComponent', () => {
  let api: jasmine.SpyObj<StudentAffairsDashboardService>;
  let auth: jasmine.SpyObj<AuthService>;

  beforeEach(async () => {
    api = jasmine.createSpyObj<StudentAffairsDashboardService>('StudentAffairsDashboardService', [
      'getTeacherTopPriority',
      'createBehaviorIncident',
      'createAcademicConcern',
      'createSessionDelay',
      'createRecognition',
      'acknowledgeGatePass',
      'acknowledgeEntryPermit'
    ]);
    api.getTeacherTopPriority.and.returnValue(of(success(priority())));
    auth = jasmine.createSpyObj<AuthService>('AuthService', ['hasRole', 'hasPermission']);
    auth.hasRole.and.callFake(role => role === 'Instructor');
    auth.hasPermission.and.returnValue(true);

    await TestBed.configureTestingModule({
      imports: [TeacherTopPriorityComponent, NoopAnimationsModule],
      providers: [
        MessageService,
        { provide: StudentAffairsDashboardService, useValue: api },
        { provide: AuthService, useValue: auth }
      ]
    }).compileComponents();
  });

  it('renders only the canonical current roster with no classroom fallback or referral action', () => {
    const fixture = TestBed.createComponent(TeacherTopPriorityComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('#classroomSelect')).toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain('إحالة طالب');
    expect(fixture.nativeElement.textContent).toContain('Student One');
    expect(fixture.nativeElement.textContent).toContain('Mathematics');
    expect(fixture.nativeElement.querySelector('[aria-label="تسجيل ملاحظة سلوكية"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[aria-label="تسجيل ملاحظة أكاديمية"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[aria-label="تسجيل تأخر عن الحصة"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[aria-label="تسجيل إشادة وتميز"]')).not.toBeNull();
  });

  it('uses the current roster student and timetable entry and blocks double submit', () => {
    const pending = new Subject<ApiResponse<any>>();
    api.createBehaviorIncident.and.returnValue(pending);
    const component = TestBed.createComponent(TeacherTopPriorityComponent).componentInstance;
    component.ngOnInit();
    component.openAction('behavior', component.context()!.roster[0]);
    component.behaviorForm.patchValue({ category: 'Conduct', description: 'Incident' });

    component.submit();
    component.submit();

    expect(api.createBehaviorIncident).toHaveBeenCalledTimes(1);
    expect(api.createBehaviorIncident.calls.mostRecent().args[0].studentId).toBe(11);
    expect(api.createBehaviorIncident.calls.mostRecent().args[0].schoolTimetableEntryId).toBe(42);
    expect(component.submitting()).toBeTrue();
  });

  it('keeps form input but prevents submission after the server context changes', () => {
    const component = TestBed.createComponent(TeacherTopPriorityComponent).componentInstance;
    component.ngOnInit();
    component.openAction('academic', component.context()!.roster[0]);
    component.academicForm.patchValue({ category: 'Progress', description: 'Keep this draft' });
    component.topPriority.set(priority(99));

    component.submit();

    expect(api.createAcademicConcern).not.toHaveBeenCalled();
    expect(component.academicForm.controls.description.value).toBe('Keep this draft');
    expect(component.submissionErrors()[0]).toContain('تغيّر سياق الحصة');
  });

  it('reloads after a 409 acknowledgement conflict without reporting success', () => {
    api.acknowledgeEntryPermit.and.returnValue(throwError(() => new HttpErrorResponse({ status: 409 })));
    const component = TestBed.createComponent(TeacherTopPriorityComponent).componentInstance;
    component.ngOnInit();

    component.acknowledgeEntryPermit(8, 'AQ==');

    expect(api.acknowledgeEntryPermit).toHaveBeenCalledOnceWith(8, 'AQ==');
    expect(api.getTeacherTopPriority).toHaveBeenCalledTimes(2);
    expect(component.acknowledgingId()).toBeNull();
  });

  it('shows the real no-lesson resolution and exposes no roster actions', () => {
    api.getTeacherTopPriority.and.returnValue(of(success(priority(null, 'Break', 'Configured school break'))));
    const fixture = TestBed.createComponent(TeacherTopPriorityComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Configured school break');
    expect(fixture.nativeElement.querySelector('.student-row')).toBeNull();
  });

  it('keeps loaded context visible but disables actions after a refresh network failure', () => {
    const component = TestBed.createComponent(TeacherTopPriorityComponent).componentInstance;
    component.ngOnInit();
    api.getTeacherTopPriority.and.returnValue(throwError(() => new HttpErrorResponse({ status: 0 })));

    component.load(true);

    expect(component.topPriority()).not.toBeNull();
    expect(component.errorMessage()).toContain('تعذر تحميل الحصة الحالية');
    expect(component.isActionAllowed('behavior')).toBeFalse();
  });

  it('renders a denied state for an initial 403', () => {
    api.getTeacherTopPriority.and.returnValue(throwError(() => new HttpErrorResponse({ status: 403 })));
    const fixture = TestBed.createComponent(TeacherTopPriorityComponent);

    fixture.detectChanges();

    expect(fixture.componentInstance.denied()).toBeTrue();
    expect(fixture.nativeElement.textContent).toContain('الوصول غير متاح');
  });

  it('refreshes once at the lesson boundary and refreshes on window focus', fakeAsync(() => {
    const fixture = TestBed.createComponent(TeacherTopPriorityComponent);
    fixture.detectChanges();

    window.dispatchEvent(new Event('focus'));
    expect(api.getTeacherTopPriority).toHaveBeenCalledTimes(2);

    tick(61_100);
    expect(api.getTeacherTopPriority).toHaveBeenCalledTimes(3);
    fixture.destroy();
  }));
});

function priority(
  entryId: number | null = 42,
  resolutionKind = 'ActiveLesson',
  resolutionReason = 'Active lesson'
): TeacherTopPriorityDto {
  const now = new Date();
  const endsAt = new Date(now.getTime() + 60_000).toISOString();
  const roster = entryId === null ? [] : [{
    id: 11,
    studentNumber: 'ST-11',
    displayName: 'Student One',
    classroomId: 9,
    classLabel: '1/A',
    isActive: true,
    photoUrl: null
  }];
  return {
    context: {
      teacher: { userId: 'teacher', displayName: 'Teacher', roleSnapshot: 'Instructor' },
      resolutionKind,
      resolutionReason,
      schoolLocalTime: now.toISOString(),
      schoolTimeZone: 'Africa/Cairo',
      timetableRevision: 3,
      currentPeriod: entryId === null ? null : {
        timetableId: 10,
        bellScheduleRevisionId: 5,
        timetableEntryId: entryId,
        period: 2,
        startsAt: new Date(now.getTime() - 60_000).toISOString(),
        endsAt,
        subject: 'Mathematics',
        classroom: { id: 9, label: '1/A', stage: 'Primary', gradeLevel: 1, section: 'A' },
        originalInstructor: { userId: 'teacher', displayName: 'Teacher', roleSnapshot: 'Instructor' },
        effectiveInstructor: { userId: 'teacher', displayName: 'Teacher', roleSnapshot: 'Instructor' },
        substitutionId: null
      },
      roster,
      permittedQuickActions: ['Behavior.Create', 'AcademicConcern.Create', 'SessionDelay.Create', 'Recognition.Create']
    },
    pendingGatePassAcknowledgements: 0,
    pendingEntryPermitAcknowledgements: 0,
    gatePassAcknowledgements: [],
    entryPermitAcknowledgements: [],
    alerts: []
  };
}

function success<T>(data: T): ApiResponse<T> {
  return { isSuccess: true, message: '', data, errors: [] };
}
