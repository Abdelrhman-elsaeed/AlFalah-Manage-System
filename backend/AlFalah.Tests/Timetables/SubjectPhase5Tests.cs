using AlFalah.Application.DTOs.Timetables;
using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.IntelligentTimetable.Handlers;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace AlFalah.Tests.Timetables;

public sealed class SubjectPhase5Tests
{
    [Fact]
    public async Task Bulk_preserves_customizations_until_explicit_overwrite_and_rejects_stale_revisions()
    {
        await using var db = await Seed(); var h = Handler(db);
        (await h.Handle(new AllocateSubjectToClassesCommand(1, Request(2, 1)), default)).IsSuccess.Should().BeTrue();
        var saved = (await h.Handle(new GetSubjectsQuery(1), default)).Data!.Requirements.Single();
        var bulk = Request(4, 1, 2);
        var result = (await h.Handle(new AllocateSubjectToClassesCommand(1, bulk), default)).Data!;
        result.Results.Select(x => x.Status).Should().Equal("Skipped", "Created");
        db.Set<ClassSubjectRequirement>().Single(x => x.ClassroomId == 1).TotalWeeklyPeriods.Should().Be(2);
        var overwrite = bulk with { OverwriteExisting = true, Classes = [new(1, saved.Revision), new(2, 1)] };
        (await h.Handle(new AllocateSubjectToClassesCommand(1, overwrite), default)).IsSuccess.Should().BeTrue();
        (await h.Handle(new AllocateSubjectToClassesCommand(1, overwrite), default)).Errors.Should().Contain(TimetableSettingsHandlerSupport.ConcurrencyConflict);
        db.ChangeTracker.Clear();
        db.Set<ClassSubjectRequirement>().Should().HaveCount(2).And.OnlyContain(r => r.TotalWeeklyPeriods == 4);
        db.AuditLogs.Should().HaveCount(3);
    }
    [Fact]
    public async Task Paired_blocks_cannot_cross_gaps_but_early_preference_does_not_limit_weekly_capacity()
    {
        await using var db = await Seed(); var h = Handler(db);
        var pair = Request(0, 1) with { Rules = Rules(0) with { PairedBlockCount = 1 } };
        (await h.Handle(new AllocateSubjectToClassesCommand(1, pair), default)).IsSuccess.Should().BeFalse();
        var early = Request(8, 1) with { Rules = Rules(8) with { TimePreference = "Early", EarliestPeriodSequence = 1, LatestPreferredPeriodSequence = 1 } };
        (await h.Handle(new AllocateSubjectToClassesCommand(1, early), default)).IsSuccess.Should().BeTrue();
        SubjectSchedulingPolicy.PreferencePenalty(early.Rules, 2).Should().BeGreaterThan(0);
        var schedule = (await h.Handle(new GetSubjectsQuery(1), default)).Data!.Schedule!;
        var adjacent = schedule with { DefaultPeriods = [schedule.DefaultPeriods[0], schedule.DefaultPeriods[1] with { StartLocalTime = new(7,45) }] };
        var act = () => SubjectSchedulingPolicy.Validate(pair.Rules, adjacent);
        act.Should().NotThrow();
    }
    [Fact]
    public async Task Rejects_foreign_scope_invalid_counts_days_rooms_and_fixed_slots_without_partial_writes()
    {
        await using var db = await Seed(); var h = Handler(db);
        foreach (var invalid in new[] {
            Request(1, 1, 999), Request(0, 1), Request(-1, 1), Request(13, 1),
            Request(1, 1) with { SubjectId = 999 }, Request(1, 1) with { Rules = Rules(1) with { RoomIds = [999] } },
            Request(1, 1) with { Rules = Rules(1) with { AllowedDays = [7] } },
            Request(1, 1) with { Rules = Rules(1) with { FixedSlots = [new(1, 99)] } },
            Request(1, 1) with { Rules = Rules(1) with { RoomIds = [1], PreferredRoomId = 2 } }
        }) (await h.Handle(new AllocateSubjectToClassesCommand(1, invalid), default)).IsSuccess.Should().BeFalse();
        db.Set<ClassSubjectRequirement>().Should().BeEmpty();
        (await Handler(db, new User(2)).Handle(new GetSubjectsQuery(1), default)).IsSuccess.Should().BeFalse();
    }
    [Theory]
    [InlineData(RoleNames.Instructor, true)] [InlineData(RoleNames.Guardian, true)] [InlineData(RoleNames.Secretary, false)]
    public async Task Manage_permission_is_required_for_reads_and_writes(string role, bool manage)
    {
        await using var db = await Seed(); var h = Handler(db, new User(1, role, manage));
        (await h.Handle(new GetSubjectsQuery(1), default)).Errors.Should().Contain(TimetableSettingsHandlerSupport.PermissionDenied);
        (await h.Handle(new CreateSubjectCommand(1, new("علوم", "#ffffff")), default)).Errors.Should().Contain(TimetableSettingsHandlerSupport.PermissionDenied);
    }
    [Fact]
    public async Task Persists_room_alternatives_and_reconciles_children_and_marks_existing_timetable_stale()
    {
        await using var db = await Seed(); var h = Handler(db);
        db.SchoolTimetables.Add(new() { Id = 1, SchoolId = 1, TimetableSetupProfileId = 1, AcademicYearId = 1, Semester = TimetableSemester.First });
        await db.SaveChangesAsync();
        var request = Request(2, 1) with { Rules = Rules(2) with { AllowedDays = [1, 2], RoomIds = [1, 2], PreferredRoomId = 1, FixedSlots = [new(1, 1)] } };
        (await h.Handle(new AllocateSubjectToClassesCommand(1, request), default)).IsSuccess.Should().BeTrue();
        db.SchoolTimetables.Single().TimingsRequireRevalidation.Should().BeTrue();
        db.ChangeTracker.Clear();
        var saved = (await h.Handle(new GetSubjectsQuery(1), default)).Data!.Requirements.Single();
        (await h.Handle(new UpdateSubjectRequirementsCommand(1, saved.Id, new(saved.Revision,
            saved.Rules with { AllowedDays = [2], FixedSlots = [], RoomIds = [2], PreferredRoomId = 2 })), default)).IsSuccess.Should().BeTrue();
        db.ChangeTracker.Clear();
        var latest = (await h.Handle(new GetSubjectsQuery(1), default)).Data!.Requirements.Single();
        latest.Rules.RoomIds.Should().Equal(2); latest.Rules.AllowedDays.Should().Equal(2); latest.Rules.FixedSlots.Should().BeEmpty();
    }
    [Fact]
    public async Task Manual_placements_obey_rooms_days_and_publish_quotas_while_early_is_soft()
    {
        await using var db = await Seed(); var h = Handler(db);
        var request = Request(1, 1) with { Rules = Rules(1) with { RoomIds = [1, 2], PreferredRoomId = 1, AllowedDays = [1], TimePreference = "Early", EarliestPeriodSequence = 1, LatestPreferredPeriodSequence = 1 } };
        (await h.Handle(new AllocateSubjectToClassesCommand(1, request), default)).IsSuccess.Should().BeTrue();
        var schedule = (await h.Handle(new GetSubjectsQuery(1), default)).Data!.Schedule!;
        var service = new SubjectAssignmentService(new SubjectRepository(db));
        var timetable = new SchoolTimetable { SchoolId = 1, TimetableSetupProfileId = 1 };
        var late = new TimetableEntryDto(1, TimetableDay.Saturday, 2, TimetableEntryType.Lesson, "1/A", "رياضيات");
        var linked = await service.ValidateAsync(timetable, [late], schedule, true, default);
        linked.Single().RoomId.Should().Be(1); linked.Single().SubjectId.Should().Be(1);
        await service.Invoking(s => s.ValidateAsync(timetable, [late with { Day = TimetableDay.Sunday }], schedule, false, default)).Should().ThrowAsync<ArgumentException>();
        await service.Invoking(s => s.ValidateAsync(timetable, [late with { RoomId = 999 }], schedule, false, default)).Should().ThrowAsync<ArgumentException>();
        await service.Invoking(s => s.ValidateAsync(timetable, [], schedule, true, default)).Should().ThrowAsync<ArgumentException>();
    }
    [Fact]
    public async Task Removal_is_class_scoped_and_historical_links_block_removal()
    {
        await using var db = await Seed(); var h = Handler(db);
        await h.Handle(new AllocateSubjectToClassesCommand(1, Request(1, 1, 2)), default);
        var rows = (await h.Handle(new GetSubjectsQuery(1), default)).Data!.Requirements;
        db.SchoolTimetableEntries.Add(new() { SchoolId = 1, ClassSubjectRequirementId = rows[0].Id }); await db.SaveChangesAsync();
        (await h.Handle(new RemoveSubjectRequirementCommand(1, rows[0].Id, rows[0].Revision), default)).IsSuccess.Should().BeFalse();
        (await h.Handle(new RemoveSubjectRequirementCommand(1, rows[1].Id, rows[1].Revision), default)).IsSuccess.Should().BeTrue();
        db.Set<ClassSubjectRequirement>().Should().ContainSingle(); db.Set<SubjectDefinition>().Should().ContainSingle();
    }
    [Fact]
    public async Task Concurrent_bulk_requests_cannot_overwrite_rules_seen_before_another_save()
    {
        await using var db = await Seed(); await Handler(db).Handle(new AllocateSubjectToClassesCommand(1, Request(1, 1)), default);
        var options = (DbContextOptions<AlFalahDbContext>)db.GetService<IDbContextOptions>(); await using var other = new AlFalahDbContext(options);
        var stale = Handler(db); await stale.Handle(new GetSubjectsQuery(1), default);
        var replace = Request(2, 1) with { OverwriteExisting = true, Classes = [new(1, 1)] };
        (await Handler(other).Handle(new AllocateSubjectToClassesCommand(1, replace), default)).IsSuccess.Should().BeTrue();
        (await stale.Handle(new AllocateSubjectToClassesCommand(1, replace with { Rules = Rules(3) }), default)).Errors.Should().Contain(TimetableSettingsHandlerSupport.ConcurrencyConflict);
    }
    private static SubjectRulesRequest Rules(int individual) => new(individual, 0, "None", null, null, [], [], [], null);
    private static AllocateSubjectRequest Request(int individual, params int[] classes) => new(1, classes.Select(c => new SubjectClassTarget(c, 0)).ToArray(), Rules(individual));
    private static SubjectHandlers Handler(AlFalahDbContext db, ICurrentUserService? user = null) => new(new SubjectService(new SubjectRepository(db), new BellScheduleRepository(db)), user ?? new User(1));
    private static async Task<AlFalahDbContext> Seed()
    {
        var db = new AlFalahDbContext(new DbContextOptionsBuilder<AlFalahDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await BellScheduleTestData.SeedAsync(db);
        db.Add(new SubjectDefinition { Id = 1, SchoolId = 1, Name = "رياضيات" });
        db.AddRange(new TimetableRoom { Id = 1, SchoolId = 1, Name = "معمل 1" }, new TimetableRoom { Id = 2, SchoolId = 1, Name = "معمل 2" });
        db.Classrooms.AddRange(new Classroom { Id = 1, SchoolId = 1, AcademicYearId = 1, ClassLabel = "1/A", IsActive = true },
            new Classroom { Id = 2, SchoolId = 1, AcademicYearId = 1, ClassLabel = "1/B", IsActive = true });
        await db.SaveChangesAsync(); return db;
    }
    private sealed class User(int school, string role = RoleNames.Secretary, bool manage = true) : ICurrentUserService
    {
        public string? UserId => "manager"; public string? Username => "manager"; public int? ActiveSchoolId => school;
        public string? PreferredLanguage => "ar"; public bool IsAuthenticated => true;
        public bool IsInRole(string name) => role == name; public bool HasPermission(string name) => manage && name == PermissionNames.TimetableManage;
        public IEnumerable<string> GetRoles() => [role]; public IEnumerable<string> GetPermissions() => [];
        public bool IsGlobalAdmin() => false; public bool IsSchoolScopedRole() => true;
    }
}
