import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { Subject, of } from 'rxjs';
import { TeacherSettingsComponent } from './teacher-settings.component';
import { TeacherAvailabilityService } from '../../../core/services/teacher-availability.service';
import { TimetableSettingsService } from '../../../core/services/timetable-settings.service';
import { ToastService } from '../../../core/services/toast.service';
import { TeacherAvailability } from '../../../core/models/teacher-availability.models';
import { ApiResponse } from '../../../core/models/api-response.model';

describe('Teacher settings matrix', () => {
  let component: TeacherSettingsComponent;
  let api: jasmine.SpyObj<TeacherAvailabilityService>;
  const profile = (): TeacherAvailability => ({ instructorProfileId: 10, teacherName: 'أحمد محمد', timetableSetupProfileId: 1,
    revision: 0, bellScheduleRevisionId: 3, scheduleName: 'التوقيت', shortDisplayName: '', maximumWeeklyPeriods: 3,
    isVisiting: false, hideFromPrint: false, allocatedPeriods: 0, remainingPeriods: 3, requiresScheduleReview: false,
    slots: [
      { day: 1, bellPeriodId: 1, sequence: 1, startLocalTime: '07:00:00', endLocalTime: '07:45:00', isAvailable: true },
      { day: 1, bellPeriodId: 2, sequence: 2, startLocalTime: '08:00:00', endLocalTime: '08:45:00', isAvailable: true },
      { day: 2, bellPeriodId: 3, sequence: 1, startLocalTime: '09:00:00', endLocalTime: '09:45:00', isAvailable: true }
    ], orphanedSlots: [], violations: [] });
  const response = (data: TeacherAvailability) => ({ isSuccess: true, data } as ApiResponse<TeacherAvailability>);
  beforeEach(() => {
    api = jasmine.createSpyObj('TeacherAvailabilityService', ['list', 'get', 'save']);
    api.get.and.returnValue(of(response(profile())));
    TestBed.configureTestingModule({ imports: [TeacherSettingsComponent, NoopAnimationsModule], providers: [
      { provide: TeacherAvailabilityService, useValue: api },
      { provide: TimetableSettingsService, useValue: {} },
      { provide: ToastService, useValue: { success: jasmine.createSpy() } },
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: () => null } } } }
    ] });
    component = TestBed.runInInjectionContext(() => new TeacherSettingsComponent());
    component.setupId = 1; component.chooseTeacher(10);
  });
  it('uses only real cells and preserves per-day timing differences', () => {
    expect(component.availableCount).toBe(3);
    expect(component.cell(2, 2)).toBeUndefined();
    expect(component.dayCells(7)).toEqual([]);
    expect(component.commonTime(1)).toBe('حسب اليوم');
    expect(component.commonTime(2)).toContain('08:00');
    component.bulk(false, 1);
    expect(component.availableCount).toBe(1);
    expect(component.cell(2, 1)!.isAvailable).toBeTrue();
    expect(component.valid).toBeFalse();
    component.draft!.maximumWeeklyPeriods = 1;
    expect(component.valid).toBeTrue();
    component.bulk(false); component.draft!.maximumWeeklyPeriods = 0;
    expect(component.valid).toBeTrue();
  });
  it('retains the grid during save, prevents concurrent edits and marks the returned revision clean', () => {
    const pending = new Subject<ApiResponse<TeacherAvailability>>(); api.save.and.returnValue(pending);
    component.draft!.shortDisplayName = 'أحمد';
    const before = component.draft;
    component.save();
    expect(component.loading).toBeFalse(); expect(component.saving).toBeTrue();
    expect(component.draft).toBe(before);
    component.bulk(false); expect(component.availableCount).toBe(3);
    component.chooseTeacher(20); expect(component.teacherId).toBe(10);
    pending.next(response({ ...profile(), shortDisplayName: 'أحمد', revision: 1 })); pending.complete();
    expect(component.saving).toBeFalse(); expect(component.hasUnsavedChanges()).toBeFalse();
    expect(api.get).toHaveBeenCalledTimes(1);
  });
  it('preserves unsaved edits after a conflict and honors cancelled teacher navigation', () => {
    const pending = new Subject<ApiResponse<TeacherAvailability>>(); api.save.and.returnValue(pending);
    component.draft!.shortDisplayName = 'تعديل'; component.save(); pending.error({ status: 409 });
    expect(component.draft!.shortDisplayName).toBe('تعديل'); expect(component.hasUnsavedChanges()).toBeTrue();
    spyOn(window, 'confirm').and.returnValue(false);
    component.chooseTeacher(20); expect(component.teacherId).toBe(10);
    expect(api.get).toHaveBeenCalledTimes(1);
  });
  it('requires explicit schedule review and rejects load below assigned or above available', () => {
    component.draft!.requiresScheduleReview = true; expect(component.valid).toBeFalse();
    component.confirmScheduleReview = true; expect(component.valid).toBeTrue();
    component.draft!.allocatedPeriods = 2; component.draft!.maximumWeeklyPeriods = 1;
    expect(component.valid).toBeFalse();
    component.draft!.maximumWeeklyPeriods = 4; expect(component.valid).toBeFalse();
  });
  it('ignores an obsolete teacher response when a newer selection is loading', () => {
    const first = new Subject<ApiResponse<TeacherAvailability>>();
    const second = new Subject<ApiResponse<TeacherAvailability>>();
    api.get.and.returnValues(first, second);
    component.chooseTeacher(20); component.chooseTeacher(30);
    second.next(response({ ...profile(), instructorProfileId: 30, teacherName: 'الثالث' }));
    first.next(response({ ...profile(), instructorProfileId: 20, teacherName: 'الثاني' }));
    expect(component.draft!.instructorProfileId).toBe(30);
  });
});
