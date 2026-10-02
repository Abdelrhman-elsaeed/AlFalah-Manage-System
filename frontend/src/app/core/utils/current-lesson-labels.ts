interface CurrentLessonCopy {
  readonly state: string;
  readonly reason: string;
}

const CURRENT_LESSON_COPY: Readonly<Record<string, CurrentLessonCopy>> = {
  ActiveLesson: {
    state: 'حصة نشطة',
    reason: 'تم تحديد الحصة الحالية من الجدول المنشور.'
  },
  Break: {
    state: 'استراحة مدرسية',
    reason: 'الوقت الحالي يقع ضمن استراحة مدرسية.'
  },
  Gap: {
    state: 'لا توجد حصة مسندة',
    reason: 'لا توجد حصة مسندة لك في هذه الفترة.'
  },
  OutsideSchoolHours: {
    state: 'خارج وقت الحصص',
    reason: 'الوقت الحالي خارج ساعات الحصص المدرسية.'
  },
  NonStudyDay: {
    state: 'يوم غير دراسي',
    reason: 'تاريخ اليوم غير مُعدّ كيوم دراسي في توقيت المدرسة.'
  },
  NoPublishedSchedule: {
    state: 'لا يوجد جدول منشور',
    reason: 'لا يوجد جدول منشور وساري لهذا التاريخ.'
  },
  AmbiguousPublishedSchedule: {
    state: 'تعارض في الجدول المنشور',
    reason: 'يوجد أكثر من جدول منشور وساري؛ يلزم مراجعة إعدادات الجدول.'
  }
};

export function currentLessonStateLabel(kind: string): string {
  return CURRENT_LESSON_COPY[kind]?.state ?? kind;
}

export function currentLessonReasonLabel(kind: string, fallback: string): string {
  return CURRENT_LESSON_COPY[kind]?.reason ?? fallback;
}

export function teacherAlertLabel(alert: string, resolutionKind: string, resolutionReason: string): string {
  if (alert === 'No current lesson was found in the published timetable') {
    return currentLessonReasonLabel(resolutionKind, resolutionReason);
  }

  const gatePasses = /^(\d+) gate pass acknowledgement\(s\) pending$/.exec(alert);
  if (gatePasses) return `${gatePasses[1]} استئذان خروج بانتظار إقرار الاستلام.`;

  const entryPermits = /^(\d+) entry permit acknowledgement\(s\) pending$/.exec(alert);
  if (entryPermits) return `${entryPermits[1]} تصريح دخول بانتظار إقرار الاستلام.`;

  return alert;
}
