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

public sealed class TeachingAssignmentPhase6Tests
{
    [Fact]
    public async Task Single_assignment_load_audit_unassign_and_reassign_are_persisted()
    {
        await using var db = await Seed(); var service = Service(db);
        var saved = await service.SaveAsync(1, 1, new(1, [Cell(1, 1)]), "manager", default);
        saved.Teachers.Single(t => t.Id == 1).Should().Match<TeachingTeacherDto>(t => t.AllocatedPeriods == 4 && t.RemainingPeriods == 2 && t.SubjectCount == 1 && t.ClassroomCount == 1);
        db.ChangeTracker.Clear();
        var cleared = await service.SaveAsync(1, 1, new(saved.Revision, [new(1, "SingleTeacher", [])]), "manager", default);
        cleared.Assignments.Should().BeEmpty(); cleared.Teachers.Single(t => t.Id == 1).AllocatedPeriods.Should().Be(0);
        await service.SaveAsync(1, 1, new(cleared.Revision, [Cell(1, 2)]), "manager", default);
        db.ChangeTracker.Clear();
        (await service.GetAsync(1, 1, default)).Assignments.Single().Members.Single().TeacherTimetableProfileId.Should().Be(2);
        db.AuditLogs.Should().HaveCount(3); db.AuditLogs.OrderBy(a => a.Id).Last().OldValues.Should().NotBeNull();
        db.Set<TeachingAssignment>().IgnoreQueryFilters().Should().HaveCount(2);
    }
    [Fact]
    public async Task Batch_capacity_is_school_setup_wide_and_failed_validation_writes_nothing()
    {
        await using var db = await Seed(); var service = Service(db);
        await service.Invoking(s => s.SaveAsync(1, 1, new(1, [Cell(1, 1), Cell(2, 1)]), "manager", default)).Should().ThrowAsync<ArgumentException>().WithMessage("*8*6*");
        db.Set<TeachingAssignment>().Should().BeEmpty(); db.AuditLogs.Should().BeEmpty();
        var saved = await service.SaveAsync(1, 1, new(1, [Cell(1, 1), Cell(2, 2)]), "manager", default);
        var swapped = await service.SaveAsync(1, 1, new(saved.Revision, [Cell(1, 2), Cell(2, 1)]), "manager", default);
        swapped.Teachers.Should().OnlyContain(t => t.AllocatedPeriods == 4);
    }
    [Fact]
    public async Task Co_teaching_charges_full_quota_to_every_member_and_split_owns_whole_pairs()
    {
        await using var db = await Seed(); var service = Service(db);
        var co = new TeachingCellRequest(1, "CoTeaching", [new(1, 4, 1), new(2, 4, 1)]);
        var saved = await service.SaveAsync(1, 1, new(1, [co]), "manager", default);
        saved.Teachers.Should().OnlyContain(t => t.AllocatedPeriods == 4);
        var split = new TeachingCellRequest(1, "SplitQuota", [new(1, 3, 1), new(2, 1, 0)]);
        var divided = await service.SaveAsync(1, 1, new(saved.Revision, [split]), "manager", default);
        divided.Teachers.Single(t => t.Id == 1).AllocatedPeriods.Should().Be(3);
        await service.Invoking(s => s.SaveAsync(1, 1, new(divided.Revision,
            [split with { Members = [new(1, 1, 1), new(2, 3, 0)] }]), "manager", default)).Should().ThrowAsync<ArgumentException>();
    }
    [Fact]
    public void A_double_period_cannot_be_split_into_one_period_per_teacher()
    {
        var requirement = new ClassSubjectRequirement { PairedBlockCount = 1 };
        var act = () => TeachingAllocationPolicy.Validate(requirement, new(1, "SplitQuota", [new(1, 1, 0), new(2, 1, 0)]));
        act.Should().Throw<ArgumentException>();
        var valid = () => TeachingAllocationPolicy.Validate(new() { IndividualPeriodCount = 1, PairedBlockCount = 2 },
            new(1, "SplitQuota", [new(1, 3, 1), new(2, 2, 1)]));
        valid.Should().NotThrow();
    }
    [Theory]
    [InlineData("SingleTeacher", 2)] [InlineData("SplitQuota", 1)] [InlineData("CoTeaching", 1)] [InlineData("Invalid", 1)]
    public void Invalid_modes_and_member_counts_are_rejected(string mode, int count)
    {
        var act = () => TeachingAllocationPolicy.Validate(new() { IndividualPeriodCount = 4 },
            new(1, mode, Enumerable.Range(1, count).Select(i => new TeachingMemberRequest(i, 4, 0)).ToArray()));
        act.Should().Throw<ArgumentException>();
    }
    [Fact]
    public async Task Rejects_duplicate_foreign_inactive_and_stale_requests()
    {
        await using var db = await Seed(); var service = Service(db);
        var inactive = db.InstructorProfiles.Single(x => x.Id == 2); inactive.IsActive = false; await db.SaveChangesAsync();
        foreach (var cell in new[] { Cell(999, 1), Cell(1, 999), Cell(1, 2), new TeachingCellRequest(1, "CoTeaching", [new(1, 4, 1), new(1, 4, 1)]) })
            await service.Invoking(s => s.SaveAsync(1, 1, new(1, [cell]), "manager", default)).Should().ThrowAsync<ArgumentException>();
        await service.Invoking(s => s.GetAsync(2, 1, default)).Should().ThrowAsync<KeyNotFoundException>();
        await service.SaveAsync(1, 1, new(1, [Cell(1, 1)]), "manager", default);
        await service.Invoking(s => s.SaveAsync(1, 1, new(1, [Cell(2, 1)]), "manager", default)).Should().ThrowAsync<BellScheduleConflictException>();
    }
    [Theory]
    [InlineData(RoleNames.Secretary, false)] [InlineData(RoleNames.Instructor, true)] [InlineData(RoleNames.Guardian, true)]
    public async Task Manage_permission_and_role_exclusions_apply_to_queries_and_commands(string role, bool manage)
    {
        await using var db = await Seed(); var handler = new TeachingAssignmentHandlers(Service(db), new User(role, manage));
        (await handler.Handle(new GetTeachingAssignmentsQuery(1), default)).Errors.Should().Contain(TimetableSettingsHandlerSupport.PermissionDenied);
        (await handler.Handle(new SaveTeachingAssignmentsCommand(1, new(1, [Cell(1, 1)])), default)).Errors.Should().Contain(TimetableSettingsHandlerSupport.PermissionDenied);
        (await handler.Handle(new UnassignTeachingSubjectCommand(1, 1, 1), default)).Errors.Should().Contain(TimetableSettingsHandlerSupport.PermissionDenied);
    }
    [Fact]
    public async Task Teacher_search_filters_sorts_and_paginates_without_losing_total_load()
    {
        await using var db = await Seed(); var service = Service(db);
        await service.SaveAsync(1, 1, new(1, [Cell(1, 2)]), "manager", default);
        var page = await service.SearchAsync(1, 1, new(Sort: "load", Descending: true, PageSize: 1), default);
        page.TotalCount.Should().Be(2); page.Items.Single().Id.Should().Be(2); page.Items.Single().AllocatedPeriods.Should().Be(4);
        var second = await service.SearchAsync(1, 1, new(Sort: "load", Descending: true, Page: 2, PageSize: 1), default);
        second.Items.Single().Id.Should().Be(1);
        (await service.SearchAsync(1, 1, new(Search: "Teacher 2"), default)).Items.Should().ContainSingle();
    }
    [Fact]
    public async Task Planned_load_prevents_lowering_maximum_and_requirement_quota_changes()
    {
        await using var db = await Seed(); await Service(db).SaveAsync(1, 1, new(1, [Cell(1, 1)]), "manager", default);
        var availability = new TeacherAvailabilityService(new TeacherAvailabilityRepository(db));
        var current = await availability.GetAsync(1, 1, 1, default); current.AllocatedPeriods.Should().Be(4);
        var request = new UpdateTeacherProfileRequest(current.Revision, current.BellScheduleRevisionId, "", 3, false, false, false,
            current.Slots.Select(s => new AvailabilitySlotRequest(s.Day, s.BellPeriodId, s.IsAvailable)).ToArray());
        await availability.Invoking(s => s.UpdateAsync(1, 1, 1, request, "manager", DateTimeOffset.UtcNow, default)).Should().ThrowAsync<ArgumentException>();
        var subjects = new SubjectService(new SubjectRepository(db), new BellScheduleRepository(db));
        await subjects.Invoking(s => s.RemoveAsync(1, 1, 1, 1, "manager", default)).Should().ThrowAsync<ArgumentException>();
    }
    [Fact]
    public async Task Co_teachers_share_one_occurrence_and_reassignment_invalidates_old_placements()
    {
        await using var db = await Seed();
        // This test places only the first configured requirement.
        db.Set<ClassSubjectRequirement>().Single(r => r.Id == 2).IsDeleted = true; await db.SaveChangesAsync();
        var saved = await Service(db).SaveAsync(1, 1, new(1, [new(1, "CoTeaching", [new(1, 4, 1), new(2, 4, 1)])]), "manager", default);
        var schedule = (await new SubjectService(new SubjectRepository(db), new BellScheduleRepository(db)).GetAsync(1, 1, default)).Schedule!;
        schedule = schedule with { DefaultPeriods = [schedule.DefaultPeriods[0], schedule.DefaultPeriods[1] with { StartLocalTime = new(7,45) }] };
        var entries = (from teacher in new[] { 1, 2 } from day in new[] { TimetableDay.Saturday, TimetableDay.Sunday } from period in new[] { 1, 2 }
            select new TimetableEntryDto(teacher, day, period, TimetableEntryType.Lesson, "1/A", "Math")).ToArray();
        var validator = new SubjectAssignmentService(new SubjectRepository(db));
        var timetable = new SchoolTimetable { SchoolId = 1, TimetableSetupProfileId = 1 };
        (await validator.ValidateAsync(timetable, entries, schedule, true, default)).Should().HaveCount(8);
        await validator.Invoking(s => s.ValidateAsync(timetable, entries.Skip(1).ToArray(), schedule, false, default)).Should().ThrowAsync<ArgumentException>();
        // Release any locally incremented validation fence before the assignment save.
        db.ChangeTracker.Clear();
        await Service(db).SaveAsync(1, 1, new(saved.Revision, [Cell(1, 1)]), "manager", default);
        await validator.Invoking(s => s.ValidateAsync(timetable, entries, schedule, true, default)).Should().ThrowAsync<ArgumentException>();
    }
    [Fact]
    public async Task Concurrent_setup_changes_reject_a_preloaded_assignment_save()
    {
        await using var db = await Seed(); var service = Service(db); await service.GetAsync(1, 1, default);
        var options = (DbContextOptions<AlFalahDbContext>)db.GetService<IDbContextOptions>(); await using var other = new AlFalahDbContext(options);
        var setup = await other.TimetableSetupProfiles.SingleAsync(); setup.Revision++; await other.SaveChangesAsync();
        await service.Invoking(s => s.SaveAsync(1, 1, new(1, [Cell(1, 1)]), "manager", default)).Should().ThrowAsync<BellScheduleConflictException>();
    }
    [Fact]
    public async Task Assignment_changes_invalidate_drafts_without_mutating_published_timetables()
    {
        await using var db = await Seed();
        db.SchoolTimetables.AddRange(
            new SchoolTimetable { Id = 1, SchoolId = 1, AcademicYearId = 1, TimetableSetupProfileId = 1,
                Semester = TimetableSemester.First, Title = "Published", Revision = 7, IsPublished = true,
                CreatedByUserId = "manager", UpdatedByUserId = "manager" },
            new SchoolTimetable { Id = 2, SchoolId = 1, AcademicYearId = 1, TimetableSetupProfileId = 1,
                Semester = TimetableSemester.First, Title = "Draft", Revision = 3,
                CreatedByUserId = "manager", UpdatedByUserId = "manager" });
        await db.SaveChangesAsync();

        await Service(db).SaveAsync(1, 1, new(1, [Cell(1, 1)]), "manager", default);

        db.SchoolTimetables.Single(x => x.Id == 1).Should().Match<SchoolTimetable>(x =>
            x.IsPublished && x.Revision == 7 && !x.TimingsRequireRevalidation);
        db.SchoolTimetables.Single(x => x.Id == 2).Should().Match<SchoolTimetable>(x =>
            !x.IsPublished && x.Revision == 4 && x.TimingsRequireRevalidation);
    }
    private static TeachingCellRequest Cell(int requirement, int teacher) => new(requirement, "SingleTeacher", [new(teacher, 4, 1)]);
    [Fact]
    public async Task Exact_maximum_is_allowed_and_inactive_class_assignments_can_be_removed()
    {
        await using var db = await Seed();
        var second = db.Set<ClassSubjectRequirement>().Single(r => r.Id == 2); second.PairedBlockCount = 0; await db.SaveChangesAsync();
        var saved = await Service(db).SaveAsync(1, 1, new(1, [Cell(1, 1), new(2, "SingleTeacher", [new(1, 2, 0)])]), "manager", default);
        saved.Teachers.Single(t => t.Id == 1).RemainingPeriods.Should().Be(0);
        db.Classrooms.Single(c => c.Id == 1).IsActive = false; await db.SaveChangesAsync();
        var overview = await Service(db).GetAsync(1, 1, default);
        overview.Requirements.Should().NotContain(r => r.Id == 1); overview.Warnings.Should().NotBeEmpty();
        var removed = await Service(db).SaveAsync(1, 1, new(saved.Revision, [new(1, "SingleTeacher", [])]), "manager", default);
        removed.Teachers.Single(t => t.Id == 1).AllocatedPeriods.Should().Be(2);
    }
    [Fact]
    public async Task Publish_rejects_a_double_period_whose_second_half_is_placed_with_another_teacher()
    {
        await using var db = await Seed(); db.Set<ClassSubjectRequirement>().Single(r => r.Id == 2).IsDeleted = true; await db.SaveChangesAsync();
        await Service(db).SaveAsync(1, 1, new(1, [new(1, "SplitQuota", [new(1, 3, 1), new(2, 1, 0)])]), "manager", default);
        var schedule = (await new SubjectService(new SubjectRepository(db), new BellScheduleRepository(db)).GetAsync(1, 1, default)).Schedule!;
        schedule = schedule with { DefaultPeriods = [schedule.DefaultPeriods[0], schedule.DefaultPeriods[1] with { StartLocalTime = new(7,45) }] };
        TimetableEntryDto Entry(int teacher, int day, int period) => new(teacher, (TimetableDay)day, period, TimetableEntryType.Lesson, "1/A", "Math");
        var validator = new SubjectAssignmentService(new SubjectRepository(db)); var timetable = new SchoolTimetable { SchoolId = 1, TimetableSetupProfileId = 1 };
        await validator.Invoking(s => s.ValidateAsync(timetable, [Entry(1,1,1), Entry(2,1,2), Entry(1,2,1), Entry(1,3,1)], schedule, true, default)).Should().ThrowAsync<ArgumentException>();
        await validator.Invoking(s => s.ValidateAsync(timetable, [Entry(1,1,1), Entry(1,1,2), Entry(1,2,1), Entry(2,2,2)], schedule, true, default)).Should().NotThrowAsync();
    }
    private static TeachingAssignmentService Service(AlFalahDbContext db) => new(new TeachingAssignmentRepository(db), new(new SubjectRepository(db), new BellScheduleRepository(db)));
    private static async Task<AlFalahDbContext> Seed()
    {
        var db = new AlFalahDbContext(new DbContextOptionsBuilder<AlFalahDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var schedule = await BellScheduleTestData.SeedAsync(db);
        db.Add(new SubjectDefinition { Id = 1, SchoolId = 1, Name = "Math" });
        for (var id = 1; id <= 2; id++)
        {
            db.Users.Add(new() { Id = $"teacher-{id}", FirstName = "Teacher", LastName = id.ToString(), IsActive = true });
            db.InstructorProfiles.Add(new() { Id = id, UserId = $"teacher-{id}", SchoolId = 1, SubjectSpecialization = "Math", IsActive = true });
            db.TeacherTimetableProfiles.Add(new() { Id = id, SchoolId = 1, TimetableSetupProfileId = 1, InstructorProfileId = id, BellScheduleRevisionId = schedule.Id, MaximumWeeklyPeriods = 6 });
            db.Classrooms.Add(new() { Id = id, SchoolId = 1, AcademicYearId = 1, ClassLabel = id == 1 ? "1/A" : "1/B", IsActive = true });
            db.Add(new ClassSubjectRequirement { Id = id, SchoolId = 1, TimetableSetupProfileId = 1, ClassroomId = id, SubjectId = 1, IndividualPeriodCount = 2, PairedBlockCount = 1 });
        }
        await db.SaveChangesAsync(); return db;
    }
    private sealed class User(string role, bool manage) : ICurrentUserService
    {
        public string? UserId => "manager"; public string? Username => "manager"; public int? ActiveSchoolId => 1;
        public string? PreferredLanguage => "ar"; public bool IsAuthenticated => true;
        public bool IsInRole(string name) => role == name; public bool HasPermission(string name) => manage && name == PermissionNames.TimetableManage;
        public IEnumerable<string> GetRoles() => [role]; public IEnumerable<string> GetPermissions() => [];
        public bool IsGlobalAdmin() => false; public bool IsSchoolScopedRole() => true;
    }
}
