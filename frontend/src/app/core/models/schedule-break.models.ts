import { BellPeriod, SaveBellSchedule, ScheduleBreak, effectivePeriods } from './bell-schedule.models';

export const breakCategories = [
  { label: 'فسحة', value: 'Recess' }, { label: 'صلاة', value: 'Prayer' },
  { label: 'وجبة', value: 'Meal' }, { label: 'تجمع', value: 'Assembly' }, { label: 'أخرى', value: 'Other' }
];

export function effectiveBreaks(schedule: SaveBellSchedule, day: number): ScheduleBreak[] {
  if (day === 0) return schedule.defaultBreaks ?? [];
  const definition = schedule.days.find(x => x.day === day);
  return !definition?.isStudyDay ? [] : definition.usesDefaultBreaks !== false ? schedule.defaultBreaks ?? [] : definition.breaks ?? [];
}

const seconds = (time: string) => {
  if (!/^([01]\d|2[0-3]):[0-5]\d(:[0-5]\d(\.\d{1,7})?)?$/.test(time)) return NaN;
  const [h, m, s] = time.split(':').map(Number); return h * 3600 + m * 60 + (s ?? 0);
};
export function breakErrors(breaks: ScheduleBreak[], periods: BellPeriod[]) {
  const rows = new Map<number, string[]>(), lessons = new Map<number, string[]>();
  const lessonConflicts = new Set<number>(), breakConflicts = new Set<number>();
  const add = (map: Map<number, string[]>, index: number, message: string) => map.set(index, [...(map.get(index) ?? []), message]);
  const overlap = (a: ScheduleBreak, b: ScheduleBreak | BellPeriod) =>
    seconds(a.startLocalTime) < seconds(b.endLocalTime) && seconds(b.startLocalTime) < seconds(a.endLocalTime);
  breaks.forEach((row, i) => {
    if (!row.name.trim() || row.name.length > 120) add(rows, i, 'اسم الاستراحة مطلوب ولا يتجاوز 120 حرفاً.');
    if (row.category && !breakCategories.some(x => x.value === row.category)) add(rows, i, 'فئة الاستراحة غير صالحة.');
    if (!(seconds(row.startLocalTime) < seconds(row.endLocalTime))) add(rows, i, 'يجب أن تسبق البداية النهاية دون عبور منتصف الليل.');
    periods.forEach((period, p) => {
      if (!overlap(row, period)) return;
      lessonConflicts.add(i);
      const message = `الاستراحة «${row.name}» تتداخل مع الحصة ${period.sequence}.`;
      add(rows, i, message); add(lessons, p, message);
    });
    breaks.slice(0, i).forEach((other, j) => {
      if (!overlap(row, other)) return;
      breakConflicts.add(i); breakConflicts.add(j);
      const message = `الاستراحة «${row.name}» تتداخل مع «${other.name}».`;
      add(rows, i, message); add(rows, j, message);
    });
  });
  const summaries = new Map([...rows].map(([index, messages]) => [index,
    lessonConflicts.has(index) ? 'يوجد تداخل زمني مع الحصص. يرجى مراجعة الأوقات.'
      : breakConflicts.has(index) ? 'يوجد تداخل زمني مع استراحة أخرى. يرجى مراجعة الأوقات.'
      : messages[0]
  ]));
  return { rows, lessons, summaries };
}

export function scheduleBreakIssues(schedule: SaveBellSchedule): { day: number; message: string }[] {
  return [0, ...schedule.days.filter(x => x.isStudyDay).map(x => x.day)].flatMap(day =>
    [...new Set([...breakErrors(effectiveBreaks(schedule, day), day === 0 ? schedule.defaultPeriods : effectivePeriods(schedule, day)).rows.values()].flat())]
      .map(message => ({ day, message })));
}

export function effectiveIntervals(schedule: SaveBellSchedule, day: number) {
  return [
    ...effectivePeriods(schedule, day).map(x => ({ kind: 'Lesson', name: x.displayLabel || `الحصة ${x.sequence}`, startLocalTime: x.startLocalTime, endLocalTime: x.endLocalTime, periodSequence: x.sequence })),
    ...effectiveBreaks(schedule, day).map(x => ({ kind: 'Break', name: x.name, startLocalTime: x.startLocalTime, endLocalTime: x.endLocalTime, periodSequence: null }))
  ].sort((a, b) => seconds(a.startLocalTime) - seconds(b.startLocalTime));
}
