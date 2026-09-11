using AlFalah.Application.DTOs.Timetables;
using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.IntelligentTimetable.Handlers;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace AlFalah.Tests.Timetables;

public sealed class TeacherAvailabilityPhase4Tests
{
    [Fact]
    public async Task First_load_defaults_to_all_effective_periods_without_writing_a_profile()
    {
        await using var db = await Seed();
        var dto = (await Handler(db).Handle(new GetTeacherAvailabilityQuery(1, 10), default)).Data!;
        dto.Slots.Should().HaveCount(12).And.OnlyContain(x => x.IsAvailable && x.Day != 7);
        dto.MaximumWeeklyPeriods.Should().Be(12);
        dto.Revision.Should().Be(0);
        db.TeacherTimetableProfiles.Should().BeEmpty();
    }

    [Fact]
    public async Task Saves_profile_flags_and_bulk_grid_atomically_with_audit_and_rejects_stale_revision()
    {
        await using var db = await Seed();
        var handler = Handler(db);
        var dto = (await handler.Handle(new GetTeacherAvailabilityQuery(1, 10), default)).Data!;
        var request = Request(dto) with { MaximumWeeklyPeriods = 8, ShortDisplayName = "  أحمد  ", IsVisiting = true, HideFromPrint = true,
            Slots = dto.Slots.Select(x => new AvailabilitySlotRequest(x.Day, x.BellPeriodId, x.Day != 1)).ToArray() };
        var result = await handler.Handle(new UpdateTeacherProfileCommand(1, 10, request), default);
        result.IsSuccess.Should().BeTrue();
        result.Data!.ShortDisplayName.Should().Be("أحمد");
        result.Data.IsVisiting.Should().BeTrue(); result.Data.HideFromPrint.Should().BeTrue();
        result.Data.Slots.Count(x => !x.IsAvailable).Should().Be(2);
        db.AuditLogs.Should().ContainSingle(x => x.Action == "Timetable.TeacherAvailability.Updated");
        (await handler.Handle(new UpdateTeacherProfileCommand(1, 10, request), default)).Errors
            .Should().Contain(TimetableSettingsHandlerSupport.ConcurrencyConflict);
        db.ChangeTracker.Clear();
        (await Handler(db).Handle(new GetTeacherAvailabilityQuery(1, 10), default)).Data!.Slots.Count(x => !x.IsAvailable).Should().Be(2);
    }

    [Theory]
    [InlineData(RoleNames.Instructor, true)]
    [InlineData(RoleNames.Guardian, true)]
    [InlineData(RoleNames.Secretary, false)]
    public async Task Forbidden_roles_and_view_only_users_cannot_read_or_edit(string role, bool manage)
    {
        await using var db = await Seed();
        var handler = Handler(db, new User(1, role, manage));
        (await handler.Handle(new GetTeacherAvailabilityQuery(1, 10), default)).Errors.Should().Contain(TimetableSettingsHandlerSupport.PermissionDenied);
        (await handler.Handle(new UpdateTeacherProfileCommand(1, 10, new(0, 1, "", 0, false, false, false, [])), default))
            .Errors.Should().Contain(TimetableSettingsHandlerSupport.PermissionDenied);
    }

