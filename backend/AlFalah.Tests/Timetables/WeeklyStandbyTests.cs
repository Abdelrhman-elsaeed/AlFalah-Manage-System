using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.IntelligentTimetable.DTOs;
using FluentAssertions;
using Xunit;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Application.Interfaces;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Tests.Timetables;

public sealed class WeeklyStandbyTests
{
    private static readonly WeeklyStandbyEngine Engine = new(new TimetableValidationEngine());
    [Fact]
    public void Fair_allocation_uses_an_eligible_free_teacher_without_changing_teaching()
    {
        var context = TimetableReviewPhase7Tests.Context();
        var engine = new WeeklyStandbyEngine(new TimetableValidationEngine());
        var request = new WeeklyStandbyRequest(1, "Fair", new(), []);
        var result = engine.Preview(context, request, []);
        result.Placements.Should().ContainSingle().Which.InstructorProfileId.Should().Be(2);
        result.Periods.Single(x => x.Day == 2 && x.Period == 1).Allocated.Should().Be(1);
        context.Timetable.Entries.Should().ContainSingle();
    }

    [Fact]
    public void A_teacher_moved_to_another_school_is_not_an_eligible_weekly_reserve()
    {
        var c = TimetableReviewPhase7Tests.Context(); c.Teachers[1].Instructor.SchoolId = 2;
        var result = Engine.Preview(c, new(1, "Fair", new(), []), []);
        result.Teachers.Should().ContainSingle(); result.Placements.Should().BeEmpty();
    }

    [Fact]
    public void Teaching_must_be_complete_and_current_before_either_mode()
    {
        var c = TimetableReviewPhase7Tests.Context();
        c.Requirements[0].IndividualPeriodCount = 2;
        foreach (var mode in new[] { "Fair", "Manual" })
            Engine.Invoking(x => x.Preview(c, new(1, mode, new(), []), [])).Should().Throw<ArgumentException>();
        c.Requirements[0].IndividualPeriodCount = 1;
        c.Timetable.TimingsRequireRevalidation = true;
        Engine.Inspect(c, new()).Ready.Should().BeFalse();
        c.Timetable.TimingsRequireRevalidation = false;
        c.Timetable.Entries.Clear();
        Engine.Inspect(c, new()).Ready.Should().BeFalse();
    }

    [Fact]
    public void Short_days_availability_and_teaching_define_real_capacity_not_a_quota()
    {
        var c = TimetableReviewPhase7Tests.Context();
        var sunday = c.Schedule!.Days.Single(x => x.Day == 2);
        sunday.UsesDefaultSchedule = false;
        sunday.Periods = c.Schedule.Days.First().Periods.Take(2).ToArray();
        c.Teachers[1].Slots.Add(new() { Day = 2, BellPeriodId = 2, IsAvailable = false, Period = sunday.Periods.Last() });
        var result = Engine.Preview(c, new(1, "Fair", new(2, [new(2, 2, 2)]), []), []);
        result.Periods.Where(x => x.Day == 2).Should().HaveCount(2);
        result.Placements.Where(x => x.Day == 2).Should().HaveCount(2);
        result.Placements.Should().Contain(x => x.InstructorProfileId == 2 && x.Period == 1);
        result.Placements.Should().Contain(x => x.InstructorProfileId == 1 && x.Period == 2);
        result.Placements.Should().NotContain(x => x.Day == 2 && x.Period > 2);
        result.Periods.Where(x => x.Day != 2).Should().OnlyContain(x => x.Required == 0 && x.Allocated == 0);
        result.Ready.Should().BeTrue("unfilled reserve demand does not invalidate complete teaching");
    }

