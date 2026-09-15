using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.IntelligentTimetable.Handlers;
using AlFalah.Application.Interfaces;
using AlFalah.Application.Validators.IntelligentTimetable;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AlFalah.Tests.Timetables;

public sealed class BellSchedulePhase2Tests
{
    [Theory]
    [InlineData("07:00", "07:00", false)]
    [InlineData("08:00", "07:00", false)]
    [InlineData("23:45", "00:30", false)]
    [InlineData("07:00", "07:01", true)]
    [InlineData("00:00", "23:59", true)]
    public void Period_boundaries_allow_flexible_duration_but_never_midnight_crossing(string start, string end, bool valid)
    {
        var request = Request() with { DefaultPeriods = new[] { Period(1, start, end) } };
        new SaveBellScheduleValidator().Validate(request).IsValid.Should().Be(valid);
    }

    [Theory]
    [InlineData("07:44", false)]
    [InlineData("07:45", true)]
    [InlineData("07:50", true)]
    [InlineData("06:00", false)]
    public void Ordered_periods_can_touch_or_have_gaps_but_cannot_overlap_or_run_backwards(string secondStart, bool valid)
    {
        var result = new SaveBellScheduleValidator().Validate(Request() with {
            DefaultPeriods = new[] { Period(1, "07:00", "07:45"), Period(2, secondStart, "08:30") } });
        result.IsValid.Should().Be(valid);
        if (!valid) result.Errors.Should().Contain(x => x.PropertyName == "DefaultPeriods[1]")
            .And.Contain(x => x.PropertyName == "DefaultPeriods[2]");
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 1)]
    [InlineData(1, 3)]
    public void Period_sequences_must_be_positive_unique_and_contiguous(int first, int second)
    {
        new SaveBellScheduleValidator().Validate(Request() with { DefaultPeriods = new[] {
            Period(first, "07:00", "07:45"), Period(second, "08:00", "08:45") } }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Overlap_feedback_includes_nonadjacent_periods_inside_a_long_period()
    {
        var result = new SaveBellScheduleValidator().Validate(Request() with { DefaultPeriods = new[] {
            Period(1, "07:00", "09:00"), Period(2, "07:10", "07:20"), Period(3, "08:00", "08:40") } });
        result.Errors.Should().Contain(x => x.PropertyName == "DefaultPeriods[3]");
    }

    [Fact]
    public void Every_weekday_is_supported_and_there_is_no_eight_or_byte_period_limit()
    {
        var periods = Enumerable.Range(1, 300).Select(i => new BellPeriodDto(i, null,
            TimeOnly.MinValue.AddMinutes(i - 1), TimeOnly.MinValue.AddMinutes(i))).ToArray();
        new SaveBellScheduleValidator().Validate(Request() with { DefaultPeriods = periods }).IsValid.Should().BeTrue();
        BellScheduleResolver.ToDay(DayOfWeek.Friday).Should().Be(TimetableDay.Friday);
        BellScheduleResolver.ToDay(DayOfWeek.Saturday).Should().Be(TimetableDay.Saturday);
    }

    [Fact]
    public void Missing_study_days_invalid_zones_and_hidden_periods_are_rejected()
    {
        var validator = new SaveBellScheduleValidator();
        validator.Validate(Request() with { Days = Array.Empty<BellScheduleDayDto>() }).IsValid.Should().BeFalse();
        validator.Validate(Request() with { Days = Request().Days.Select(x => x with { IsStudyDay = false }).ToArray() }).IsValid.Should().BeFalse();
        validator.Validate(Request() with { SchoolTimeZoneId = "not/a-zone" }).IsValid.Should().BeFalse();
        validator.Validate(Request() with { Name = "  " }).IsValid.Should().BeFalse();
        validator.Validate(Request() with { Days = Request().Days.Select(x => x with { Periods = Request().DefaultPeriods }).ToArray() }).IsValid.Should().BeFalse();
        validator.Validate(Request() with { Days = Request().Days.Select(x => x with { UsesDefaultSchedule = false }).ToArray() }).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Revisions_preserve_published_times_and_only_invalidate_draft_dependents()
    {
        await using var db = Database();
        var old = await BellScheduleTestData.SeedAsync(db, published: true);
        var repository = new BellScheduleRepository(db);
        var original = await repository.GetRevisionAsync(1, old.Id, default);
        var handler = Handler(db);
        var request = Request() with { Name = "  توقيت جديد  ", Revision = 1, DefaultPeriods = new[] { Period(1, "09:00", "09:45") } };
        var response = await handler.Handle(new(old.BellScheduleTemplateId, request), default);
        response.IsSuccess.Should().BeTrue();
        response.Data!.Revision.Should().Be(2);
        response.Data.Name.Should().Be("توقيت جديد");
        (await repository.GetRevisionAsync(1, old.Id, default))!.DefaultPeriods.Should().BeEquivalentTo(original!.DefaultPeriods);
        var published = await repository.GetPublishedAsync(1, new DateTimeOffset(2026, 9, 1, 4, 10, 0, TimeSpan.Zero), default);
        published!.Revision.Should().Be(1);
        db.SchoolTimetables.Single().BellScheduleRevisionId.Should().Be(old.Id);
        db.SchoolTimetables.Single().TimingsRequireRevalidation.Should().BeFalse();
        db.SchoolTimetables.Single().Revision.Should().Be(1);
        db.TimetableSetupProfiles.Single().Revision.Should().Be(2);
        var stale = await handler.Handle(new(old.BellScheduleTemplateId, request), default);
        stale.IsSuccess.Should().BeFalse();
        stale.Errors.Should().Contain(TimetableSettingsHandlerSupport.ConcurrencyConflict);
    }

    [Fact]
    public async Task Day_overrides_are_isolated_and_holidays_and_gaps_resolve_to_null()
    {
        await using var db = Database();
        await BellScheduleTestData.SeedAsync(db);
        var request = Request() with { Days = Request().Days.Select(x => x.Day == 7
            ? x with { UsesDefaultSchedule = false, Periods = new[] { Period(1, "09:00", "09:25") } }
            : x.Day == 1 ? x with { IsStudyDay = false } : x).ToArray() };
        var result = await Handler(db).Handle(new(null, request), default);
        var schedule = result.Data!;
        BellScheduleResolver.EffectivePeriods(schedule, TimetableDay.Friday).Single().EndLocalTime.Should().Be(new TimeOnly(9, 25));
        BellScheduleResolver.EffectivePeriods(schedule, TimetableDay.Sunday).Single().EndLocalTime.Should().Be(new TimeOnly(7, 45));
        BellScheduleResolver.EffectivePeriods(schedule, TimetableDay.Saturday).Should().BeEmpty();
        // September is UTC+3 in Cairo; resolution must ignore the supplied offset.
        BellScheduleResolver.CurrentPeriod(schedule, new(2026, 9, 1, 4, 0, 0, TimeSpan.Zero))!.Sequence.Should().Be(1);
        BellScheduleResolver.CurrentPeriod(schedule, new(2026, 9, 1, 4, 45, 0, TimeSpan.Zero)).Should().BeNull();
        BellScheduleResolver.CurrentPeriod(schedule, new(2026, 9, 5, 4, 10, 0, TimeSpan.Zero)).Should().BeNull();
        BellScheduleResolver.CurrentPeriod(schedule, new(2026, 9, 4, 6, 10, 0, TimeSpan.Zero))!.Sequence.Should().Be(1);
    }

    [Fact]
    public async Task Scope_permissions_names_and_selection_are_enforced_in_handlers()
    {
        await using var db = Database();
        var revision = await BellScheduleTestData.SeedAsync(db);
        var request = Request();
        (await Handler(db, new User(2)).Handle(new(revision.BellScheduleTemplateId, request), default)).IsSuccess.Should().BeFalse();
        (await Handler(db, new User(1, false)).Handle(new(null, request), default)).IsSuccess.Should().BeFalse();
        (await Handler(db, new User(1, true, RoleNames.Instructor)).Handle(new(null, request), default)).IsSuccess.Should().BeFalse();
        var created = await Handler(db).Handle(new(null, request), default);
        created.IsSuccess.Should().BeTrue();
        (await Handler(db).Handle(new(null, request), default)).IsSuccess.Should().BeFalse();
        var selector = new SelectBellScheduleHandler(new BellScheduleRepository(db), new TimetableSettingsRepository(db), new User(1), TimeProvider.System);
        var profile = db.TimetableSetupProfiles.Single();
        (await selector.Handle(new(profile.Id, new(created.Data!.Id, profile.Revision)), default)).IsSuccess.Should().BeTrue();
        profile.BellScheduleTemplateId.Should().Be(created.Data.Id);
        (await selector.Handle(new(profile.Id, new(created.Data.Id, 1)), default)).IsSuccess.Should().BeFalse();
        (await new BellScheduleRepository(db).ListAsync(2, 1, TimetableSemester.First, default)).Should().BeEmpty();
    }

    [Fact]
    public async Task Persisted_revision_rows_cannot_be_modified()
    {
        await using var db = Database();
        var revision = await BellScheduleTestData.SeedAsync(db);
        revision.Days.Single(x => x.Day == 0).Periods.First().StartLocalTime = new(6, 0);
        await db.Invoking(x => x.SaveChangesAsync()).Should().ThrowAsync<InvalidOperationException>().WithMessage("*immutable*");
    }

    [Fact]
    public async Task Gate_pass_resolution_selects_the_exact_period_and_never_a_teacher_in_a_gap()
    {
        await using var db = Database();
        await BellScheduleTestData.SeedAsync(db, published: true);
        db.InstructorProfiles.Add(new InstructorProfile { Id = 10, SchoolId = 1, UserId = "teacher", IsActive = true });
        var timetable = db.SchoolTimetables.Single();
        foreach (var sequence in new[] { 1, 2 }) db.SchoolTimetableEntries.Add(new() {
            SchoolId = 1, SchoolTimetableId = timetable.Id, InstructorProfileId = 10, Day = TimetableDay.Tuesday,
            Period = sequence, EntryType = TimetableEntryType.Lesson, ClassroomId = 20, ClassLabel = "1/A", Subject = "Math" });
        await db.SaveChangesAsync();
        var repository = new GatePassWorkflowRepository(db);
        async Task<AlFalah.Application.StudentAffairs.GatePasses.GatePassTimetableSnapshot?> Resolve(int hour, int minute) =>
            await repository.ResolvePublishedTimetableAsync(1, 1, TimetableSemester.First, 20, "1/A",
                new DateTimeOffset(2026, 9, 1, hour, minute, 0, TimeSpan.Zero), default);
        (await Resolve(4, 10))!.Period.Should().Be(1);
        (await Resolve(4, 45)).Should().BeNull();
        (await Resolve(5, 10))!.Period.Should().Be(2);
        (await Resolve(5, 35)).Should().BeNull();
    }

    private static AlFalahDbContext Database() => new(new DbContextOptionsBuilder<AlFalahDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static SaveBellScheduleHandler Handler(AlFalahDbContext db, ICurrentUserService? user = null) =>
        new(new BellScheduleRepository(db), new TimetableSettingsRepository(db), user ?? new User(1), TimeProvider.System);
    private static BellPeriodDto Period(int sequence, string start, string end) => new(sequence, null, TimeOnly.Parse(start), TimeOnly.Parse(end));
    private static SaveBellScheduleRequest Request() => new(1, TimetableSemester.First, "قالب الاختبار", 0, "Africa/Cairo",
        new[] { Period(1, "07:00", "07:45") }, Enumerable.Range(1, 7).Select(day => new BellScheduleDayDto(day, true, true, Array.Empty<BellPeriodDto>())).ToArray());
    private sealed class User(int schoolId, bool manage = true, string role = RoleNames.Secretary) : ICurrentUserService
    {
        public string? UserId => "manager";
        public string? Username => "manager";
        public int? ActiveSchoolId => schoolId;
        public string? PreferredLanguage => "ar";
        public bool IsAuthenticated => true;
        public bool IsInRole(string name) => role == name;
        public bool HasPermission(string name) => name == PermissionNames.TimetableView || (manage && name == PermissionNames.TimetableManage);
        public IEnumerable<string> GetRoles() => new[] { role };
        public IEnumerable<string> GetPermissions() => new[] { PermissionNames.TimetableView };
        public bool IsGlobalAdmin() => false;
        public bool IsSchoolScopedRole() => true;
    }
}
