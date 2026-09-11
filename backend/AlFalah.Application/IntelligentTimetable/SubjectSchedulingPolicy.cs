using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.IntelligentTimetable;

/// <summary>Shared feasibility and ranking rules for subject configuration and subsequent generation.</summary>
public static class SubjectSchedulingPolicy
{
    public static bool Adjacent(BellPeriodDto first, BellPeriodDto second) =>
        second.Sequence == first.Sequence + 1 && first.EndLocalTime == second.StartLocalTime;

    // A preference changes ranking only. It must never remove a candidate or fail feasibility.
    public static int PreferencePenalty(SubjectRulesRequest rules, int sequence) =>
        rules.TimePreference == "None" ? 0 : Math.Max(0, (rules.EarliestPeriodSequence ?? sequence) - sequence)
            + Math.Max(0, sequence - (rules.LatestPreferredPeriodSequence ?? sequence));

    public static void Validate(SubjectRulesRequest rules, BellScheduleDto schedule)
    {
        var days = schedule.Days.Where(x => x.IsStudyDay && (rules.AllowedDays.Count == 0 || rules.AllowedDays.Contains(x.Day))).ToArray();
        if (days.Length == 0 || rules.AllowedDays.Any(d => days.All(x => x.Day != d)))
            throw new ArgumentException("الأيام المسموحة يجب أن تكون ضمن أيام الدراسة.");
        var total = rules.IndividualPeriodCount + 2 * rules.PairedBlockCount;
        var capacity = 0; var pairs = 0;
        foreach (var day in days)
        {
            var periods = BellScheduleResolver.EffectivePeriods(schedule, (TimetableDay)day.Day).OrderBy(x => x.Sequence).ToArray();
            capacity += periods.Length;
            for (var i = 0; i + 1 < periods.Length; i++)
                if (Adjacent(periods[i], periods[i + 1])) { pairs++; i++; }
            if (rules.TimePreference != "None" && (!periods.Any(x => x.Sequence == rules.EarliestPeriodSequence) || !periods.Any(x => x.Sequence == rules.LatestPreferredPeriodSequence)))
                throw new ArgumentException("نافذة التفضيل يجب أن تشير إلى حصص موجودة في الأيام المسموحة.");
        }
        if (total > capacity) throw new ArgumentException("عدد الحصص يتجاوز الحصص المتاحة في الأيام المسموحة.");
        if (rules.PairedBlockCount > pairs) throw new ArgumentException("لا توجد حصص متجاورة زمنياً تكفي للكتل الزوجية. الاستراحة لا يمكن أن تقسم حصة زوجية.");
        if (rules.FixedSlots.Count > total) throw new ArgumentException("الحصص المثبتة تتجاوز النصاب الأسبوعي.");
        foreach (var slot in rules.FixedSlots)
            if (days.All(x => x.Day != slot.Day) || !BellScheduleResolver.EffectivePeriods(schedule, (TimetableDay)slot.Day).Any(x => x.Sequence == slot.Period))
                throw new ArgumentException("الحصة المثبتة غير موجودة في جدول الأيام المسموحة.");
    }
}
