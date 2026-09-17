import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { TimetableSettingsService } from '../../../core/services/timetable-settings.service';
import { ToastService } from '../../../core/services/toast.service';
import { TimetableSettingsComponent } from './timetable-settings.component';

describe('Timetable settings academic years', () => {
  let component: TimetableSettingsComponent;
  let settingsService: {
    getOverview: jasmine.Spy;
    createAcademicYear: jasmine.Spy;
  };

  beforeEach(() => {
    settingsService = {
      getOverview: jasmine.createSpy('getOverview').and.returnValue(of({
        isSuccess: true,
        data: overview()
      })),
      createAcademicYear: jasmine.createSpy('createAcademicYear').and.returnValue(of({
        isSuccess: true,
        message: 'تمت الإضافة',
        errors: [],
        data: { id: 7, code: '2026-2027', nameAr: 'العام الدراسي 2026-2027', isActive: true }
      }))
    };
    TestBed.configureTestingModule({ providers: [
      { provide: TimetableSettingsService, useValue: settingsService },
      { provide: ToastService, useValue: { success: jasmine.createSpy('success'), error: jasmine.createSpy('error') } },
      { provide: Router, useValue: { navigateByUrl: jasmine.createSpy('navigateByUrl') } }
    ] });
    component = TestBed.runInInjectionContext(() => new TimetableSettingsComponent());
    component.overview.set(overview());
  });

  it('opens the academic-year dialog with a complete editable date range', () => {
    component.openAcademicYearDialog();

    expect(component.academicYearDialogOpen()).toBeTrue();
    expect(component.academicYearForm.valid).toBeTrue();
    expect(component.academicYearForm.controls.code.value).toMatch(/^\d{4}-\d{4}$/);
    expect(component.academicYearForm.controls.firstSemesterStartsOn.value).toBeTruthy();
    expect(component.academicYearForm.controls.secondSemesterEndsOn.value).toBeTruthy();
  });

  it('creates the year and reloads the selected academic scope', () => {
    component.openAcademicYearDialog();
    component.academicYearForm.patchValue({
      code: '2026-2027',
      nameAr: 'العام الدراسي 2026-2027',
      startsOn: '2026-08-01',
      endsOn: '2027-07-31',
      firstSemesterStartsOn: '2026-08-01',
      firstSemesterEndsOn: '2026-12-31',
      secondSemesterStartsOn: '2027-01-01',
      secondSemesterEndsOn: '2027-07-31',
      activeSemester: 2
    });

    component.saveAcademicYear();

    expect(settingsService.createAcademicYear).toHaveBeenCalledWith(jasmine.objectContaining({
      code: '2026-2027',
      activeSemester: 2
    }));
    expect(component.academicYearDialogOpen()).toBeFalse();
    expect(settingsService.getOverview).toHaveBeenCalledWith(7, 2, undefined);
  });

  it('opens the timetable generation step when every prerequisite is complete', () => {
    const ready = overview();
    ready.hardPrerequisitesValid = true;
    ready.completionPercent = 100;
    ready.selectedProfile = { id: 15 };
    ready.steps = [
      { key: 'study-days', status: 'complete', route: '/intelligent-timetable/timings' },
      { key: 'teachers', status: 'complete', route: '/intelligent-timetable/teachers' },
      { key: 'classrooms', status: 'complete', route: '/student-affairs/classrooms' },
      { key: 'subjects', status: 'complete', route: '/intelligent-timetable/subjects' },
      { key: 'assignments', status: 'complete', route: '/intelligent-timetable/assignments' },
      { key: 'subject-rules', status: 'complete', route: '/intelligent-timetable/subjects' },
      { key: 'timetable', status: 'complete', route: '/timetable' }
    ];
    component.overview.set(ready);

    component.continueSetup();

    expect(TestBed.inject(Router).navigateByUrl).toHaveBeenCalledWith('/timetable');
  });

  it('opens the generated timetable from the ready-state action', () => {
    const ready = overview();
    ready.hardPrerequisitesValid = true;
    ready.steps = [{ key: 'timetable', status: 'complete', route: '/timetable' }];
    component.overview.set(ready);

    expect(component.generationActionLabel()).toBe('عرض الجدول المولّد');
    component.openGeneration();

    expect(TestBed.inject(Router).navigateByUrl).toHaveBeenCalledWith('/timetable');
  });

  function overview(): any {
    return {
      schoolId: 1,
      schoolName: 'مدرسة الفلاح',
      academicYears: [],
      profiles: [],
      selectedProfile: null,
      selectedAcademicYearId: 0,
      selectedSemester: 1,
      selectedSemesterLabelAr: 'الفصل الدراسي الأول',
      canManage: true,
      completionPercent: 0,
      hardPrerequisitesValid: false,
      counts: {
        activeClassrooms: 0,
        activeStudents: 0,
        classroomsMissingLocation: 0,
        activeTeachers: 0,
        availableSubjects: 0
      },
      steps: [],
      warnings: []
    };
  }
});
