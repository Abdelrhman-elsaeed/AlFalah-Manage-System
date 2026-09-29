namespace AlFalah.Application.IntelligentTimetable;

public interface ISchoolLocalDateResolver
{
    Task<DateOnly?> ResolveAsync(
        int schoolId,
        DateTimeOffset instant,
        CancellationToken cancellationToken);
}

public sealed class SchoolLocalDateResolver(IBellScheduleRepository schedules)
    : ISchoolLocalDateResolver
{
    public async Task<DateOnly?> ResolveAsync(
        int schoolId,
        DateTimeOffset instant,
        CancellationToken cancellationToken)
    {
        try
        {
            var candidates = await schedules.GetPublishedCandidatesAsync(
                schoolId,
                instant,
                cancellationToken).ConfigureAwait(false);

            if (candidates.Count != 1) return null;

            return DateOnly.FromDateTime(
                BellScheduleResolver.LocalTime(candidates[0].Schedule, instant).DateTime);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return null;
        }
    }
}
