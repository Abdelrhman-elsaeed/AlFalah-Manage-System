using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace AlFalah.Tests.Timetables;

public sealed class CurrentLessonResolverTests
{
    [Theory]
    [InlineData(4, 0, CurrentLessonResolutionKind.ActiveLesson, 1)]
    [InlineData(4, 44, CurrentLessonResolutionKind.ActiveLesson, 1)]
    [InlineData(4, 45, CurrentLessonResolutionKind.Gap, null)]
    [InlineData(4, 50, CurrentLessonResolutionKind.ActiveLesson, 2)]
    [InlineData(5, 35, CurrentLessonResolutionKind.Break, null)]
    [InlineData(5, 55, CurrentLessonResolutionKind.ActiveLesson, 3)]
    [InlineData(3, 59, CurrentLessonResolutionKind.OutsideSchoolHours, null)]
    [InlineData(6, 40, CurrentLessonResolutionKind.OutsideSchoolHours, null)]
    public async Task Uses_school_timezone_half_open_boundaries_breaks_and_gaps(
        int utcHour,
        int utcMinute,
        CurrentLessonResolutionKind expected,
        int? period)
    {
        var resolver = Resolver([Candidate()], [Entry()]);

        var result = await resolver.ResolveForClassroomAsync(
            1,
            new DateTimeOffset(2026, 9, 1, utcHour, utcMinute, 0, TimeSpan.Zero),
            20,
            "1/A",
            default);

        result.Kind.Should().Be(expected);
        result.PeriodSequence.Should().Be(period);
        result.SchoolTimeZoneId.Should().Be("Africa/Cairo");
        if (expected == CurrentLessonResolutionKind.ActiveLesson)
        {
            result.SchoolTimetableId.Should().Be(10);
            result.BellScheduleRevisionId.Should().Be(5);
            result.SchoolTimetableEntryId.Should().Be(30);
        }
    }

    [Fact]
    public async Task Distinguishes_non_study_missing_and_ambiguous_publication()
    {
        var nonStudy = await Resolver([Candidate()], [Entry()]).ResolveForClassroomAsync(
            1, new DateTimeOffset(2026, 9, 4, 4, 10, 0, TimeSpan.Zero), 20, "1/A", default);
        var missing = await Resolver([], []).ResolveForClassroomAsync(
            1, new DateTimeOffset(2026, 9, 1, 4, 10, 0, TimeSpan.Zero), 20, "1/A", default);
        var ambiguous = await Resolver([Candidate(), Candidate() with { SchoolTimetableId = 11 }], [Entry()])
            .ResolveForClassroomAsync(
                1, new DateTimeOffset(2026, 9, 1, 4, 10, 0, TimeSpan.Zero), 20, "1/A", default);

        nonStudy.Kind.Should().Be(CurrentLessonResolutionKind.NonStudyDay);
        missing.Kind.Should().Be(CurrentLessonResolutionKind.NoPublishedSchedule);
        ambiguous.Kind.Should().Be(CurrentLessonResolutionKind.AmbiguousPublishedSchedule);
    }

    [Fact]
    public async Task Returns_original_and_effective_instructor_with_substitution_identity()
    {
        var entry = Entry() with
        {
            EffectiveInstructor = new CurrentLessonInstructor(101, "substitute", "Substitute Teacher"),
            ActiveSubstitutionId = 77
        };
        var resolver = Resolver([Candidate()], [entry]);

        var substitute = await resolver.ResolveForInstructorAsync(
            1,
            new DateTimeOffset(2026, 9, 1, 4, 10, 0, TimeSpan.Zero),
            "substitute",
            default);
        var original = await resolver.ResolveForInstructorAsync(
            1,
            new DateTimeOffset(2026, 9, 1, 4, 10, 0, TimeSpan.Zero),
            "original",
            default);

        substitute.Kind.Should().Be(CurrentLessonResolutionKind.ActiveLesson);
        substitute.OriginalInstructor!.UserId.Should().Be("original");
        substitute.EffectiveInstructor!.UserId.Should().Be("substitute");
        substitute.ActiveSubstitutionId.Should().Be(77);
        original.Kind.Should().Be(CurrentLessonResolutionKind.Gap);
    }

