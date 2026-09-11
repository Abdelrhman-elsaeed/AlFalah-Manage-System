using System.Text.Json;
using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Events;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Notifications;
using AlFalah.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AlFalah.Tests.Timetables;

public sealed class TimetableSubstitutionPhase8Tests
{
    private static readonly DateOnly Date = new(2026, 9, 13); // Sunday
    private static readonly DateTimeOffset Expires = new(2026, 9, 13, 4, 5, 0, TimeSpan.Zero);
    private static readonly TimetableValidationEngine Validator = new();
    private static readonly TimetableSwapEngine Engine = new(Validator);

    [Fact] public void Free_teacher_is_green_and_cover_does_not_rewrite_assignment_quota()
    {
        var c = Context();
        var candidate = Engine.Candidates(c, 1, "Substitution", Date, Expires, Assigned(c), default).Single();
        candidate.Color.Should().Be("Green"); candidate.Movements.Single().ToTeacherId.Should().Be(2);
        c.Timetable.Entries.Single().InstructorProfileId.Should().Be(1);
    }
    [Fact] public void Unavailable_teacher_and_teacher_collision_are_red()
    {
        var c = Context(); var p = c.Schedule!.Days.First().Periods.First();
        c.Teachers[1].Slots.Add(new() { Day = 2, IsAvailable = false, BellPeriodId = p.Id, Period = p });
        Engine.Candidates(c, 1, "Substitution", Date, Expires, Assigned(c), default).Single().Color.Should().Be("Red");
        c.Teachers[1].Slots.Clear(); AddLesson(c, 2, 2, 2, 1);
        Engine.Candidates(c, 1, "Substitution", Date, Expires, Assigned(c), default).Single().Color.Should().Be("Red");
    }
    [Fact] public void Fourth_consecutive_period_is_yellow()
    {
        var c = Context(); c.Timetable.Entries.First().Period = 4;
        for (var p = 1; p <= 3; p++) AddLesson(c, p + 1, 2, 2, p);
        var candidate = Engine.Candidates(c, 1, "Substitution", Date, Expires, Assigned(c), default).Single();
        candidate.Color.Should().Be("Yellow"); candidate.Warnings.Should().Contain(w => w.Contains("متتالية"));
    }
    [Fact] public void Selecting_second_half_covers_both_periods()
    {
        var c = PairContext();
        var candidate = Engine.Candidates(c, 2, "Substitution", Date, Expires, Assigned(c), default).Single();
        candidate.Color.Should().Be("Green"); candidate.Movements.Select(m => m.EntryId).Should().BeEquivalentTo([1, 2]);
    }
    [Fact] public void Co_teachers_and_pairs_move_together_in_a_structural_swap()
    {
        var c = PairContext(); c.Assignments[0].Mode = "CoTeaching";
        c.Assignments[0].Members.Add(new() { Id = 2, TeacherTimetableProfileId = 2, AllocatedPeriodCount = 2, AllocatedPairedBlockCount = 1 });
        c.Timetable.Entries.Add(Copy(c.Timetable.Entries.First(), 3, 2, 1));
        c.Timetable.Entries.Add(Copy(c.Timetable.Entries.First(), 4, 2, 2));
        AddLesson(c, 5, 1, 2, 4);
        var proposals = Engine.Candidates(c, 2, "Swap", Date, Expires, null, default);
        proposals.Should().NotBeEmpty(); proposals.Should().OnlyContain(p => p.Movements.Count >= 5);
        proposals[0].Movements.Where(m => m.EntryId <= 4).Select(m => m.ToPeriod).Should().BeEquivalentTo([4, 5, 4, 5]);
    }
    [Fact] public void Direct_swap_and_three_way_cycles_are_explicit_same_day_and_safe()
    {
        var c = Context();
        c = c with { Teachers = c.Teachers.Append(new TeacherTimetableProfile { Id = 3, SchoolId = 1, TimetableSetupProfileId = 1,
            InstructorProfileId = 3, BellScheduleRevisionId = 1, MaximumWeeklyPeriods = 20,
            Instructor = new() { Id = 3, SchoolId = 1, IsActive = true, User = new() { IsActive = true, FirstName = "Third" } } }).ToArray() };
        AddLesson(c, 2, 2, 2, 2); AddLesson(c, 3, 3, 1, 3);
        var p1 = c.Schedule!.Days.First().Periods.Single(p => p.Sequence == 1);
        c.Teachers[1].Slots.Add(new() { Day = 2, IsAvailable = false, BellPeriodId = p1.Id, Period = p1 });
        var candidates = Engine.Candidates(c, 1, "Swap", Date, Expires, null, default);
        candidates.Should().Contain(p => p.Kind == "DirectSwap" && p.Color == "Red");
        candidates.Should().Contain(p => p.Kind == "ThreeWaySwap" && p.Color != "Red" && p.Movements.Count == 3);
        var safe = candidates.Where(p => p.Color != "Red").ToArray(); safe.Should().NotBeEmpty();
        foreach (var p in safe)
        {
            p.Movements.Should().OnlyContain(m => m.FromDay == m.ToDay);
            Validator.Evaluate(TimetableRepairEngine.Simulate(c, p.Movements)).Should().NotContain(v => v.Severity == ViolationSeverity.Error);
        }
    }
    [Fact] public void Scheduled_standby_can_cover_a_lesson_without_changing_the_weekly_standby()
    {
        var c = Context(); c.Timetable.Entries.Add(new() { Id = 2, SchoolId = 1, SchoolTimetableId = 1,
            InstructorProfileId = 2, Day = TimetableDay.Sunday, Period = 1, EntryType = TimetableEntryType.Standby });
        Engine.Candidates(c, 1, "Substitution", Date, Expires, Assigned(c), default).Single().Color.Should().Be("Green");
        c.Timetable.Entries.Should().HaveCount(2);
    }
    [Fact] public void A_lesson_at_the_same_time_is_not_offered_as_a_no_op_swap()
    {
        var c = Context(); AddLesson(c, 2, 2, 2, 1);
        Engine.Candidates(c, 1, "Swap", Date, Expires, null, default).Should().BeEmpty();
    }
    [Fact] public async Task Paired_cover_is_persisted_as_one_change_and_both_slots_become_live()
    {
        await using var db = await Seed();
        var r = db.Set<ClassSubjectRequirement>().Single(); r.IndividualPeriodCount = 0; r.PairedBlockCount = 1;
        var member = db.Set<TeachingAssignmentMember>().Single(); member.AllocatedPeriodCount = 2; member.AllocatedPairedBlockCount = 1;
        db.SchoolTimetableEntries.Add(Copy(db.SchoolTimetableEntries.Single(), 2, 1, 2)); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var service = Service(db); var p = await service.CandidatesAsync(1, Date, 2, "Substitution", default);
        await service.ExecuteAsync(1, Request(p, p.Candidates.Single()), default);
        db.TimetableSubstitutions.Should().ContainSingle(); db.Set<TimetableSubstitutionMovement>().Should().HaveCount(2);
        var day = await service.DailyAsync(1, Date, default);
        day.Lessons.Should().ContainSingle(); day.Lessons[0].TeacherId.Should().Be(2); day.Lessons[0].Periods.Should().Equal(1, 2);
    }
    [Fact] public async Task Recorded_absence_disqualifies_a_replacement()
    {
        await using var db = await Seed(); db.AttendanceRecords.Add(new() { SchoolId = 1, UserId = "teacher2", AttendanceDate = Date,
            Status = AttendanceStatus.Absent, RecordedByUserId = "manager" }); await db.SaveChangesAsync();
        var p = await Service(db).CandidatesAsync(1, Date, 1, "Substitution", default);
        p.Candidates.Single().Color.Should().Be("Red");
    }
    [Fact] public async Task Cover_is_live_date_scoped_audited_versioned_and_idempotent()
    {
        await using var db = await Seed(); var service = Service(db);
        var candidates = await service.CandidatesAsync(1, Date, 1, "Substitution", default);
        var request = Request(candidates, candidates.Candidates.Single());
        var result = await service.ExecuteAsync(1, request, default);
        result.AfterRevision.Should().Be(2); db.SchoolTimetables.Single().IsPublished.Should().BeTrue();
        db.SchoolTimetableEntries.Single().InstructorProfileId.Should().Be(1);
        db.TimetableSubstitutions.Single().ApprovedByUserId.Should().Be("manager");
        db.SchoolTimetableVersions.Single().ChangeKind.Should().Be(TimetableChangeKind.Substitution);
        db.OutboxMessages.Should().ContainSingle();
        (await service.ExecuteAsync(1, request, default)).Id.Should().Be(result.Id);
        db.TimetableSubstitutions.Should().ContainSingle(); db.OutboxMessages.Should().ContainSingle();
        (await service.DailyAsync(1, Date, default)).Lessons.Single().TeacherId.Should().Be(2);
        (await service.DailyAsync(1, Date.AddDays(7), default)).Lessons.Single().TeacherId.Should().Be(1);
        var context = new TeacherContextRepository(db);
        (await context.GetPeriodRosterAsync(1, "teacher2", 1, Date, 1, default)).Should().NotBeNull();
        (await context.GetPeriodRosterAsync(1, "teacher1", 1, Date, 1, default)).Should().BeNull();
        (await context.GetPeriodRosterAsync(1, "teacher1", 1, Date.AddDays(7), 1, default)).Should().NotBeNull();
    }
    [Fact] public async Task Live_structural_swap_keeps_publication_and_creates_fresh_analysis()
    {
        await using var db = await Seed(); var c = await new TimetableReviewRepository(db).GetValidationContextAsync(1, 1, default);
        AddLesson(c!, 2, 2, 2, 2); AddNewGraph(db, c!); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var service = Service(db); var p = await service.CandidatesAsync(1, Date, 1, "Swap", default);
        var candidate = p.Candidates.First(x => x.Color != "Red");
        await service.ExecuteAsync(1, Request(p, candidate, candidate.Color == "Yellow" ? "Approved" : null), default);
        db.SchoolTimetables.Single().IsPublished.Should().BeTrue();
        db.SchoolTimetableEntries.Single(e => e.Id == 1).Period.Should().Be(2);
        db.SchoolTimetableEntries.Single(e => e.Id == 2).Period.Should().Be(1);
        db.TimetableAnalysisRuns.Single().TimetableRevision.Should().Be(2);
    }
    [Fact] public async Task Stale_expired_and_tampered_proposals_never_write()
    {
        await using var db = await Seed(); var service = Service(db); var p = await service.CandidatesAsync(1, Date, 1, "Substitution", default);
        var r = Request(p, p.Candidates.Single());
        foreach (var bad in new[] { r with { Revision = 99 }, r with { ProposalId = "forged" }, r with { ExpiresAt = Expires.AddMinutes(-10) } })
            await service.Invoking(s => s.ExecuteAsync(1, bad, default)).Should().ThrowAsync<BellScheduleConflictException>();
        db.TimetableSubstitutions.Should().BeEmpty(); db.OutboxMessages.Should().BeEmpty(); db.SchoolTimetableVersions.Should().BeEmpty();
    }
    [Fact] public async Task Confirmation_revalidates_availability_and_red_is_never_overridden()
    {
        await using var db = await Seed(); var service = Service(db); var p = await service.CandidatesAsync(1, Date, 1, "Substitution", default);
        db.TeacherAvailabilitySlots.Add(new() { TeacherTimetableProfileId = 2, Day = 2, BellPeriodId = 1, IsAvailable = false });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await service.Invoking(s => s.ExecuteAsync(1, Request(p, p.Candidates.Single()), default)).Should().ThrowAsync<BellScheduleConflictException>();
        var red = await service.CandidatesAsync(1, Date, 1, "Substitution", default);
        await service.Invoking(s => s.ExecuteAsync(1, Request(red, red.Candidates.Single(), "Ignore conflict"), default)).Should().ThrowAsync<ArgumentException>();
        db.TimetableSubstitutions.Should().BeEmpty();
    }
    [Theory] [InlineData(RoleNames.Secretary)] [InlineData(RoleNames.SchoolManager)]
    public async Task Authorized_yellow_acceptance_requires_and_records_reason(string role)
    {
        await using var db = await Seed(); db.Set<ClassSubjectRequirement>().Single().TimePreference = "Early";
        db.Set<ClassSubjectRequirement>().Single().LatestPreferredPeriodSequence = 0; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var service = Service(db, new User(Role: role)); var p = await service.CandidatesAsync(1, Date, 1, "Substitution", default);
        var candidate = p.Candidates.Single(); candidate.Color.Should().Be("Yellow");
        await service.Invoking(s => s.ExecuteAsync(1, Request(p, candidate, "  "), default)).Should().ThrowAsync<ArgumentException>();
        var result = await service.ExecuteAsync(1, Request(p, candidate, "  Essential cover  "), default);
        result.Reason.Should().Be("Essential cover");
    }
    [Fact] public async Task Unauthorized_yellow_override_school_leak_and_viewer_execution_are_blocked()
    {
        await using var db = await Seed();
        await Service(db, new User(School: 2)).Invoking(s => s.DailyAsync(1, Date, default)).Should().ThrowAsync<KeyNotFoundException>();
        await Service(db, new User(Manage: false, Role: RoleNames.Moderator)).Invoking(s => s.CandidatesAsync(1, Date, 1, "Substitution", default)).Should().ThrowAsync<UnauthorizedAccessException>();
        db.Set<ClassSubjectRequirement>().Single().TimePreference = "Early"; db.Set<ClassSubjectRequirement>().Single().LatestPreferredPeriodSequence = 0;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var service = Service(db, new User(Role: RoleNames.Moderator)); var p = await service.CandidatesAsync(1, Date, 1, "Substitution", default);
        await service.Invoking(s => s.ExecuteAsync(1, Request(p, p.Candidates.Single(), "reason"), default)).Should().ThrowAsync<UnauthorizedAccessException>();
    }
    [Fact] public async Task Notification_outbox_dispatch_is_deduplicated_and_notifies_both_teachers()
    {
        await using var db = await Seed(); var service = Service(db); var p = await service.CandidatesAsync(1, Date, 1, "Substitution", default);
        await service.ExecuteAsync(1, Request(p, p.Candidates.Single()), default);
        var e = JsonSerializer.Deserialize<TeacherTimetableChangedEvent>(db.OutboxMessages.Single().PayloadJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        e.SubstitutionId.Should().BeGreaterThan(0);
        var dispatcher = new StudentAffairsNotificationDispatcher(db, new Clock());
        await dispatcher.ProcessAsync(e, default); await db.SaveChangesAsync();
        await dispatcher.ProcessAsync(e, default); await db.SaveChangesAsync();
        db.Notifications.Select(n => n.UserId).Should().BeEquivalentTo(["teacher1", "teacher2"]);
    }
    private static TimetableValidationContext Context()
    {
        var c = TimetableReviewPhase7Tests.Context();
        return c with { Subjects = c.Subjects.ToList(), Requirements = c.Requirements.ToList(), Assignments = c.Assignments.ToList() };
    }
    private static Dictionary<int, int> Assigned(TimetableValidationContext c) => c.Timetable.Entries.ToDictionary(e => e.Id, e => e.InstructorProfileId);
    private static TimetableValidationContext PairContext()
    {
        var c = Context(); c.Requirements[0].IndividualPeriodCount = 0; c.Requirements[0].PairedBlockCount = 1;
        c.Assignments[0].Members.Single().AllocatedPeriodCount = 2; c.Assignments[0].Members.Single().AllocatedPairedBlockCount = 1;
        c.Timetable.Entries.Add(Copy(c.Timetable.Entries.First(), 2, 1, 2)); return c;
    }
    private static SchoolTimetableEntry Copy(SchoolTimetableEntry e, int id, int teacher, int period) => new() {
        Id = id, SchoolId = 1, SchoolTimetableId = 1, InstructorProfileId = teacher, ClassroomId = e.ClassroomId,
        SubjectId = e.SubjectId, ClassSubjectRequirementId = e.ClassSubjectRequirementId, EntryType = TimetableEntryType.Lesson,
        ClassLabel = e.ClassLabel, Subject = e.Subject, Day = e.Day, Period = period };
    private static void AddLesson(TimetableValidationContext c, int id, int teacher, int classroom, int period)
    {
        var subject = new SubjectDefinition { Id = id, SchoolId = 1, Name = "Subject " + id };
        var requirement = new ClassSubjectRequirement { Id = id, SchoolId = 1, TimetableSetupProfileId = 1, ClassroomId = classroom,
            Classroom = c.Classrooms.First(x => x.Id == classroom), SubjectId = id, Subject = subject, IndividualPeriodCount = 1 };
        var assignment = new TeachingAssignment { Id = id, SchoolId = 1, TimetableSetupProfileId = 1, ClassSubjectRequirementId = id,
            Members = [new() { Id = id, SchoolId = 1, TimetableSetupProfileId = 1, TeacherTimetableProfileId = teacher, AllocatedPeriodCount = 1 }] };
        // Fixtures use mutable lists for extension while production exposes readonly collections.
        ((List<SubjectDefinition>)c.Subjects).Add(subject); ((List<ClassSubjectRequirement>)c.Requirements).Add(requirement); ((List<TeachingAssignment>)c.Assignments).Add(assignment);
        c.Timetable.Entries.Add(new() { Id = id, SchoolId = 1, SchoolTimetableId = 1, InstructorProfileId = teacher, ClassroomId = classroom,
            SubjectId = id, ClassSubjectRequirementId = id, ClassLabel = "Class " + classroom, Subject = subject.Name, Day = TimetableDay.Sunday,
            Period = period, EntryType = TimetableEntryType.Lesson });
    }
    private static void AddNewGraph(AlFalahDbContext db, TimetableValidationContext c)
    {
        foreach (var r in c.Requirements.Where(x => x.Id > 1)) r.Classroom = db.Classrooms.Single(x => x.Id == r.ClassroomId);
        db.AddRange(c.Subjects.Where(x => x.Id > 1)); db.AddRange(c.Requirements.Where(x => x.Id > 1)); db.AddRange(c.Assignments.Where(x => x.Id > 1));
        db.AddRange(c.Timetable.Entries.Where(x => x.Id > 1));
    }
    private static async Task<AlFalahDbContext> Seed()
    {
        var db = await TimetableReviewPhase7Tests.Seed();
        var year = db.AcademicYears.Single(); year.StartsOn = new(2026, 1, 1); year.EndsOn = new(2026, 12, 31);
        var t = db.SchoolTimetables.Single(); t.IsPublished = true; t.PublishedAt = Expires.AddDays(-1);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return db;
    }
    private static ExecuteSwapRequest Request(SwapCandidatesDto p, SwapCandidateDto c, string? reason = null) =>
        new(Guid.NewGuid(), p.Revision, p.Date, p.SourceEntryId, p.Mode, c.Id, p.ExpiresAt, reason);
    private static TimetableSubstitutionService Service(AlFalahDbContext db, User? user = null)
    {
        user ??= new(); var repo = new TimetableSubstitutionRepository(db);
        return new(repo, Engine, new(repo, Validator, new(Validator), user), Validator, user, new Clock());
    }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Expires.AddMinutes(-5); }
    private sealed record User(int School = 1, string Role = RoleNames.SchoolManager, bool Manage = true) : ICurrentUserService
    {
        public string? UserId => "manager"; public string? Username => "manager"; public int? ActiveSchoolId => School;
        public string? PreferredLanguage => "ar"; public bool IsAuthenticated => true;
        public bool IsInRole(string role) => Role == role;
        public bool HasPermission(string p) => p == PermissionNames.TimetableView || Manage && p == PermissionNames.TimetableManage;
        public IEnumerable<string> GetRoles() => [Role]; public IEnumerable<string> GetPermissions() => [PermissionNames.TimetableView];
        public bool IsGlobalAdmin() => false; public bool IsSchoolScopedRole() => true;
    }
}
