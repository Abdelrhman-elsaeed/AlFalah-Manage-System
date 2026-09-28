using AlFalah.Domain.Enums;

namespace AlFalah.Application.IntelligentTimetable;

public enum CurrentLessonResolutionKind
{
    ActiveLesson = 1,
    Break = 2,
    Gap = 3,
    OutsideSchoolHours = 4,
    NonStudyDay = 5,
    NoPublishedSchedule = 6,
    AmbiguousPublishedSchedule = 7
}

public sealed record CurrentLessonClassroom(
    int Id,
    string Label,
    SchoolStage Stage,
    byte GradeLevel,
    string Section);

public sealed record CurrentLessonInstructor(int Id, string UserId, string DisplayName);

public sealed record CurrentLessonEntry(
    int SchoolTimetableEntryId,
    int? ClassroomId,
    string ClassroomLabel,
    SchoolStage? ClassroomStage,
    byte? ClassroomGradeLevel,
    string? ClassroomSection,
    string Subject,
    CurrentLessonInstructor OriginalInstructor,
    CurrentLessonInstructor EffectiveInstructor,
    int? ActiveSubstitutionId);

public sealed record CurrentLessonResolution(
    CurrentLessonResolutionKind Kind,
    DateOnly? SchoolLocalDate,
    DateTimeOffset? SchoolLocalTime,
    string? SchoolTimeZoneId,
    int? AcademicYearId,
    TimetableSemester? Semester,
    int? BellScheduleRevisionId,
    int? SchoolTimetableId,
    int? TimetableRevision,
    int? SchoolTimetableEntryId,
    int? PeriodSequence,
    DateTimeOffset? PeriodStartsAt,
    DateTimeOffset? PeriodEndsAt,
    CurrentLessonClassroom? Classroom,
    CurrentLessonInstructor? OriginalInstructor,
    CurrentLessonInstructor? EffectiveInstructor,
    int? ActiveSubstitutionId,
    string ResolutionReason)
{
    public bool HasValidPublishedSchedule => Kind is not CurrentLessonResolutionKind.NoPublishedSchedule
        and not CurrentLessonResolutionKind.AmbiguousPublishedSchedule;

    public bool TeacherAcknowledgementRequired => Kind == CurrentLessonResolutionKind.ActiveLesson;
}

public interface ICurrentLessonEntryRepository
{
    Task<IReadOnlyList<CurrentLessonEntry>> GetEntriesAsync(
        int schoolId,
        int schoolTimetableId,
        DateOnly localDate,
        TimetableDay day,
        int periodSequence,
        int? classroomId,
        string? classroomLabel,
        CancellationToken cancellationToken);
}

public interface ICurrentLessonResolver
{
    Task<CurrentLessonResolution> ResolveForClassroomAsync(
        int schoolId,
        DateTimeOffset instant,
        int classroomId,
        string? classroomLabel,
        CancellationToken cancellationToken);

    Task<CurrentLessonResolution> ResolveForInstructorAsync(
        int schoolId,
        DateTimeOffset instant,
        string instructorUserId,
        CancellationToken cancellationToken);
}