    [Fact]
    public async Task Fails_closed_when_the_published_timezone_is_invalid()
    {
        var invalid = Candidate() with
        {
            Schedule = Schedule() with { SchoolTimeZoneId = "Invalid/E2E-Timezone" }
        };

        var result = await Resolver([invalid], [Entry()]).ResolveForClassroomAsync(
            1,
            new DateTimeOffset(2026, 9, 1, 4, 10, 0, TimeSpan.Zero),
            20,
            "1/A",
            default);

        result.Kind.Should().Be(CurrentLessonResolutionKind.NoPublishedSchedule);
        result.ResolutionReason.Should().Contain("timezone");
    }

    private static CurrentLessonResolver Resolver(
        IReadOnlyList<PublishedBellScheduleCandidate> candidates,
        IReadOnlyList<CurrentLessonEntry> entries) =>
        new(new ScheduleRepository(candidates), new EntryRepository(entries));

    private static PublishedBellScheduleCandidate Candidate() => new(10, 3, Schedule());

    private static BellScheduleDto Schedule() => new(
        2,
        5,
        1,
        1,
        TimetableSemester.First,
        "Reference schedule",
        1,
        "Africa/Cairo",
        [
            new BellPeriodDto(1, "Period 1", new TimeOnly(7, 0), new TimeOnly(7, 45)),
            new BellPeriodDto(2, "Period 2", new TimeOnly(7, 50), new TimeOnly(8, 35)),
            new BellPeriodDto(3, "Period 3", new TimeOnly(8, 55), new TimeOnly(9, 40))
        ],
        Enumerable.Range(1, 7)
            .Select(day => new BellScheduleDayDto(day, day != (int)TimetableDay.Friday, true, []))
            .ToArray(),
        [],
        [new ScheduleBreakDto("Morning break", "Break", new TimeOnly(8, 35), new TimeOnly(8, 55))]);

    private static CurrentLessonEntry Entry() => new(
        30,
        20,
        "1/A",
        SchoolStage.Primary,
        1,
        "A",
        "Math",
        new CurrentLessonInstructor(100, "original", "Original Teacher"),
        new CurrentLessonInstructor(100, "original", "Original Teacher"),
        null);

    private sealed class EntryRepository(IReadOnlyList<CurrentLessonEntry> entries) : ICurrentLessonEntryRepository
    {
        public Task<IReadOnlyList<CurrentLessonEntry>> GetEntriesAsync(
            int schoolId,
            int schoolTimetableId,
            DateOnly localDate,
            TimetableDay day,
            int periodSequence,
            int? classroomId,
            string? classroomLabel,
            CancellationToken cancellationToken) => Task.FromResult(entries);
    }

    private sealed class ScheduleRepository(IReadOnlyList<PublishedBellScheduleCandidate> candidates) : IBellScheduleRepository
    {
        public Task<IReadOnlyList<PublishedBellScheduleCandidate>> GetPublishedCandidatesAsync(int schoolId, DateTimeOffset instant, CancellationToken ct) =>
            Task.FromResult(candidates);
        public Task<BellScheduleDto?> GetPublishedAsync(int schoolId, DateTimeOffset instant, CancellationToken ct) =>
            Task.FromResult(candidates.Count == 1 ? candidates[0].Schedule : null);
        public Task<IReadOnlyList<BellScheduleDto>> ListAsync(int schoolId, int yearId, TimetableSemester semester, CancellationToken ct) => throw new NotSupportedException();
        public Task<BellScheduleTemplate?> FindAsync(int schoolId, int id, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> NameExistsAsync(int schoolId, SaveBellScheduleRequest request, int? exceptId, CancellationToken ct) => throw new NotSupportedException();
        public Task<BellScheduleDependencies> GetDependenciesAsync(int schoolId, int? templateId, int? profileId, CancellationToken ct) => throw new NotSupportedException();
        public Task SaveRevisionAsync(BellScheduleTemplate template, BellScheduleRevision revision, bool isNew, CancellationToken ct) => throw new NotSupportedException();
        public Task SelectAsync(TimetableSetupProfile profile, BellScheduleTemplate template, CancellationToken ct) => throw new NotSupportedException();
        public Task<BellScheduleDto?> GetRevisionAsync(int schoolId, int revisionId, CancellationToken ct) => throw new NotSupportedException();
        public Task<BellScheduleDto?> GetSelectedAsync(int schoolId, int yearId, TimetableSemester semester, int? profileId, CancellationToken ct) => throw new NotSupportedException();
    }
}
