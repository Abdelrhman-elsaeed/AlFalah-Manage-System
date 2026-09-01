using AlFalah.Domain.Enums;

namespace AlFalah.Application.StudentAffairs.TeacherContext;

public sealed class TeacherContextScheduleOptions
{
    public string SchoolTimeZoneId { get; init; } = "Africa/Cairo";
    public TimeOnly FirstPeriodStartsAt { get; init; } = new(7, 0);
    public int PeriodDurationMinutes { get; init; } = 45;
    public int PassingTimeMinutes { get; init; } = 5;
    public bool AllowOffHoursFallback { get; init; }
}

public readonly record struct TeacherPeriodWindow(
    byte Period,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt);

public sealed class TeacherContextSchedule
{
    public const byte PeriodCount = 8;

    private readonly TeacherContextScheduleOptions _options;

    public TeacherContextSchedule(TeacherContextScheduleOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.PeriodDurationMinutes);
        ArgumentOutOfRangeException.ThrowIfNegative(options.PassingTimeMinutes);
        _options = options;
        TimeZone = ResolveTimeZone(options.SchoolTimeZoneId);
    }

    public TimeZoneInfo TimeZone { get; }
    public string TimeZoneId => TimeZone.Id;
    public bool AllowOffHoursFallback => _options.AllowOffHoursFallback;

    public DateTimeOffset ToSchoolLocalTime(DateTimeOffset utcNow) =>
        TimeZoneInfo.ConvertTime(utcNow, TimeZone);

    public byte? GetCurrentPeriod(TimeOnly schoolLocalTime)
    {
        for (byte period = 1; period <= PeriodCount; period++)
        {
            var (startsAt, endsAt) = GetLocalTimes(period);
            if (schoolLocalTime >= startsAt && schoolLocalTime < endsAt)
            {
                return period;
            }
        }

        return null;
    }

    public byte GetFallbackPeriod(TimeOnly schoolLocalTime)
    {
        var slotMinutes = _options.PeriodDurationMinutes + _options.PassingTimeMinutes;
        var elapsedMinutes = (schoolLocalTime.ToTimeSpan() - _options.FirstPeriodStartsAt.ToTimeSpan()).TotalMinutes;
        var period = (int)Math.Floor(elapsedMinutes / slotMinutes) + 1;
        return (byte)Math.Clamp(period, 1, PeriodCount);
    }

    public TeacherPeriodWindow GetWindow(DateOnly schoolLocalDate, byte period)
    {
        if (period is < 1 or > PeriodCount)
        {
            throw new ArgumentOutOfRangeException(nameof(period));
        }

        var (startTime, endTime) = GetLocalTimes(period);
        var start = schoolLocalDate.ToDateTime(startTime, DateTimeKind.Unspecified);
        var end = schoolLocalDate.ToDateTime(endTime, DateTimeKind.Unspecified);
        return new TeacherPeriodWindow(
            period,
            new DateTimeOffset(start, TimeZone.GetUtcOffset(start)),
            new DateTimeOffset(end, TimeZone.GetUtcOffset(end)));
    }

    public static TimetableDay? ToTimetableDay(DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Saturday => TimetableDay.Saturday,
        DayOfWeek.Sunday => TimetableDay.Sunday,
        DayOfWeek.Monday => TimetableDay.Monday,
        DayOfWeek.Tuesday => TimetableDay.Tuesday,
        DayOfWeek.Wednesday => TimetableDay.Wednesday,
        DayOfWeek.Thursday => TimetableDay.Thursday,
        _ => null
    };

    private (TimeOnly StartsAt, TimeOnly EndsAt) GetLocalTimes(byte period)
    {
        var offsetMinutes = (period - 1)
            * (_options.PeriodDurationMinutes + _options.PassingTimeMinutes);
        var startsAt = _options.FirstPeriodStartsAt.AddMinutes(offsetMinutes);
        return (startsAt, startsAt.AddMinutes(_options.PeriodDurationMinutes));
    }

    private static TimeZoneInfo ResolveTimeZone(string configuredId)
    {
        if (string.IsNullOrWhiteSpace(configuredId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(configuredId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
