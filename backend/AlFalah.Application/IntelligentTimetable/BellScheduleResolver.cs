using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.IntelligentTimetable;

public sealed class BellScheduleResolver(IBellScheduleRepository repository)
{
    public Task<BellScheduleDto?> PublishedAsync(int schoolId, DateTimeOffset instant, CancellationToken ct) =>
        repository.GetPublishedAsync(schoolId, instant, ct);

    public static TimetableDay ToDay(DayOfWeek day) => (TimetableDay)(((int)day + 1) % 7 + 1);
    public static IReadOnlyList<BellPeriodDto> EffectivePeriods(BellScheduleDto? schedule, TimetableDay day)
    {
        var definition = schedule?.Days.FirstOrDefault(x => x.Day == (int)day);
        if (definition is null || !definition.IsStudyDay) return Array.Empty<BellPeriodDto>();
        return definition.UsesDefaultSchedule ? schedule!.DefaultPeriods : definition.Periods;
    }

    public static DateTimeOffset LocalTime(BellScheduleDto schedule, DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(schedule.SchoolTimeZoneId));

    public static IReadOnlyList<ScheduleBreakDto> EffectiveBreaks(BellScheduleDto? schedule, TimetableDay day)
    {
        var definition = schedule?.Days.FirstOrDefault(x => x.Day == (int)day);
        return definition is null || !definition.IsStudyDay ? []
            : (definition.UsesDefaultBreaks ? schedule!.DefaultBreaks : definition.Breaks) ?? [];
    }

    public static IReadOnlyList<ScheduleIntervalDto> EffectiveIntervals(BellScheduleDto schedule, TimetableDay day) =>
        EffectivePeriods(schedule, day).Select(x => new ScheduleIntervalDto("Lesson", x.DisplayLabel ?? $"الحصة {x.Sequence}", x.StartLocalTime, x.EndLocalTime, x.Sequence))
            .Concat(EffectiveBreaks(schedule, day).Select(x => new ScheduleIntervalDto("Break", x.Name, x.StartLocalTime, x.EndLocalTime)))
            .OrderBy(x => x.StartLocalTime).ToArray();

    public static BellPeriodDto? CurrentPeriod(BellScheduleDto? schedule, DateTimeOffset instant)
    {
        if (schedule is null) return null;
        var local = LocalTime(schedule, instant);
        var clock = TimeOnly.FromDateTime(local.DateTime);
        if (EffectiveBreaks(schedule, ToDay(local.DayOfWeek)).Any(x => x.StartLocalTime <= clock && clock < x.EndLocalTime)) return null;
        return EffectivePeriods(schedule, ToDay(local.DayOfWeek))
            .SingleOrDefault(x => x.StartLocalTime <= clock && clock < x.EndLocalTime);
    }
}
