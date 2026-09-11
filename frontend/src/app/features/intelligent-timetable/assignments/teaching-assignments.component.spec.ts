import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { TeachingAssignmentsComponent } from './teaching-assignments.component';
import { TeachingAssignmentService } from '../../../core/services/teaching-assignment.service';
import { TimetableSettingsService } from '../../../core/services/timetable-settings.service';
import { TeachingOverview } from '../../../core/models/teaching-assignment.models';

describe('Teaching assignments', () => {
  let component: TeachingAssignmentsComponent;
  let api: jasmine.SpyObj<TeachingAssignmentService>;
  const overview = (): TeachingOverview => ({ revision: 3, subjects: [{ id: 1, name: 'رياضيات', color: '#2563eb', revision: 1 }],
    classrooms: [{ id: 10, name: '1/A', stage: 1, gradeLevel: 1 }, { id: 20, name: '1/B', stage: 1, gradeLevel: 1 }],
    requirements: [10, 20].map(id => ({ id, subjectId: 1, classroomId: id, classroomName: `${id}`, revision: 1, totalWeeklyPeriods: 4,
      rules: { individualPeriodCount: 2, pairedBlockCount: 1, timePreference: 'None', earliestPeriodSequence: null,
        latestPreferredPeriodSequence: null, allowedDays: [], fixedSlots: [], roomIds: [], preferredRoomId: null } })),
    assignments: [], teachers: [1, 2].map(id => ({ id, instructorProfileId: id, name: `معلم ${id}`, specialization: 'رياضيات',
      isActive: true, allocatedPeriods: 0, maximumWeeklyPeriods: 6, remainingPeriods: 6, subjectCount: 0, classroomCount: 0, isVisiting: false, warnings: [] })), warnings: [] });
  beforeEach(() => {
    api = jasmine.createSpyObj('TeachingAssignmentService', ['get', 'save', 'teachers']);
    api.teachers.and.returnValue(of({ isSuccess: true, data: { items: overview().teachers, totalCount: 2 }, errors: [], message: '' }));
    TestBed.configureTestingModule({ providers: [{ provide: TeachingAssignmentService, useValue: api },
      { provide: TimetableSettingsService, useValue: {} }, { provide: ActivatedRoute, useValue: {} }] });
    component = TestBed.runInInjectionContext(() => new TeachingAssignmentsComponent()); component.setupId = 1; component.data = overview();
  });
  it('preserves selected teachers when searching and paging the server table', () => {
    component.open(component.data!.requirements[0]); component.toggle(component.data!.teachers[0]);
    component.teacherSearch = 'معلم'; component.searchTeachers(); component.lazyTeachers({ first: 10, rows: 10 });
    expect(component.selected(1)).toBeTrue(); expect(component.members.length).toBe(1);
    expect(api.teachers.calls.mostRecent().args[1]['page']).toBe(2);
  });
  it('includes all unsaved cells in capacity even when the matrix is filtered', () => {
    component.open(component.data!.requirements[0]); component.toggle(component.data!.teachers[0]); component.confirmCell();
    component.classroomId = 20; component.open(component.data!.requirements[1]); component.toggle(component.data!.teachers[0]);
    expect(component.workload(1, true).periods).toBe(8); expect(component.dialogError).toContain('تجاوز');
    component.confirmCell(); expect(component.drafts.size).toBe(1);
  });
  it('requires an explicit multi-teacher mode and keeps each pair with one teacher', () => {
    component.open(component.data!.requirements[0]); component.toggle(component.data!.teachers[0]); component.toggle(component.data!.teachers[1]);
    expect(component.mode).toBeNull(); expect(component.dialogError).not.toBe('');
    component.mode = 'SplitQuota'; component.members = [
      { teacherTimetableProfileId: 1, allocatedPeriodCount: 1, allocatedPairedBlockCount: 1 },
      { teacherTimetableProfileId: 2, allocatedPeriodCount: 3, allocatedPairedBlockCount: 0 }];
    expect(component.dialogError).not.toBe('');
    component.members[0].allocatedPeriodCount = 3; component.members[1].allocatedPeriodCount = 1;
    expect(component.dialogError).toBe(''); component.confirmCell(); expect(component.drafts.size).toBe(1);
  });
  it('co-teaching counts the whole subject quota against each teacher', () => {
    component.open(component.data!.requirements[0]); component.toggle(component.data!.teachers[0]); component.toggle(component.data!.teachers[1]);
    component.mode = 'CoTeaching'; component.modeChanged(); expect(component.dialogError).toBe('');
    component.confirmCell(); expect(component.workload(1).periods).toBe(4); expect(component.workload(2).periods).toBe(4);
  });
  it('keeps the draft after an HTTP conflict and sends the original setup revision', () => {
    api.save.and.returnValue(throwError(() => ({ status: 409 })));
    component.open(component.data!.requirements[0]); component.toggle(component.data!.teachers[0]); component.confirmCell(); component.save();
    expect(api.save.calls.mostRecent().args[1]).toBe(3); expect(component.drafts.size).toBe(1);
    expect(component.saving).toBeFalse(); expect(component.error).not.toBe(''); expect(component.hasUnsavedChanges()).toBeTrue();
  });
  it('canceling a dialog does not mutate the persisted cell and unassigning releases draft load', () => {
    component.open(component.data!.requirements[0]); component.toggle(component.data!.teachers[0]);
    spyOn(window, 'confirm').and.returnValue(true); component.closeDialog(); expect(component.cell(10).members.length).toBe(0);
    component.open(component.data!.requirements[0]); component.toggle(component.data!.teachers[0]); component.confirmCell();
    component.clear(component.data!.requirements[0]); expect(component.workload(1).periods).toBe(0);
  });
});