    [Theory]
    [InlineData(1, 2, 1)] // occupied teaching
    [InlineData(2, 7, 1)] // non-study day
    [InlineData(2, 2, 9)] // outside real periods
    [InlineData(999, 2, 1)] // another school / missing profile
    public void Manual_rejects_impossible_cells(int teacher, int day, int period)
    {
        Engine.Invoking(x => x.Preview(TimetableReviewPhase7Tests.Context(),
            new(1, "Manual", new(), [new(teacher, day, period)]), [])).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Fair_distribution_balances_total_load_and_reroutes_for_constrained_periods()
    {
        var c = TimetableReviewPhase7Tests.Context();
        var overrides = Enumerable.Range(2, 7).Select(p => new StandbyDemand(2, p, 1)).ToArray();
        var result = Engine.Preview(c, new(1, "Fair", new(1, overrides), []), []);
        result.Placements.Should().HaveCount(8);
        var loads = result.Teachers.Select(t => t.TeachingCount + t.StandbyCount).ToArray();
        (loads.Max() - loads.Min()).Should().BeLessThanOrEqualTo(1);
        c.Teachers[1].Slots.Add(new() { Day = 2, BellPeriodId = 2, IsAvailable = false, Period = c.Schedule!.Days.First().Periods.ElementAt(1) });
        result = Engine.Preview(c, new(1, "Fair", new(1, [new(2, 2, 1)]), []), []);
        result.Placements.Should().Contain(x => x.InstructorProfileId == 2 && x.Period == 1);
        result.Placements.Should().Contain(x => x.InstructorProfileId == 1 && x.Period == 2);
    }

    [Fact]
    public void Locks_exclusions_and_confirmed_cover_are_respected()
    {
        var c = TimetableReviewPhase7Tests.Context();
        c.Timetable.Entries.Add(new() { Id = 2, SchoolId = 1, SchoolTimetableId = 1, InstructorProfileId = 2,
            Day = TimetableDay.Sunday, Period = 2, EntryType = TimetableEntryType.Standby });
        var legacy = Engine.Inspect(c, new()).Placements.Single();
        legacy.IsLocked.Should().BeTrue(); legacy.Source.Should().Be("Manual");
        var result = Engine.Preview(c, new(1, "Fair", new(), [legacy]), []);
        result.Placements.Should().Contain(legacy);
        Engine.Invoking(x => x.Preview(c, new(1, "Fair", new(), []), ["2:2:2"]))
            .Should().Throw<ArgumentException>();
        Engine.Invoking(x => x.Preview(c, new(1, "Fair", new(), [legacy with { IsLocked = false }]), ["2:2:2"]))
            .Should().Throw<ArgumentException>();
        result = Engine.Preview(c, new(1, "Fair", new(1, ExcludedTeacherIds: [2]), []), []);
        result.Placements.Should().BeEmpty(); result.Periods.First().Required.Should().Be(1);
    }

    [Fact]
    public void Duplicate_cells_stale_revision_invalid_demand_and_cancellation_are_rejected()
    {
        var c = TimetableReviewPhase7Tests.Context();
        Engine.Invoking(x => x.Preview(c, new(0, "Fair", new(), []), [])).Should().Throw<BellScheduleConflictException>();
        Engine.Invoking(x => x.Preview(c, new(1, "Manual", new(), [new(2, 2, 1), new(2, 2, 1)]), [])).Should().Throw<ArgumentException>();
        Engine.Invoking(x => x.Preview(c, new(1, "Fair", new(3), []), [])).Should().Throw<ArgumentException>();
        Engine.Invoking(x => x.Preview(c, new(1, "Fair", new(), []), [], new(true))).Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public async Task Reviewed_apply_preserves_teaching_ids_versions_metadata_and_rejects_replay_and_tamper()
    {
        await using var db = await TimetableReviewPhase7Tests.Seed();
        var service = Service(db);
        var request = new WeeklyStandbyRequest(1, "Fair", new(), []);
        var preview = await service.PreviewAsync(1, request, default);
        (await db.SchoolTimetableEntries.CountAsync()).Should().Be(1, "preview is read-only");
        await service.Invoking(x => x.ApplyAsync(1, request with { ProposalId = "forged" }, default)).Should().ThrowAsync<BellScheduleConflictException>();
        var result = await service.ApplyAsync(1, request with { ProposalId = preview.ProposalId }, default);
        result.Revision.Should().Be(2); result.IsPublished.Should().BeFalse();
        var lesson = await db.SchoolTimetableEntries.SingleAsync(x => x.EntryType == TimetableEntryType.Lesson);
        lesson.Id.Should().Be(1); lesson.InstructorProfileId.Should().Be(1);
        var version = await db.SchoolTimetableVersions.SingleAsync();
        version.SnapshotJson.Should().Contain("Fair").And.Contain("StandbySettingsJson");
        (await db.AuditLogs.CountAsync(x => x.Action == "Timetable.WeeklyStandby.Saved")).Should().Be(1);
        await service.Invoking(x => x.ApplyAsync(1, request with { ProposalId = preview.ProposalId }, default)).Should().ThrowAsync<BellScheduleConflictException>();
        var next = new WeeklyStandbyRequest(2, "Manual", new(), result.Placements.Select(p => p with { IsLocked = true }).ToArray());
        preview = await service.PreviewAsync(1, next, default);
        await service.ApplyAsync(1, next with { ProposalId = preview.ProposalId }, default);
        (await db.SchoolTimetableEntries.SingleAsync(x => x.EntryType == TimetableEntryType.Standby)).IsStandbyLocked.Should().BeTrue();
    }

    [Fact]
    public async Task School_scope_and_delegated_editor_permissions_are_enforced()
    {
        await using var db = await TimetableReviewPhase7Tests.Seed();
        foreach (var role in new[] { RoleNames.Instructor, RoleNames.Guardian, RoleNames.Secretary, RoleNames.Moderator })
            await Service(db, new(Role: role)).Invoking(x => x.GetAsync(1, default)).Should().ThrowAsync<UnauthorizedAccessException>();
        await Service(db, new(School: 2)).Invoking(x => x.GetAsync(1, default)).Should().ThrowAsync<KeyNotFoundException>();
        db.TimetableEditorGrants.Add(new() { SchoolId = 1, ModeratorUserId = "manager", GrantedByUserId = "manager" });
        await db.SaveChangesAsync();
        (await Service(db, new(Role: RoleNames.Moderator)).GetAsync(1, default)).Ready.Should().BeTrue();
        (await Service(db, new(Role: RoleNames.Secretary, Manage: true)).GetAsync(1, default)).Ready.Should().BeTrue();
    }

    internal static WeeklyStandbyService Service(AlFalahDbContext db, User? user = null)
    {
        var repository = new WeeklyStandbyRepository(db); var validator = new TimetableValidationEngine();
        return new(repository, new(validator), new(repository, validator, new(validator), user ?? new()), user ?? new(), TimeProvider.System);
    }
    internal sealed record User(int School = 1, string Role = RoleNames.SchoolManager, bool Manage = false) : ICurrentUserService
    {
        public string? UserId => "manager"; public string? Username => "manager"; public int? ActiveSchoolId => School;
        public string? PreferredLanguage => "ar"; public bool IsAuthenticated => true;
        public bool IsInRole(string role) => Role == role;
        public bool HasPermission(string permission) => Manage && permission == PermissionNames.TimetableManage;
        public IEnumerable<string> GetRoles() => [Role]; public IEnumerable<string> GetPermissions() => [];
        public bool IsGlobalAdmin() => false; public bool IsSchoolScopedRole() => true;
    }
}
