import { TestBed } from '@angular/core/testing';
import { TimetableSettingsService } from '../../core/services/timetable-settings.service';
import { TimetableService } from '../../core/services/timetable.service';
import { ToastService } from '../../core/services/toast.service';
import { SchoolTimetableComponent } from './school-timetable.component';

describe('School timetable empty grid', () => {
  let component: SchoolTimetableComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [
      { provide: TimetableService, useValue: {} },
      { provide: TimetableSettingsService, useValue: {} },
      { provide: ToastService, useValue: {} }
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
});
