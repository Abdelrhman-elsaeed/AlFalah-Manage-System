import { TimetableBreaksEditorComponent } from './timetable-breaks-editor.component';
import { breakErrors, effectiveBreaks, scheduleBreakIssues } from '../../../core/models/schedule-break.models';

describe('Timetable breaks', () => {
  let editor: TimetableBreaksEditorComponent;
  beforeEach(() => {
    editor = new TimetableBreaksEditorComponent();
    editor.dayNames = ['كل الأيام', 'السبت', 'الأحد', 'الاثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة'];
    editor.draft = { academicYearId: 1, semester: 1, name: 'اختبار', revision: 1, schoolTimeZoneId: 'Africa/Cairo',
      defaultPeriods: [
        { sequence: 1, displayLabel: '', startLocalTime: '07:00:00', endLocalTime: '07:45:00' },
        { sequence: 2, displayLabel: '', startLocalTime: '08:00:00', endLocalTime: '08:45:00' }
      ], defaultBreaks: [{ name: 'فسحة', category: 'Recess', startLocalTime: '07:45:00', endLocalTime: '08:00:00' }],
      days: Array.from({ length: 7 }, (_, i) => ({ day: i + 1, isStudyDay: i < 6, usesDefaultSchedule: true, periods: [], usesDefaultBreaks: true, breaks: [] })) };
  });
  it('permits exact boundary adjacency and highlights both breaks and lessons on overlap', () => {
    expect(editor.validation.rows.size).toBe(0);
    editor.edit(0, 'startLocalTime', '07:44');
    expect(editor.validation.rows.has(0)).toBeTrue(); expect(editor.validation.lessons.has(0)).toBeTrue();
    editor.edit(0, 'startLocalTime', '07:45');
    expect(editor.validation.rows.size).toBe(0);
  });
  it('allows before, after and intentional gaps, with equivalent time precision at boundaries', () => {
    const row = editor.rows[0];
    for (const [start, end] of [['06:30', '07:00'], ['08:45', '09:00'], ['10:00', '10:15']]) {
      expect(breakErrors([{ ...row, startLocalTime: start, endLocalTime: end }], editor.periods).rows.size).toBe(0);
    }
    editor.edit(0, 'endLocalTime', '07:45'); expect(editor.validation.rows.size).toBe(1);
    editor.edit(0, 'endLocalTime', '00:00'); expect(editor.validation.rows.size).toBe(1);
  });
  it('reports both breaks, including containment and identical times', () => {
    editor.draft.defaultBreaks!.push({ ...editor.rows[0] });
    expect([...editor.validation.rows.keys()].sort()).toEqual([0, 1]);
  });
  it('uses after-period only to fill start once, with no period binding in the request', () => {
    editor.afterPeriod(0, 1);
    expect(editor.rows[0].startLocalTime).toBe('07:45:00');
    editor.draft.defaultPeriods[0].endLocalTime = '07:30:00';
    expect(editor.rows[0].startLocalTime).toBe('07:45:00');
    expect(Object.keys(editor.rows[0]).sort()).toEqual(['category', 'endLocalTime', 'name', 'startLocalTime']);
  });
  it('isolates day break variations while continuing to inherit periods', () => {
    editor.activeDay = 2; editor.edit(0, 'name', 'فسحة الأحد');
    expect(editor.draft.days[1].usesDefaultBreaks).toBeFalse();
    expect(editor.draft.days[1].usesDefaultSchedule).toBeTrue();
    expect(editor.rows[0].name).toBe('فسحة الأحد');
    expect(effectiveBreaks(editor.draft, 3)[0].name).toBe('فسحة');
    editor.activeDay = 0; editor.edit(0, 'endLocalTime', '07:55');
    expect(effectiveBreaks(editor.draft, 2)[0].endLocalTime).toBe('08:00:00');
  });
  it('rejects multi-day apply atomically if one target day has a lesson conflict', () => {
    editor.draft.days[2].usesDefaultSchedule = false;
    editor.draft.days[2].periods = [{ sequence: 1, displayLabel: '', startLocalTime: '07:40:00', endLocalTime: '08:00:00' }];
    editor.selectedDays = new Set([2, 3]);
    const before = JSON.stringify(editor.draft);
    editor.applyToDays();
    expect(JSON.stringify(editor.draft)).toBe(before);
    expect(editor.error).toContain('الاثنين');
    expect(scheduleBreakIssues(editor.draft).some(x => x.day === 3)).toBeTrue();
  });
  it('copies selected days independently and requires confirmation to remove', () => {
    const confirm = spyOn(window, 'confirm').and.returnValue(true);
    editor.selectedDays = new Set([2, 3]); editor.applyToDays();
    editor.activeDay = 2; editor.edit(0, 'name', 'خاص');
    expect(effectiveBreaks(editor.draft, 3)[0].name).toBe('فسحة');
    confirm.and.returnValue(false); editor.remove(0); expect(editor.rows.length).toBe(1);
    confirm.and.returnValue(true); editor.remove(0); expect(editor.rows.length).toBe(0);
    expect(effectiveBreaks(editor.draft, 3).length).toBe(1);
  });
  it('respects read-only access for helper, add, edit and delete actions', () => {
    editor.disabled = true;
    const before = JSON.stringify(editor.draft);
    editor.afterPeriod(0, 2); editor.edit(0, 'name', 'تعديل'); editor.resize(2); editor.remove(0);
    expect(JSON.stringify(editor.draft)).toBe(before);
  });
});
