using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.StudentAffairs.TeacherContext;

public sealed class TeacherContextScheduleOptions
{
    public string SchoolTimeZoneId { get; init; } = "Africa/Cairo";

}

public readonly record struct TeacherPeriodWindow(int Period, DateTimeOffset StartsAt, DateTimeOffset EndsAt);

/// <summary>Request-scoped view of the immutable published timing revision.</summary>
public sealed class TeacherContextSchedule(TeacherContextScheduleOptions options, IBellScheduleRepository repository)
{
    private BellScheduleDto? _schedule;
    private DateOnly _date;
    public int? RevisionId => _schedule?.RevisionId;
    public TimeZoneInfo TimeZone => TimeZoneInfo.FindSystemTimeZoneById(_schedule?.SchoolTimeZoneId ?? options.SchoolTimeZoneId);
    public string TimeZoneId => TimeZone.Id;
    public bool AllowOffHoursFallback => false;

    public async Task LoadAsync(int schoolId, DateTimeOffset instant, CancellationToken ct)
    {
        _schedule = await repository.GetPublishedAsync(schoolId, instant, ct);
        _date = DateOnly.FromDateTime(ToSchoolLocalTime(instant).DateTime);
    }
    public DateTimeOffset ToSchoolLocalTime(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, TimeZone);
    public int? GetCurrentPeriod(TimeOnly time) => BellScheduleResolver.EffectivePeriods(_schedule, BellScheduleResolver.ToDay(_date.DayOfWeek))
        .SingleOrDefault(x => x.StartLocalTime <= time && time < x.EndLocalTime)?.Sequence;
    public int GetFallbackPeriod(TimeOnly time) => 0;
    public bool HasPeriod(DateOnly date, int period) => BellScheduleResolver.EffectivePeriods(_schedule, BellScheduleResolver.ToDay(date.DayOfWeek)).Any(x => x.Sequence == period);
    public TeacherPeriodWindow GetWindow(DateOnly date, int period)
    {
        var slot = BellScheduleResolver.EffectivePeriods(_schedule, BellScheduleResolver.ToDay(date.DayOfWeek)).Single(x => x.Sequence == period);
        var start = date.ToDateTime(slot.StartLocalTime, DateTimeKind.Unspecified);
        var end = date.ToDateTime(slot.EndLocalTime, DateTimeKind.Unspecified);
        return new(period, new DateTimeOffset(start, TimeZone.GetUtcOffset(start)), new DateTimeOffset(end, TimeZone.GetUtcOffset(end)));
    }
    public static TimetableDay? ToTimetableDay(DayOfWeek day) => BellScheduleResolver.ToDay(day);
}
