import { TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';
import { TimetableTimingsComponent } from './timetable-timings.component';
import { BellScheduleService } from '../../../core/services/bell-schedule.service';
import { TimetableSettingsService } from '../../../core/services/timetable-settings.service';
import { ToastService } from '../../../core/services/toast.service';
import { periodErrors } from '../../../core/models/bell-schedule.models';

describe('Timetable timing editor', () => {
  let component: TimetableTimingsComponent;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [
      { provide: BellScheduleService, useValue: {} },
      { provide: TimetableSettingsService, useValue: { getOverview: () => of({}) } },
      { provide: ToastService, useValue: {} }
    ] });
    component = TestBed.runInInjectionContext(() => new TimetableTimingsComponent());
    component.yearId = 1; component.newTemplate(false);
  });
  it('ignores dropdown initialization for the already selected scope and template', () => {
    const reload = spyOn(component, 'loadContext');
    component.activeDay = 2; component.edit(0, 'startLocalTime', '07:15');
    component.selectedId = 10;
    component.templates = [{ ...component.draft!, id: 10, revisionId: 1, schoolId: 1, selectedByProfileIds: [] }];
    component.changeContext(1, 1); component.choose(10);
    expect(reload).not.toHaveBeenCalled();
    expect(component.activeDay).toBe(2);
    expect(component.effective(2)[0].startLocalTime).toBe('07:15:00');
  });
  it('ignores a late template response from the previous academic scope', () => {
    const pending = new Subject<any>();
    TestBed.inject(BellScheduleService).list = () => pending;
    component.loadTemplates();
    component.changeContext(2, 1);
    pending.next({ data: [{ ...component.draft, id: 10 }] });
    expect(component.yearId).toBe(2);
    expect(component.draft).toBeNull();
    expect(component.templates).toEqual([]);
  });
  it('creates an independent override without changing defaults or another day', () => {
    component.activeDay = 2;
    component.edit(0, 'startLocalTime', '07:15');
    expect(component.draft!.days[1].usesDefaultSchedule).toBeFalse();
    expect(component.effective(2)[0].startLocalTime).toBe('07:15:00');
    expect(component.effective(3)[0].startLocalTime).toBe('07:00:00');
    expect(component.draft!.defaultPeriods[0].startLocalTime).toBe('07:00:00');
  });
  it('shows both overlapping rows and permits exact adjacent boundaries', () => {
    component.resize(2);
    component.edit(1, 'startLocalTime', '07:44');
    expect(component.errors.has(0)).toBeTrue(); expect(component.errors.has(1)).toBeTrue();
    component.edit(1, 'startLocalTime', '07:45');
    expect(component.errors.size).toBe(0);
  });
  it('requires confirmation before removing configured periods', () => {
    component.resize(2);
    spyOn(window, 'confirm').and.returnValue(false);
    component.resize(1);
    expect(component.periods.length).toBe(2);
  });
  it('highlights every overlapping row even when a long period covers several later rows', () => {
    component.resize(3);
    component.edit(0, 'endLocalTime', '10:00');
    expect([...component.errors.keys()].sort()).toEqual([0, 1, 2]);
  });
  it('previews and applies only the selected days with separate copies', () => {
    component.resize(2); component.toggleDay(2, true); component.toggleDay(3, true);
    component.previewApply();
    expect(component.comparison.length).toBe(2);
    expect(component.draft!.days[1].usesDefaultSchedule).toBeTrue();
    component.applyToDays(); component.activeDay = 2; component.edit(0, 'displayLabel', 'مخصص');
    expect(component.effective(3)[0].displayLabel).not.toBe('مخصص');
    expect(component.draft!.days[3].usesDefaultSchedule).toBeTrue();
  });
  it('allows Friday and clears periods when a day becomes a holiday', () => {
    component.setStudyDay(component.draft!.days[6], true);
    expect(component.effective(7).length).toBe(1);
    component.activeDay = 7; component.edit(0, 'displayLabel', 'الجمعة');
    spyOn(window, 'confirm').and.returnValue(true);
    component.setStudyDay(component.draft!.days[6], false);
    expect(component.effective(7)).toEqual([]); expect(component.activeDay).toBe(0);
  });
  it('rejects midnight crossing, malformed times and invalid off-tab overrides', () => {
    expect(periodErrors([{ sequence: 1, displayLabel: '', startLocalTime: '23:00', endLocalTime: '00:30' }]).size).toBe(1);
    expect(periodErrors([{ sequence: 1, displayLabel: '', startLocalTime: '', endLocalTime: '25:00' }]).size).toBe(1);
    component.draft!.name = 'اختبار'; component.activeDay = 2; component.edit(0, 'endLocalTime', '06:00');
    component.activeDay = 0; expect(component.errors.size).toBe(0); expect(component.valid).toBeFalse();
  });
});