    [Fact]
    public async Task Rejects_other_schools_inactive_teachers_negative_load_duplicate_and_foreign_cells()
    {
        await using var db = await Seed();
        (await Handler(db, new User(2)).Handle(new GetTeacherAvailabilityQuery(1, 10), default)).IsSuccess.Should().BeFalse();
        var handler = Handler(db);
        var dto = (await handler.Handle(new GetTeacherAvailabilityQuery(1, 10), default)).Data!;
        var valid = Request(dto);
        foreach (var invalid in new[] {
            valid with { MaximumWeeklyPeriods = -1 }, valid with { MaximumWeeklyPeriods = 13 },
            valid with { Slots = valid.Slots.Concat(new[] { valid.Slots[0] }).ToArray() },
            valid with { Slots = valid.Slots.Select((s, i) => i == 0 ? s with { BellPeriodId = 999 } : s).ToArray() },
            valid with { MaximumWeeklyPeriods = 1, Slots = valid.Slots.Select(s => s with { IsAvailable = false }).ToArray() }
        }) (await handler.Handle(new UpdateTeacherProfileCommand(1, 10, invalid), default)).IsSuccess.Should().BeFalse();
        db.TeacherTimetableProfiles.Should().BeEmpty();
        db.InstructorProfiles.Single().IsActive = false; await db.SaveChangesAsync();
        (await handler.Handle(new GetAvailabilityTeachersQuery(1), default)).Data.Should().BeEmpty();
        (await handler.Handle(new UpdateTeacherProfileCommand(1, 10, valid), default)).IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Zero_is_a_hard_limit_and_unavailability_blocks_lessons_and_standby()
    {
        await using var db = await Seed();
        var handler = Handler(db);
        var dto = (await handler.Handle(new GetTeacherAvailabilityQuery(1, 10), default)).Data!;
        var saved = (await handler.Handle(new UpdateTeacherProfileCommand(1, 10, Request(dto) with { MaximumWeeklyPeriods = 0 }), default)).Data!;
        var timetable = new SchoolTimetable { SchoolId = 1, TimetableSetupProfileId = 1, BellScheduleRevisionId = dto.BellScheduleRevisionId };
        var service = new TeacherAvailabilityService(new TeacherAvailabilityRepository(db));
        await service.Invoking(x => x.ValidateAssignmentsAsync(timetable, new[] { Lesson(1) }, default)).Should().ThrowAsync<ArgumentException>().WithMessage("*الحد الأسبوعي*");
        saved = (await handler.Handle(new UpdateTeacherProfileCommand(1, 10, Request(saved) with { MaximumWeeklyPeriods = 1,
            Slots = saved.Slots.Select(s => new AvailabilitySlotRequest(s.Day, s.BellPeriodId, s.Day != 1)).ToArray() }), default)).Data!;
        foreach (var type in new[] { TimetableEntryType.Lesson, TimetableEntryType.Standby })
            await service.Invoking(x => x.ValidateAssignmentsAsync(timetable, new[] { Lesson(1) with { EntryType = type } }, default))
                .Should().ThrowAsync<ArgumentException>().WithMessage("*غير متاح*");
        await service.Invoking(x => x.ValidateAssignmentsAsync(timetable, new[] { Lesson(2), Lesson(3) }, default))
            .Should().ThrowAsync<ArgumentException>().WithMessage("*الحد الأسبوعي*");
        await service.ValidateAssignmentsAsync(timetable, new[] { Lesson(2) }, default);
    }

    [Fact]
    public async Task Closing_assigned_cell_keeps_lesson_and_reports_violation_but_lowering_load_is_rejected()
    {
        await using var db = await Seed();
        db.SchoolTimetables.Add(new() { Id = 1, SchoolId = 1, TimetableSetupProfileId = 1, AcademicYearId = 1, Semester = TimetableSemester.First });
        db.SchoolTimetableEntries.Add(new() { SchoolId = 1, SchoolTimetableId = 1, InstructorProfileId = 10,
            Day = TimetableDay.Saturday, Period = 1, EntryType = TimetableEntryType.Lesson });
        await db.SaveChangesAsync();
        var handler = Handler(db);
        var dto = (await handler.Handle(new GetTeacherAvailabilityQuery(1, 10), default)).Data!;
        dto.AllocatedPeriods.Should().Be(1);
        (await handler.Handle(new UpdateTeacherProfileCommand(1, 10, Request(dto) with { MaximumWeeklyPeriods = 0 }), default)).IsSuccess.Should().BeFalse();
        var result = await handler.Handle(new UpdateTeacherProfileCommand(1, 10, Request(dto) with { MaximumWeeklyPeriods = 1,
            Slots = dto.Slots.Select(s => new AvailabilitySlotRequest(s.Day, s.BellPeriodId, s.Day != 1)).ToArray() }), default);
        result.IsSuccess.Should().BeTrue(); result.Data!.Violations.Should().NotBeEmpty();
        db.SchoolTimetableEntries.Should().ContainSingle();
    }

    [Fact]
    public async Task Timing_changes_preserve_exact_times_only_and_require_explicit_review()
    {
        await using var db = await Seed();
        var handler = Handler(db);
        var dto = (await handler.Handle(new GetTeacherAvailabilityQuery(1, 10), default)).Data!;
        var saved = (await handler.Handle(new UpdateTeacherProfileCommand(1, 10, Request(dto) with { MaximumWeeklyPeriods = 1,
            Slots = dto.Slots.Select(s => new AvailabilitySlotRequest(s.Day, s.BellPeriodId, s.Sequence != 1)).ToArray() }), default)).Data!;
        var template = db.Set<BellScheduleTemplate>().Single(); template.Revision++;
        var revision = new BellScheduleRevision { SchoolId = 1, BellScheduleTemplateId = template.Id, Revision = 2, Name = "جديد", SchoolTimeZoneId = "Africa/Cairo" };
        revision.Days.Add(new() { Day = 0, IsStudyDay = true, Periods = new List<BellPeriod> {
            new() { Sequence = 1, StartLocalTime = new(6, 0), EndLocalTime = new(6, 45) },
            new() { Sequence = 2, StartLocalTime = new(7, 0), EndLocalTime = new(7, 45) } } });
        revision.Days.Add(new() { Day = 1, IsStudyDay = true, UsesDefaultSchedule = true });
        db.Add(revision); await db.SaveChangesAsync();
        var changed = (await handler.Handle(new GetTeacherAvailabilityQuery(1, 10), default)).Data!;
        changed.RequiresScheduleReview.Should().BeTrue(); changed.OrphanedSlots.Should().NotBeEmpty();
        changed.Slots.Single(s => s.Sequence == 1).IsAvailable.Should().BeTrue();
        changed.Slots.Single(s => s.Sequence == 2).IsAvailable.Should().BeFalse();
        (await handler.Handle(new UpdateTeacherProfileCommand(1, 10, Request(changed)), default)).IsSuccess.Should().BeFalse();
        (await handler.Handle(new UpdateTeacherProfileCommand(1, 10, Request(changed) with { ConfirmScheduleReview = true }), default))
            .Data!.RequiresScheduleReview.Should().BeFalse();
        var audit = db.AuditLogs.OrderBy(x => x.Id).Last();
        using var before = System.Text.Json.JsonDocument.Parse(audit.OldValues!);
        before.RootElement.GetProperty("BellScheduleRevisionId").GetInt32().Should().Be(saved.BellScheduleRevisionId);
    }

    [Fact]
    public async Task Concurrent_first_profile_creation_conflicts_with_a_prevalidated_assignment()
    {
        await using var db = await Seed();
        var options = (DbContextOptions<AlFalahDbContext>)db.GetService<IDbContextOptions>();
        await using var other = new AlFalahDbContext(options);
        var dto = (await Handler(db).Handle(new GetTeacherAvailabilityQuery(1, 10), default)).Data!;
        var service = new TeacherAvailabilityService(new TeacherAvailabilityRepository(db));
        await service.ValidateAssignmentsAsync(new() { SchoolId = 1, TimetableSetupProfileId = 1,
            BellScheduleRevisionId = dto.BellScheduleRevisionId }, new[] { Lesson(1) }, default);
        var latest = (await Handler(other).Handle(new GetTeacherAvailabilityQuery(1, 10), default)).Data!;
        (await Handler(other).Handle(new UpdateTeacherProfileCommand(1, 10, Request(latest) with { MaximumWeeklyPeriods = 0 }), default)).IsSuccess.Should().BeTrue();
        await db.Invoking(x => x.SaveChangesAsync()).Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    private static TimetableEntryDto Lesson(int day) => new(10, (TimetableDay)day, 1, TimetableEntryType.Lesson, "1/A", "رياضيات");
    private static UpdateTeacherProfileRequest Request(TeacherAvailabilityDto d) => new(d.Revision, d.BellScheduleRevisionId,
        d.ShortDisplayName, d.MaximumWeeklyPeriods, d.IsVisiting, d.HideFromPrint, false,
        d.Slots.Select(s => new AvailabilitySlotRequest(s.Day, s.BellPeriodId, s.IsAvailable)).ToArray());
    private static TeacherAvailabilityHandlers Handler(AlFalahDbContext db, ICurrentUserService? user = null) {
        var repo = new TeacherAvailabilityRepository(db);
        return new(repo, new TeacherAvailabilityService(repo), user ?? new User(1), TimeProvider.System);
    }
    private static async Task<AlFalahDbContext> Seed() {
        var db = new AlFalahDbContext(new DbContextOptionsBuilder<AlFalahDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Users.Add(new() { Id = "teacher", FirstName = "أحمد", LastName = "محمد", IsActive = true });
        db.InstructorProfiles.Add(new() { Id = 10, SchoolId = 1, UserId = "teacher", IsActive = true });
        await BellScheduleTestData.SeedAsync(db);
        return db;
    }
    private sealed class User(int school, string role = RoleNames.Secretary, bool manage = true) : ICurrentUserService {
        public string? UserId => "manager";
        public string? Username => "manager";
        public int? ActiveSchoolId => school;
        public string? PreferredLanguage => "ar";
        public bool IsAuthenticated => true;
        public bool IsInRole(string name) => role == name;
        public bool HasPermission(string name) => name == PermissionNames.TimetableView || manage && name == PermissionNames.TimetableManage;
        public IEnumerable<string> GetRoles() => new[] { role };
        public IEnumerable<string> GetPermissions() => new[] { PermissionNames.TimetableView };
        public bool IsGlobalAdmin() => false;
        public bool IsSchoolScopedRole() => true;
    }
}