public sealed class CurrentLessonResolver(
    IBellScheduleRepository schedules,
    ICurrentLessonEntryRepository entries) : ICurrentLessonResolver
{
    public Task<CurrentLessonResolution> ResolveForClassroomAsync(
        int schoolId,
        DateTimeOffset instant,
        int classroomId,
        string? classroomLabel,
        CancellationToken cancellationToken) =>
        ResolveAsync(schoolId, instant, classroomId, classroomLabel, null, cancellationToken);

    public Task<CurrentLessonResolution> ResolveForInstructorAsync(
        int schoolId,
        DateTimeOffset instant,
        string instructorUserId,
        CancellationToken cancellationToken) =>
        ResolveAsync(schoolId, instant, null, null, instructorUserId, cancellationToken);

    private async Task<CurrentLessonResolution> ResolveAsync(
        int schoolId,
        DateTimeOffset instant,
        int? classroomId,
        string? classroomLabel,
        string? instructorUserId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PublishedBellScheduleCandidate> candidates;
        try
        {
            candidates = await schedules.GetPublishedCandidatesAsync(schoolId, instant, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeZoneNotFoundException)
        {
            return Empty(CurrentLessonResolutionKind.NoPublishedSchedule, "The published schedule has an unknown school timezone");
        }
        catch (InvalidTimeZoneException)
        {
            return Empty(CurrentLessonResolutionKind.NoPublishedSchedule, "The published schedule has an invalid school timezone");
        }
        if (candidates.Count == 0)
            return Empty(CurrentLessonResolutionKind.NoPublishedSchedule, "No published schedule is effective for the school date");
        if (candidates.Count != 1)
            return Empty(CurrentLessonResolutionKind.AmbiguousPublishedSchedule, "More than one published schedule is effective for the school date");

        var candidate = candidates[0];
        var schedule = candidate.Schedule;
        DateTimeOffset local;
        try
        {
            local = BellScheduleResolver.LocalTime(schedule, instant);
        }
        catch (TimeZoneNotFoundException)
        {
            return Empty(CurrentLessonResolutionKind.NoPublishedSchedule, "The published schedule has an unknown school timezone");
        }
        catch (InvalidTimeZoneException)
        {
            return Empty(CurrentLessonResolutionKind.NoPublishedSchedule, "The published schedule has an invalid school timezone");
        }
        var localDate = DateOnly.FromDateTime(local.DateTime);
        var day = BellScheduleResolver.ToDay(local.DayOfWeek);
        var dayDefinition = schedule.Days.SingleOrDefault(x => x.Day == (int)day);
        var context = new ResolutionContext(candidate, local, localDate);

        if (dayDefinition is null || !dayDefinition.IsStudyDay)
            return context.Empty(CurrentLessonResolutionKind.NonStudyDay, "The school date is not configured as a study day");

        var clock = TimeOnly.FromDateTime(local.DateTime);
        if (BellScheduleResolver.EffectiveBreaks(schedule, day)
            .Any(x => x.StartLocalTime <= clock && clock < x.EndLocalTime))
            return context.Empty(CurrentLessonResolutionKind.Break, "Teacher acknowledgement is not required during a configured break");

        var periods = BellScheduleResolver.EffectivePeriods(schedule, day);
        var activePeriods = periods
            .Where(x => x.StartLocalTime <= clock && clock < x.EndLocalTime)
            .ToArray();
        if (activePeriods.Length > 1)
            return context.Empty(CurrentLessonResolutionKind.AmbiguousPublishedSchedule, "The published Bell revision contains overlapping active periods");
        if (activePeriods.Length == 0)
        {
            var outside = periods.Count == 0
                || clock < periods.Min(x => x.StartLocalTime)
                || clock >= periods.Max(x => x.EndLocalTime);
            return context.Empty(
                outside ? CurrentLessonResolutionKind.OutsideSchoolHours : CurrentLessonResolutionKind.Gap,
                outside
                    ? "Teacher acknowledgement is not required outside school lesson hours"
                    : "Teacher acknowledgement is not required during a timetable gap");
        }

        var period = activePeriods[0];
        var matches = await entries.GetEntriesAsync(
            schoolId,
            candidate.SchoolTimetableId,
            localDate,
            day,
            period.Sequence,
            classroomId,
            classroomLabel,
            cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(instructorUserId))
            matches = matches.Where(x => x.EffectiveInstructor.UserId == instructorUserId).ToArray();

        if (matches.Count == 0)
            return context.Empty(CurrentLessonResolutionKind.Gap, "No lesson entry matches the requested classroom or instructor in the active period");
        if (matches.Count != 1)
            return context.Empty(CurrentLessonResolutionKind.AmbiguousPublishedSchedule, "Multiple lesson entries match the requested classroom or instructor");

        var entry = matches[0];
        var startsAt = LocalBoundary(localDate, period.StartLocalTime, schedule.SchoolTimeZoneId);
        var endsAt = LocalBoundary(localDate, period.EndLocalTime, schedule.SchoolTimeZoneId);
        var classroom = entry.ClassroomId is null
            ? null
            : new CurrentLessonClassroom(
                entry.ClassroomId.Value,
                entry.ClassroomLabel,
                entry.ClassroomStage ?? default,
                entry.ClassroomGradeLevel ?? 0,
                entry.ClassroomSection ?? string.Empty);

        return new CurrentLessonResolution(
            CurrentLessonResolutionKind.ActiveLesson,
            localDate,
            local,
            schedule.SchoolTimeZoneId,
            schedule.AcademicYearId,
            schedule.Semester,
            schedule.RevisionId,
            candidate.SchoolTimetableId,
            candidate.TimetableRevision,
            entry.SchoolTimetableEntryId,
            period.Sequence,
            startsAt,
            endsAt,
            classroom,
            entry.OriginalInstructor,
            entry.EffectiveInstructor,
            entry.ActiveSubstitutionId,
            entry.ActiveSubstitutionId is null
                ? "Active lesson resolved from the published timetable"
                : "Active lesson resolved with a date-specific instructor substitution");
    }

    private static DateTimeOffset LocalBoundary(DateOnly date, TimeOnly time, string timeZoneId)
    {
        var value = date.ToDateTime(time, DateTimeKind.Unspecified);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return new DateTimeOffset(value, zone.GetUtcOffset(value));
    }

    private static CurrentLessonResolution Empty(CurrentLessonResolutionKind kind, string reason) =>
        new(kind, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, reason);

    private sealed record ResolutionContext(
        PublishedBellScheduleCandidate Candidate,
        DateTimeOffset LocalTime,
        DateOnly LocalDate)
    {
        public CurrentLessonResolution Empty(CurrentLessonResolutionKind kind, string reason) =>
            new(
                kind,
                LocalDate,
                LocalTime,
                Candidate.Schedule.SchoolTimeZoneId,
                Candidate.Schedule.AcademicYearId,
                Candidate.Schedule.Semester,
                Candidate.Schedule.RevisionId,
                Candidate.SchoolTimetableId,
                Candidate.TimetableRevision,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                reason);
    }
}
