export interface BellPeriod { sequence: number; displayLabel: string | null; startLocalTime: string; endLocalTime: string; }
export interface ScheduleBreak { name: string; category: string | null; startLocalTime: string; endLocalTime: string; }
export interface BellDay { day: number; isStudyDay: boolean; usesDefaultSchedule: boolean; periods: BellPeriod[]; usesDefaultBreaks?: boolean; breaks?: ScheduleBreak[]; }
export interface BellSchedule {
  id: number; revisionId: number; schoolId: number; academicYearId: number; semester: number;
  name: string; revision: number; schoolTimeZoneId: string; defaultPeriods: BellPeriod[];
  days: BellDay[]; selectedByProfileIds: number[]; defaultBreaks?: ScheduleBreak[];
}
export type SaveBellSchedule = Pick<BellSchedule, 'academicYearId' | 'semester' | 'name' | 'revision' | 'schoolTimeZoneId' | 'defaultPeriods' | 'days' | 'defaultBreaks'>;
export function effectivePeriods(schedule: Pick<BellSchedule, 'days' | 'defaultPeriods'>, day: number): BellPeriod[] {
  const definition = schedule.days.find(x => x.day === day);
  return !definition?.isStudyDay ? [] : definition.usesDefaultSchedule ? schedule.defaultPeriods : definition.periods;
}
export function periodErrors(periods: BellPeriod[]): Map<number, string[]> {
  const errors = new Map<number, string[]>();
  const add = (row: number, text: string) => errors.set(row, [...(errors.get(row) ?? []), text]);
  periods.forEach((period, index) => {
    const time = /^([01]\d|2[0-3]):[0-5]\d(:[0-5]\d(\.\d{1,7})?)?$/;
    if (!time.test(period.startLocalTime) || !time.test(period.endLocalTime) || period.startLocalTime >= period.endLocalTime)
      add(index, 'يجب أن تسبق البداية النهاية، دون عبور منتصف الليل.');
    if (index && period.startLocalTime < periods[index - 1].endLocalTime) {
      const message = `الحصة ${index + 1} تبدأ قبل نهاية الحصة ${index}.`;
      add(index, message); add(index - 1, message);
    }
    for (let earlier = 0; earlier < index - 1; earlier++) {
      const other = periods[earlier];
      if (period.startLocalTime >= other.endLocalTime || other.startLocalTime >= period.endLocalTime) continue;
      const message = `الحصة ${index + 1} تتداخل مع الحصة ${earlier + 1}.`;
      add(index, message); add(earlier, message);
    }
  });
  return errors;
}
