import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { SubjectSettingsComponent } from './subject-settings.component';
import { SubjectService } from '../../../core/services/subject.service';
import { TimetableSettingsService } from '../../../core/services/timetable-settings.service';
import { SubjectOverview } from '../../../core/models/subject.models';

describe('Subject configuration', () => {
  let component: SubjectSettingsComponent;
  let api: jasmine.SpyObj<SubjectService>;
  const overview = (): SubjectOverview => ({ subjects: [{ id: 1, name: 'رياضيات', color: '#2563eb', revision: 1 }],
    classrooms: [{ id: 10, name: '1/A', stage: 1, gradeLevel: 1 }, { id: 20, name: '2/A', stage: 1, gradeLevel: 2 }],
    rooms: [], requirements: [{ id: 100, subjectId: 1, classroomId: 10, classroomName: '1/A', revision: 3, totalWeeklyPeriods: 6,
      rules: { individualPeriodCount: 4, pairedBlockCount: 1, timePreference: 'Early', earliestPeriodSequence: 1,
        latestPreferredPeriodSequence: 2, allowedDays: [], fixedSlots: [], roomIds: [], preferredRoomId: null } }], schedule: null });
  beforeEach(() => {
    api = jasmine.createSpyObj('SubjectService', ['get', 'allocate', 'update', 'remove', 'saveSubject']);
    api.get.and.returnValue(of({ isSuccess: true, data: overview(), errors: [], message: '' }));
    api.allocate.and.returnValue(of({ isSuccess: true, data: { results: [{ classroomId: 20, classroomName: '2/A', status: 'Created', message: null }] }, errors: [], message: '' }));
    TestBed.configureTestingModule({ imports: [SubjectSettingsComponent, NoopAnimationsModule], providers: [
      { provide: SubjectService, useValue: api }, { provide: TimetableSettingsService, useValue: {} },
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: () => null } } } }
    ] });
    component = TestBed.runInInjectionContext(() => new SubjectSettingsComponent());
    component.setupId = 1; component.data = overview();
  });
  it('computes physical periods from individual periods and paired blocks', () => {
    component.openRules(1, component.data!.requirements[0]);
    expect(component.total).toBe(6);
    expect(component.hasUnsavedChanges()).toBeFalse();
    component.rules.pairedBlockCount = 2;
    expect(component.total).toBe(8); expect(component.hasUnsavedChanges()).toBeTrue();
    expect(component.data!.requirements[0].rules.pairedBlockCount).toBe(1);
  });
  it('does not send bulk changes until the user chooses how to handle existing classes', () => {
    component.openRules(1); component.selectedClasses = [10, 20]; component.prepareSave();
    expect(component.overwriteOpen).toBeTrue(); expect(api.allocate).not.toHaveBeenCalled();
    component.save(false);
    const request = api.allocate.calls.mostRecent().args[1];
    expect(request.overwriteExisting).toBeFalse();
    expect(request.classes).toEqual([{ classroomId: 10, revision: 3 }, { classroomId: 20, revision: 0 }]);
  });
  it('sends explicit overwrite and preserves the draft when the server reports a conflict', () => {
    api.allocate.and.returnValue(throwError(() => ({ status: 409 })));
    component.openRules(1); component.selectedClasses = [10]; component.rules.individualPeriodCount = 7;
    component.prepareSave(); component.save(true);
    expect(api.allocate.calls.mostRecent().args[1].overwriteExisting).toBeTrue();
    expect(component.rules.individualPeriodCount).toBe(7); expect(component.editorOpen).toBeTrue();
    expect(component.saving).toBeFalse(); expect(component.error).not.toBe('');
  });
  it('select all respects grade filtering and individual editing exposes only that class rules', () => {
    component.openRules(1); component.grade = 2; component.selectAll(); expect(component.selectedClasses).toEqual([20]);
    component.openRules(1, component.data!.requirements[0]);
    expect(component.selectedClasses).toEqual([10]); expect(component.rules.timePreference).toBe('Early');
  });
  it('rejects fractional counts and zero total before saving', () => {
    component.openRules(1); component.selectedClasses = [20];
    component.rules.individualPeriodCount = 0; expect(component.valid).toBeFalse();
    component.rules.pairedBlockCount = 1.5; expect(component.valid).toBeFalse();
    component.rules.pairedBlockCount = 1; expect(component.valid).toBeTrue();
  });
});
