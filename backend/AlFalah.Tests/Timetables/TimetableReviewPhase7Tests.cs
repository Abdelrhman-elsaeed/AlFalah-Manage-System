using System.Text.Json;
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
using Xunit;

namespace AlFalah.Tests.Timetables;

public sealed class TimetableReviewPhase7Tests
{
    private static readonly TimetableValidationEngine Engine = new();
    [Fact] public void Valid_normalized_timetable_has_no_hard_errors()
    { Engine.Evaluate(Context()).Should().NotContain(x => x.Severity == ViolationSeverity.Error); }

    [Theory]
    [InlineData(ViolationRuleCode.TeacherDoubleBooking)] [InlineData(ViolationRuleCode.ClassroomDoubleBooking)]
    [InlineData(ViolationRuleCode.RoomDoubleBooking)] [InlineData(ViolationRuleCode.UnavailableSlot)]
    [InlineData(ViolationRuleCode.NonStudyDayPlacement)] [InlineData(ViolationRuleCode.MissingAssignment)]
    [InlineData(ViolationRuleCode.ScheduleCountMismatch)] [InlineData(ViolationRuleCode.BrokenPairedBlock)]
    [InlineData(ViolationRuleCode.DisallowedDay)] [InlineData(ViolationRuleCode.ViolatedFixedSlot)]
    public void All_ten_hard_checks_are_enforced(ViolationRuleCode rule)
    {
        var c = Context(); var e = c.Timetable.Entries.First(); var r = c.Requirements[0];
        switch (rule)
        {
            case ViolationRuleCode.TeacherDoubleBooking: c.Timetable.Entries.Add(Entry(2, teacher: 1, classroom: 2)); break;
            case ViolationRuleCode.ClassroomDoubleBooking: c.Timetable.Entries.Add(Entry(2, teacher: 2)); break;
            case ViolationRuleCode.RoomDoubleBooking: e.RoomId = 1; c.Timetable.Entries.Add(Entry(2, teacher: 2, classroom: 2, room: 1)); break;
            case ViolationRuleCode.UnavailableSlot: Close(c, 1); break;
            case ViolationRuleCode.NonStudyDayPlacement: e.Day = TimetableDay.Friday; break;
            case ViolationRuleCode.MissingAssignment: e.InstructorProfileId = 999; break;
            case ViolationRuleCode.ScheduleCountMismatch: r.IndividualPeriodCount = 3; break;
            case ViolationRuleCode.BrokenPairedBlock: r.PairedBlockCount = 1; r.IndividualPeriodCount = 0; break;
            case ViolationRuleCode.DisallowedDay: r.AllowedDays.Add(new() { Day = 3 }); break;
            case ViolationRuleCode.ViolatedFixedSlot: r.FixedSlots.Add(new() { Day = 2, Period = 2 }); break;
        }
        Engine.Evaluate(c).Should().Contain(x => x.RuleCode == rule && x.Severity == ViolationSeverity.Error);
    }
    [Theory] [InlineData(3, false)] [InlineData(4, true)]
    public void Consecutive_boundary(int count, bool warning)
    {
        var c = Context(); c.Timetable.Entries = Enumerable.Range(1, count).Select(i => Entry(i, period: i, room: i % 2 == 0 ? 1 : null)).ToList();
        Has(c, ViolationRuleCode.ExcessiveConsecutive).Should().Be(warning);
    }
    [Theory] [InlineData(2, false)] [InlineData(3, true)]
    public void Last_period_boundary(int count, bool warning)
    {
        var c = Context(); c.Timetable.Entries = Enumerable.Range(1, count).Select(i => Entry(i, day: i + 1, period: 8)).ToList();
        Has(c, ViolationRuleCode.UnfairLastPeriods).Should().Be(warning);
    }
    [Theory] [InlineData(4, false)] [InlineData(5, true)]
    public void Idle_gap_boundary(int finalPeriod, bool warning)
    {
        var c = Context(); c.Timetable.Entries.Add(Entry(2, period: finalPeriod));
        Has(c, ViolationRuleCode.ExcessiveGaps).Should().Be(warning);
    }
    [Theory] [InlineData(1, false)] [InlineData(2, true)]
    public void Individual_subject_distribution_boundary(int count, bool warning)
    {
        var c = Context(); c.Timetable.Entries = Enumerable.Range(1, count).Select(i => Entry(i, period: i)).ToList();
        Has(c, ViolationRuleCode.SubjectConcentration).Should().Be(warning);
    }
    [Fact] public void Intact_pair_is_exempt_and_a_break_splits_it()
    {
        var c = Context(); c.Requirements[0].IndividualPeriodCount = 0; c.Requirements[0].PairedBlockCount = 1;
        c.Timetable.Entries.Add(Entry(2, period: 2));
        Has(c, ViolationRuleCode.BrokenPairedBlock).Should().BeFalse(); Has(c, ViolationRuleCode.SubjectConcentration).Should().BeFalse();
        c.Schedule!.Days.Single(x => x.Day == 0).Periods.Single(x => x.Sequence == 2).StartLocalTime = new(8, 5);
        Has(c, ViolationRuleCode.BrokenPairedBlock).Should().BeTrue();
    }
    [Fact] public void Early_preference_and_cluster_checks_are_soft()
    {
        var c = Context(); c.Timetable.Entries.First().Period = 7; c.Requirements[0].TimePreference = "Early";
        Engine.Evaluate(c).Should().Contain(x => x.RuleCode == ViolationRuleCode.EarlyPreferenceMissed && x.Severity == ViolationSeverity.Warning);
        c.Requirements[0].TimePreference = "None"; c.Timetable.Entries.Add(Entry(2, day: 3, period: 8));
        Has(c, ViolationRuleCode.SubjectClusterImbalance).Should().BeTrue();
    }
    [Fact] public void Overlapping_real_time_windows_conflict_even_with_different_sequences()
    {
        var c = Context(); c.Schedule!.Days.First().Periods.Single(x => x.Sequence == 2).StartLocalTime = new(7, 50);
        c.Timetable.Entries.Add(Entry(2, period: 2, classroom: 2));
        Has(c, ViolationRuleCode.TeacherDoubleBooking).Should().BeTrue();
    }
    [Fact] public void Break_overlap_invalid_period_inactive_reference_and_maximum_load_block()
    {
        var c = Context(); c.Schedule!.Days.First().Breaks.Add(new() { Name = "Break", Window = new() { StartLocalTime = new(7, 45), EndLocalTime = new(8, 0) } });
        Has(c, ViolationRuleCode.NonStudyDayPlacement).Should().BeTrue();
        c.Timetable.Entries.First().Period = 100; Has(c, ViolationRuleCode.NonStudyDayPlacement).Should().BeTrue();
        c.Teachers[0].Instructor.IsActive = false; Has(c, ViolationRuleCode.MissingAssignment).Should().BeTrue();
        c.Teachers[0].MaximumWeeklyPeriods = 0; Has(c, ViolationRuleCode.ScheduleCountMismatch).Should().BeTrue();
    }
    [Fact] public void Co_teaching_counts_one_occurrence_and_does_not_double_book_class_or_room()
    {
        var c = Context(); c.Assignments[0].Mode = "CoTeaching";
        c.Assignments[0].Members.Add(new() { Id = 2, TeacherTimetableProfileId = 2, AllocatedPeriodCount = 1 });
        c.Timetable.Entries.First().RoomId = 1; c.Timetable.Entries.Add(Entry(2, teacher: 2, room: 1));
        Engine.Evaluate(c).Should().NotContain(x => x.Severity == ViolationSeverity.Error);
        c.Timetable.Entries.Last().Period = 2;
        Has(c, ViolationRuleCode.MissingAssignment).Should().BeTrue();
    }
    [Fact] public void Hard_override_and_empty_reason_are_rejected_and_soft_override_records_actor()
    {
        var hard = new TimetableAnalysisFinding { Severity = ViolationSeverity.Error };
        Action reject = () => TimetableReviewService.Override(hard, "manager", "reason"); reject.Should().Throw<ArgumentException>();
        var soft = new TimetableAnalysisFinding { Severity = ViolationSeverity.Warning };
        Action empty = () => TimetableReviewService.Override(soft, "manager", " "); empty.Should().Throw<ArgumentException>();
        TimetableReviewService.Override(soft, "manager", "  approved reason  ");
        soft.IsOverridden.Should().BeTrue(); soft.OverrideReason.Should().Be("approved reason");
        soft.OverriddenByUserId.Should().Be("manager"); soft.OverriddenAt.Should().NotBeNull();
    }
    [Fact] public void Proposals_are_ranked_pure_and_resolve_conflicts_without_new_hard_errors()
    {
        var c = Context(); Close(c, 1);
        var target = Engine.Evaluate(c).Single(x => x.RuleCode == ViolationRuleCode.UnavailableSlot);
        var finding = new TimetableAnalysisFinding { Id = 1, Severity = target.Severity, EvidenceJson = JsonSerializer.Serialize(target) };
        var proposals = new TimetableRepairEngine(Engine).Propose(c, new() { Id = 1 }, finding, default);
        proposals.Should().HaveCount(3); proposals.Select(x => x.Rank).Should().Equal(1, 2, 3);
        c.Timetable.Entries.Single().Period.Should().Be(1);
        foreach (var proposal in proposals) Engine.Evaluate(TimetableRepairEngine.Simulate(c, proposal.Movements)).Should().NotContain(x => x.Severity == ViolationSeverity.Error);
    }
    [Fact] public async Task Repair_is_persisted_audited_versioned_and_reanalyzed_and_replay_rejected()
    {
        await using var db = await Seed(close: true); var service = Service(db);
        var before = await service.EvaluateAsync(1, default); before.CanPublish.Should().BeFalse();
        var proposals = await service.ProposalsAsync(1, before.Findings.Single(x => x.RuleCode == ViolationRuleCode.UnavailableSlot).Id, default);
        var after = await service.ApplyAsync(1, proposals[0], default);
        after.TimetableRevision.Should().Be(2); after.AnalysisRunId.Should().NotBe(before.AnalysisRunId); after.CanPublish.Should().BeTrue();
        db.SchoolTimetableVersions.Should().ContainSingle(); db.AuditLogs.Should().ContainSingle(x => x.Action == "Timetable.Review.RepairApplied");
        db.TimetableAnalysisRuns.Should().HaveCount(2);
        await service.Invoking(x => x.ApplyAsync(1, proposals[0], default)).Should().ThrowAsync<BellScheduleConflictException>();
        db.ChangeTracker.Clear(); (await Service(db).EvaluateAsync(1, default)).HardViolationCount.Should().Be(0);
    }
    [Fact] public void Teacher_conflict_offers_safe_swap_as_well_as_move()
    {
        var c = Context();
        var otherSubject = new SubjectDefinition { Id = 2, SchoolId = 1, Name = "Science" };
        c = c with { Subjects = [.. c.Subjects, otherSubject], Requirements = [.. c.Requirements,
            new() { Id = 2, SchoolId = 1, ClassroomId = 2, SubjectId = 1, IndividualPeriodCount = 1, Classroom = c.Classrooms[1], Subject = c.Subjects[0] },
            new() { Id = 3, SchoolId = 1, ClassroomId = 1, SubjectId = 2, IndividualPeriodCount = 1, Classroom = c.Classrooms[0], Subject = otherSubject }],
            Assignments = [.. c.Assignments,
                new() { Id = 2, ClassSubjectRequirementId = 2, Members = [new() { Id = 2, TeacherTimetableProfileId = 1, AllocatedPeriodCount = 1 }] },
                new() { Id = 3, ClassSubjectRequirementId = 3, Members = [new() { Id = 3, TeacherTimetableProfileId = 2, AllocatedPeriodCount = 1 }] }] };
        var second = Entry(2, classroom: 2); second.ClassSubjectRequirementId = 2;
        var third = Entry(3, teacher: 2, period: 2); third.ClassSubjectRequirementId = 3; third.SubjectId = 2;
        c.Timetable.Entries.Add(second); c.Timetable.Entries.Add(third);
        var target = Engine.Evaluate(c).Single(x => x.RuleCode == ViolationRuleCode.TeacherDoubleBooking);
        var proposals = new TimetableRepairEngine(Engine).Propose(c, new() { Id = 1 },
            new() { Id = 1, Severity = ViolationSeverity.Error, EvidenceJson = JsonSerializer.Serialize(target) }, default);
        proposals.Should().Contain(x => x.Kind == RepairProposalKind.SwapEntries).And.Contain(x => x.Kind == RepairProposalKind.MoveEntry);
        foreach (var p in proposals) Engine.Evaluate(TimetableRepairEngine.Simulate(c, p.Movements)).Should().NotContain(x => x.Severity == ViolationSeverity.Error);
    }
    [Fact] public void Repair_moves_a_paired_block_as_a_whole()
    {
        var c = Context(); c.Requirements[0].IndividualPeriodCount = 0; c.Requirements[0].PairedBlockCount = 1;
        c.Assignments[0].Members.Single().AllocatedPeriodCount = 2; c.Assignments[0].Members.Single().AllocatedPairedBlockCount = 1;
        c.Timetable.Entries.Add(Entry(2, period: 2)); Close(c, 1);
        var target = Engine.Evaluate(c).Single(x => x.RuleCode == ViolationRuleCode.UnavailableSlot);
        var proposals = new TimetableRepairEngine(Engine).Propose(c, new() { Id = 1 },
            new() { Id = 1, Severity = ViolationSeverity.Error, EvidenceJson = JsonSerializer.Serialize(target) }, default);
        proposals.Should().NotBeEmpty().And.OnlyContain(x => x.Movements.Count == 2);
        foreach (var p in proposals) Engine.Evaluate(TimetableRepairEngine.Simulate(c, p.Movements)).Should().NotContain(x => x.Severity == ViolationSeverity.Error);
    }
    [Fact] public async Task Tampered_movement_is_rejected_without_a_snapshot()
    {
        await using var db = await Seed(close: true); var service = Service(db); var run = await service.EvaluateAsync(1, default);
        var proposal = (await service.ProposalsAsync(1, run.Findings.Single(x => x.RuleCode == ViolationRuleCode.UnavailableSlot).Id, default))[0];
        await service.Invoking(x => x.ApplyAsync(1, proposal with { Movements = [proposal.Movements[0] with { ToTeacherId = 999 }] }, default)).Should().ThrowAsync<ArgumentException>();
        db.SchoolTimetableVersions.Should().BeEmpty(); db.SchoolTimetableEntries.Single().Period.Should().Be(1);
    }
    [Fact] public async Task Publication_always_checks_current_inputs_and_accepts_soft_only_timetables()
    {
        await using var db = await Seed(); var service = Service(db);
        var r = await service.EvaluateAsync(1, default); r.CanPublish.Should().BeTrue();
        var teacher = db.TeacherTimetableProfiles.Single(x => x.Id == 1); teacher.MaximumWeeklyPeriods = 0; await db.SaveChangesAsync();
        await service.Invoking(x => x.PublishAsync(1, 1, default)).Should().ThrowAsync<ArgumentException>();
        db.SchoolTimetables.Single().IsPublished.Should().BeFalse();
        teacher.MaximumWeeklyPeriods = 20; var requirement = db.Set<ClassSubjectRequirement>().Single();
        requirement.TimePreference = "Early"; requirement.LatestPreferredPeriodSequence = 0; requirement.Revision++;
        await db.SaveChangesAsync(); var soft = await service.EvaluateAsync(1, default);
        soft.WarningCount.Should().BeGreaterThan(0); soft.CanPublish.Should().BeTrue();
        var warning = soft.Findings.Single(x => x.RuleCode == ViolationRuleCode.EarlyPreferenceMissed);
        await service.OverrideAsync(warning.Id, "Accepted for this revision", default);
        var published = await service.PublishAsync(1, 1, default);
        published.IsPublished.Should().BeTrue(); published.TimetableRevision.Should().Be(2);
        published.Findings.Single(x => x.RuleCode == ViolationRuleCode.EarlyPreferenceMissed).IsOverridden.Should().BeTrue();
        published.Findings.Single().OverrideReason.Should().Be("Accepted for this revision");
    }
    [Fact] public async Task Published_review_remains_pinned_to_the_publication_snapshot_after_setup_changes()
    {
        await using var db = await Seed(); var service = Service(db);
        var published = await service.PublishAsync(1, 1, default);
        published.HardViolationCount.Should().Be(0);

        var teacher = db.TeacherTimetableProfiles.Single(x => x.Id == 1);
        teacher.MaximumWeeklyPeriods = 0;
        teacher.Revision++;
        var setup = db.TimetableSetupProfiles.Single();
        setup.Revision++;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var reviewedAgain = await Service(db).EvaluateAsync(1, default);
        reviewedAgain.AnalysisRunId.Should().Be(published.AnalysisRunId);
        reviewedAgain.TimetableRevision.Should().Be(published.TimetableRevision);
        reviewedAgain.HardViolationCount.Should().Be(0);
    }
    [Fact] public async Task School_scope_role_permissions_and_management_override_are_enforced()
    {
        await using var db = await Seed();
        await Service(db, new User(School: 2)).Invoking(x => x.EvaluateAsync(1, default)).Should().ThrowAsync<KeyNotFoundException>();
        foreach (var role in new[] { RoleNames.Guardian, RoleNames.Instructor })
            await Service(db, new User(Role: role)).Invoking(x => x.EvaluateAsync(1, default)).Should().ThrowAsync<UnauthorizedAccessException>();
        var reviewer = Service(db, new User(Role: RoleNames.Secretary, Manage: false));
        (await reviewer.EvaluateAsync(1, default)).CanManage.Should().BeFalse();
        await reviewer.Invoking(x => x.PublishAsync(1, 1, default)).Should().ThrowAsync<UnauthorizedAccessException>();
        await Service(db, new User(Role: RoleNames.Secretary)).Invoking(x => x.OverrideAsync(1, "reason", default)).Should().ThrowAsync<UnauthorizedAccessException>();
        var handler = new TimetableReviewHandlers(Service(db, new User(Role: RoleNames.Instructor)));
        (await handler.Handle(new EvaluateTimetableQuery(1), default)).IsSuccess.Should().BeFalse();
    }
    [Fact] public async Task Analysis_history_is_immutable_and_old_findings_become_stale_after_setup_changes()
    {
        await using var db = await Seed(close: true); var service = Service(db); var run = await service.EvaluateAsync(1, default);
        db.TimetableSetupProfiles.Single().Revision++; await db.SaveChangesAsync();
        await service.Invoking(x => x.ProposalsAsync(1, run.Findings.First().Id, default)).Should().ThrowAsync<BellScheduleConflictException>();
        var newer = await service.EvaluateAsync(1, default); newer.AnalysisRunId.Should().NotBe(run.AnalysisRunId);
        db.TimetableAnalysisRuns.First().HardViolationCount = 0;
        await db.Invoking(x => x.SaveChangesAsync()).Should().ThrowAsync<InvalidOperationException>();
    }
    private static bool Has(TimetableValidationContext c, ViolationRuleCode code) => Engine.Evaluate(c).Any(x => x.RuleCode == code);
    private static void Close(TimetableValidationContext c, int period) => c.Teachers[0].Slots.Add(new() {
        Id = 1, TeacherTimetableProfileId = 1, Day = 2, IsAvailable = false, BellPeriodId = period,
        Period = c.Schedule!.Days.First().Periods.Single(x => x.Sequence == period) });
    private static SchoolTimetableEntry Entry(int id, int teacher = 1, int classroom = 1, int day = 2, int period = 1, int? room = null) =>
        new() { Id = id, SchoolId = 1, SchoolTimetableId = 1, ClassroomId = classroom, InstructorProfileId = teacher,
            ClassSubjectRequirementId = 1, SubjectId = 1, Subject = "Math", ClassLabel = $"Class {classroom}",
            Day = (TimetableDay)day, Period = period, RoomId = room, EntryType = TimetableEntryType.Lesson };
    internal static TimetableValidationContext Context()
    {
        var template = new BellScheduleTemplate { Id = 1, SchoolId = 1, AcademicYearId = 1, Semester = TimetableSemester.First, Name = "Timing" };
        var schedule = new BellScheduleRevision { Id = 1, SchoolId = 1, BellScheduleTemplateId = 1, Revision = 1,
            Template = template, SchoolTimeZoneId = "Africa/Cairo", Name = "Timing" };
        schedule.Days.Add(new() { Id = 1, BellScheduleRevisionId = 1, Day = 0, IsStudyDay = true,
            Periods = Enumerable.Range(1, 8).Select(i => new BellPeriod { Id = i, BellScheduleDayId = 1, Sequence = i,
                StartLocalTime = new TimeOnly(7, 0).AddMinutes(i * 30), EndLocalTime = new TimeOnly(7, 0).AddMinutes((i + 1) * 30) }).ToList() });
        foreach (var day in Enumerable.Range(2, 5)) schedule.Days.Add(new() { Id = day, BellScheduleRevisionId = 1, Day = day, IsStudyDay = true, UsesDefaultSchedule = true });
        var setup = new TimetableSetupProfile { Id = 1, SchoolId = 1, AcademicYearId = 1, Semester = TimetableSemester.First, BellScheduleTemplateId = 1, Name = "Setup" };
        var teachers = Enumerable.Range(1, 2).Select(i => new TeacherTimetableProfile { Id = i, SchoolId = 1,
            TimetableSetupProfileId = 1, InstructorProfileId = i, MaximumWeeklyPeriods = 20, BellScheduleRevisionId = 1,
            Instructor = new() { Id = i, SchoolId = 1, IsActive = true, UserId = $"teacher{i}", User = new() { Id = $"teacher{i}", FirstName = $"Teacher {i}", LastName = "Test", IsActive = true } } }).ToArray();
        var classrooms = Enumerable.Range(1, 2).Select(i => new Classroom { Id = i, SchoolId = 1, AcademicYearId = 1, ClassLabel = $"Class {i}" }).ToArray();
        var subject = new SubjectDefinition { Id = 1, SchoolId = 1, Name = "Math" };
        var requirement = new ClassSubjectRequirement { Id = 1, SchoolId = 1, TimetableSetupProfileId = 1, ClassroomId = 1,
            SubjectId = 1, Classroom = classrooms[0], Subject = subject, IndividualPeriodCount = 1 };
        var assignment = new TeachingAssignment { Id = 1, SchoolId = 1, TimetableSetupProfileId = 1, ClassSubjectRequirementId = 1,
            Members = [new() { Id = 1, SchoolId = 1, TimetableSetupProfileId = 1, TeachingAssignmentId = 1, TeacherTimetableProfileId = 1, AllocatedPeriodCount = 1 }] };
        var timetable = new SchoolTimetable { Id = 1, SchoolId = 1, AcademicYearId = 1, Semester = TimetableSemester.First,
            TimetableSetupProfileId = 1, BellScheduleRevisionId = 1, Title = "Review", CreatedByUserId = "manager", UpdatedByUserId = "manager", Entries = [Entry(1)] };
        return new(timetable, setup, schedule, teachers, [requirement], [assignment], classrooms, [subject], [new() { Id = 1, SchoolId = 1, Name = "Lab" }]);
    }
    internal static async Task<AlFalahDbContext> Seed(bool close = false)
    {
        var db = new AlFalahDbContext(new DbContextOptionsBuilder<AlFalahDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var c = Context(); if (close) Close(c, 1);
        db.Schools.Add(new() { Id = 1, Name = "School", IsActive = true });
        db.Users.Add(new() { Id = "manager", FirstName = "Manager", LastName = "Test", IsActive = true });
        db.AcademicYears.Add(new() { Id = 1, Code = "2026", NameAr = "2026", IsActive = true });
        db.Add(c.Schedule!); db.Add(c.Setup!); db.AddRange(c.Teachers); db.AddRange(c.Classrooms); db.AddRange(c.Subjects);
        db.AddRange(c.Requirements); db.AddRange(c.Assignments); db.AddRange(c.Rooms); db.Add(c.Timetable);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return db;
    }
    private static TimetableReviewService Service(AlFalahDbContext db, User? user = null) =>
        new(new TimetableReviewRepository(db), Engine, new TimetableRepairEngine(Engine), user ?? new User());
    private sealed record User(int School = 1, string Role = RoleNames.SchoolManager, bool Manage = true) : ICurrentUserService
    {
        public string? UserId => "manager"; public string? Username => "manager"; public int? ActiveSchoolId => School;
        public string? PreferredLanguage => "ar"; public bool IsAuthenticated => true;
        public bool IsInRole(string role) => Role == role;
        public bool HasPermission(string permission) => permission == PermissionNames.TimetableReview || Manage && permission == PermissionNames.TimetableManage;
        public IEnumerable<string> GetRoles() => [Role]; public IEnumerable<string> GetPermissions() => [PermissionNames.TimetableReview];
        public bool IsGlobalAdmin() => false; public bool IsSchoolScopedRole() => true;
    }
}
