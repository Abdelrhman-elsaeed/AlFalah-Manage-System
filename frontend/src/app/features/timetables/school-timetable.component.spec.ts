import { TestBed } from '@angular/core/testing';
import { TimetableSettingsService } from '../../core/services/timetable-settings.service';
import { TimetableService } from '../../core/services/timetable.service';
import { ToastService } from '../../core/services/toast.service';
import { BellSchedule } from '../../core/models/bell-schedule.models';
import { SchoolTimetable } from '../../core/models/timetable.models';
import { TimetableSubstitutionService } from '../../core/services/timetable-substitution.service';
import { SchoolTimetableComponent } from './school-timetable.component';
import { InlineSwapSessionStore } from './inline-swap/inline-swap-session.store';
import { NEVER, of } from 'rxjs';

describe('School timetable empty grid', () => {
  let component: SchoolTimetableComponent;
  let timetableApi: jasmine.SpyObj<TimetableService>;
  let substitutionApi: jasmine.SpyObj<TimetableSubstitutionService>;

  beforeEach(() => {
    timetableApi = jasmine.createSpyObj<TimetableService>('TimetableService', ['getCurrent']);
    substitutionApi = jasmine.createSpyObj<TimetableSubstitutionService>('TimetableSubstitutionService', ['inlineCandidates', 'executeInline']);
    substitutionApi.inlineCandidates.and.returnValue(NEVER);
    TestBed.configureTestingModule({ providers: [
      { provide: TimetableService, useValue: timetableApi },
      { provide: TimetableSettingsService, useValue: {} },
      { provide: ToastService, useValue: {} },
      { provide: TimetableSubstitutionService, useValue: substitutionApi },
      InlineSwapSessionStore
    ] });
    component = TestBed.runInInjectionContext(() => new SchoolTimetableComponent());
    component.catalog.set({
      schoolId: 1,
      schoolName: 'مدرسة الفلاح',
      academicYears: [{ id: 1, code: '2026-2027', nameAr: '2026-2027', isActive: true }],
      semesters: [{ value: 1, labelAr: 'الفصل الأول' }],
      days: [
        { value: 1, labelAr: 'السبت' },
        { value: 2, labelAr: 'الأحد' },
        { value: 3, labelAr: 'الاثنين' },
        { value: 4, labelAr: 'الثلاثاء' },
        { value: 5, labelAr: 'الأربعاء' },
        { value: 6, labelAr: 'الخميس' },
        { value: 7, labelAr: 'الجمعة' }
      ],
      periodCount: 0,
      teachers: [],
      moderators: [],
      capabilities: { canManage: true, canDelegate: false, canViewVersions: true }
    });
    component.selectedYearId.set(1);
  });

  it('shows weekday headers and eight placeholder periods before a table exists', () => {
    expect(component.studyDays().map(day => day.value)).toEqual([2, 3, 4, 5, 6]);
    expect(component.intervalsFor(2)).toHaveSize(8);
    expect(component.intervalsFor(2)[0]).toEqual(jasmine.objectContaining({
      kind: 'Lesson',
      name: 'الحصة 1',
      periodSequence: 1
    }));
    expect(component.gridEditable()).toBeFalse();
  });

  it('does not open the cell editor while the grid is only a preview', () => {
    component.openCell({
      instructorProfileId: 4,
      userId: 'teacher-4',
      fullName: 'معلم تجريبي',
      employeeNumber: null,
      subject: null,
      classes: [],
      isCurrentUser: false
    }, 2, 1);

    expect(component.cellDialogVisible()).toBeFalse();
    expect(component.selectedCell).toBeNull();
  });

  it('toggles the timetable fullscreen workspace without changing editability', () => {
    expect(component.gridFullscreen()).toBeFalse();
    expect(component.gridEditable()).toBeFalse();

    component.toggleGridFullscreen();

    expect(component.gridFullscreen()).toBeTrue();
    expect(component.gridEditable()).toBeFalse();

    component.closeGridFullscreen();

    expect(component.gridFullscreen()).toBeFalse();
  });

  it('keeps generated interval objects stable across cell clicks', () => {
    const schedule = bellSchedule();
    component.contextBellSchedule.set(schedule);

    const firstRead = component.intervalsFor(2);
    const secondRead = component.intervalsFor(2);

    expect(secondRead).toBe(firstRead);
    expect(secondRead[0]).toBe(firstRead[0]);
  });

  it('preserves normalized generator metadata when editing a generated lesson', () => {
    component.catalog.update(catalog => ({
      ...catalog!,
      teachers: [{
        instructorProfileId: 4,
        userId: 'teacher-4',
        fullName: 'معلم تجريبي',
        employeeNumber: null,
        subject: 'الرياضيات',
        classes: ['الأول - أ'],
        isCurrentUser: false
      }]
    }));
    const timetable = generatedTimetable();
    (component as any).applyTimetable(timetable);

    component.openCell(component.catalog()!.teachers[0], 2, 1);
    component.draftSubject = 'رياضيات متقدمة';
    component.saveCell();

    expect(component.getEntry(4, 2, 1)).toEqual(jasmine.objectContaining({
      classroomId: 21,
      subjectId: 31,
      classSubjectRequirementId: 41,
      roomId: 51,
      roomName: 'معمل الرياضيات',
      subjectColor: '#2563eb'
    }));
  });

  it('opens the inline swap session only for a saved lesson', () => {
    component.catalog.update(catalog => ({
      ...catalog!,
      teachers: [{ instructorProfileId: 4, userId: 'teacher-4', fullName: 'معلم تجريبي', employeeNumber: null,
        subject: 'الرياضيات', classes: ['الأول - أ'], isCurrentUser: false }]
    }));
    (component as any).applyTimetable(generatedTimetable());

    component.openCell(component.catalog()!.teachers[0], 2, 1);
    component.selectCellDialogTab('swap');

    expect(component.inlineSwap.phase()).toBe('choosing');
    expect(component.inlineSwap.source()?.entry.id).toBe(100);
  });

  it('preserves WholeTimetable when stale candidates are analysed again', () => {
    component.catalog.update(catalog => ({
      ...catalog!,
      teachers: [{ instructorProfileId: 4, userId: 'teacher-4', fullName: 'معلم تجريبي', employeeNumber: null,
        subject: 'الرياضيات', classes: ['الأول - أ'], isCurrentUser: false }]
    }));
    const timetable = generatedTimetable();
    (component as any).applyTimetable(timetable);
    component.inlineSwap.begin({ entry: timetable.entries[0], teacherName: 'معلم تجريبي', dayLabel: 'الأحد', periodLabel: 'الأولى' });
    component.inlineSwap.scope.set('WholeTimetable');
    timetableApi.getCurrent.and.returnValue(of({ isSuccess: true, message: '', errors: [], data: timetable }));

    component.reanalyseInlineSwap();

    expect(substitutionApi.inlineCandidates).toHaveBeenCalledOnceWith(
      timetable.id, jasmine.any(String), timetable.entries[0].id!, 'WholeTimetable');
  });

  it('does not cancel an execution that may already be committed by the server', () => {
    component.inlineSwap.phase.set('executing');
    component.gridFullscreen.set(true);
    const confirm = spyOn(window, 'confirm');

    component.closeGridFullscreenOnEscape();
    component.cancelInlineSwap();

    expect(confirm).not.toHaveBeenCalled();
    expect(component.inlineSwap.phase()).toBe('executing');
    expect(component.gridFullscreen()).toBeTrue();
  });

  it('executes an accepted green alternative automatically', async () => {
    const timetable = generatedTimetable();
    (component as any).applyTimetable(timetable);
    component.inlineSwap.begin({
      entry: timetable.entries[0],
      teacherName: 'معلم تجريبي',
      dayLabel: 'الأحد',
      periodLabel: 'الأولى'
    });
    component.inlineSwap.result.set({
      timetableId: timetable.id,
      revision: timetable.revision,
      date: '2026-09-20',
      sourceEntryId: 100,
      sourceEntryIds: [100],
      scope: 'SameDay',
      expiresAt: '2099-01-01T00:00:00Z',
      canOverride: true,
      cells: [{
        anchorEntryId: 200,
        entryIds: [200],
        teacherId: 5,
        day: 2,
        period: 2,
        color: 'Red',
        directProposalId: 'direct',
        alternativeProposalIds: ['safe-three-way'],
        reasonSummary: 'تعارض'
      }],
      proposals: [
        { id: 'direct', kind: 'DirectSwap', color: 'Red', label: 'تبديل مباشر', errors: ['تعارض'], warnings: [], preview: [] },
        { id: 'safe-three-way', kind: 'ThreeWaySwap', color: 'Green', label: 'تبديل ثلاثي آمن', errors: [], warnings: [], preview: [] }
      ]
    });
    component.inlineSwap.selectedCell.set(component.inlineSwap.result()!.cells[0]);
    component.inlineSwap.selectedProposalId.set('direct');
    component.inlineSwap.phase.set('reviewing');
    substitutionApi.executeInline.and.returnValue(of({
      isSuccess: true,
      message: '',
      errors: [],
      data: {
        id: 1,
        kind: 'ThreeWaySwap',
        date: '2026-09-20',
        beforeRevision: 3,
        afterRevision: 4,
        requestedBy: 'user',
        approvedBy: 'user',
        reason: null,
        confirmedAt: '2026-09-17T00:00:00Z'
      }
    }));
    timetableApi.getCurrent.and.returnValue(NEVER);

    await component.acceptInlineSwapSuggestion('safe-three-way');

    expect(substitutionApi.executeInline).toHaveBeenCalledOnceWith(
      jasmine.objectContaining({ timetableId: timetable.id }),
      'safe-three-way',
      jasmine.any(String),
      null);
    expect(component.inlineSwap.phase()).toBe('idle');
  });

  function bellSchedule(): BellSchedule {
    return {
      id: 1,
      revisionId: 1,
      schoolId: 1,
      academicYearId: 1,
      semester: 1,
      name: 'توقيت الاختبار',
      revision: 1,
      schoolTimeZoneId: 'Africa/Cairo',
      defaultPeriods: [{ sequence: 1, displayLabel: 'الأولى', startLocalTime: '07:00:00', endLocalTime: '07:45:00' }],
      defaultBreaks: [],
      days: [{ day: 2, isStudyDay: true, usesDefaultSchedule: true, usesDefaultBreaks: true, periods: [], breaks: [] }],
      selectedByProfileIds: [1]
    };
  }

  function generatedTimetable(): SchoolTimetable {
    return {
      id: 10,
      schoolId: 1,
      academicYearId: 1,
      academicYearName: '2026-2027',
      semester: 1,
      semesterLabelAr: 'الفصل الأول',
      title: 'الجدول المولد',
      isPublished: true,
      publishedAt: '2026-09-16T00:00:00Z',
      revision: 3,
      updatedAt: '2026-09-16T00:00:00Z',
      bellSchedule: bellSchedule(),
      timingsRequireRevalidation: false,
      capabilities: { canManage: true, canDelegate: false, canViewVersions: true },
      teacherSummaries: [{ instructorProfileId: 4, lessonCount: 1, standbyCount: 0 }],
      entries: [{
        id: 100,
        instructorProfileId: 4,
        day: 2,
        period: 1,
        entryType: 1,
        classLabel: 'الأول - أ',
        subject: 'الرياضيات',
        classroomId: 21,
        subjectId: 31,
        classSubjectRequirementId: 41,
        roomId: 51,
        roomName: 'معمل الرياضيات',
        subjectColor: '#2563eb'
      }]
    };
  }
});
