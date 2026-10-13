using System.Text.Json;
using AlFalah.Application.Duty;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Duty;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AlFalah.Tests.Duty;

public sealed class DutyTests
{
    private static readonly DateOnly Start = new(2030, 1, 6);
    internal static DutySettings Settings(int termId = 1, string[]? ids = null, int days = 20) => new(termId, Start, Start.AddDays(days-1), [0,1,2,3,4,5,6], ids ?? ["A","B","C"], [], [], Start, Start.AddDays(days-1), true);
    [Fact]
    public void Ten_teachers_twenty_dates_are_equal_and_deterministic()
    {
        var s = Settings(ids: Enumerable.Range(1,10).Select(x => x.ToString()).ToArray());
        var first = DutyRotation.Generate(s); first.Should().Equal(DutyRotation.Generate(s));
        first.GroupBy(x => x.Teacher).Select(x => x.Count()).Should().OnlyContain(x => x == 2);
    }
    [Fact]
    public void Closure_consumes_no_turn_and_date_exclusion_is_respected()
    {
        var s = Settings(days: 5) with { Closures = [new(Start.AddDays(1), Start.AddDays(1), "Holiday")], Exclusions = [new("A", Start, Start, "Exclude") ] };
        var dates = DutyRotation.Generate(s); dates.Should().HaveCount(4);
        dates.Select(x => x.Teacher).Should().Equal("B", "C", "A", "B");
        dates.Should().NotContain(x => x.Date == Start.AddDays(1));
    }
    private static DutyState State() => new()
    {
        Plans = [new DutyPlan { Id = 1, SchoolId = 1, SettingsJson = JsonSerializer.Serialize(Settings()) }],
        Teachers = [new(1,"A","A"),new(2,"B","B"),new(3,"C","C")],
        Assignments = [new() { Id = 1, DutyPlanId=1, SchoolId=1, Date=Start, OriginalTeacherId="A", EffectiveTeacherId="A" },
            new() { Id=2, DutyPlanId=1, SchoolId=1, Date=Start.AddDays(1), OriginalTeacherId="B", EffectiveTeacherId="B" },
            new() { Id=3, DutyPlanId=1, SchoolId=1, Date=Start.AddDays(2), OriginalTeacherId="C", EffectiveTeacherId="C" }]
    };
    private static AttendanceRecord Fact(string user, DateOnly date, AttendanceStatus status) => new() { Id=1, SchoolId=1, UserId=user, AttendanceDate=date, Status=status, UpdatedAt=DateTimeOffset.UtcNow };
    private static DutyReconciler Engine() => new(new DutyRepository(new AlFalahDbContext(new DbContextOptionsBuilder<AlFalahDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options)));
    [Fact]
    public void Repeated_absence_uses_effective_queue_and_retry_does_not_duplicate()
    {
        var state = State(); var engine = Engine(); state.Attendance.Add(Fact("A",Start,AttendanceStatus.Absent));
        engine.Reconcile(state,1,Start); state.Assignments.Select(x=>x.EffectiveTeacherId).Should().Equal("B","A","C");
        engine.Reconcile(state,1,Start); state.Changes.Should().ContainSingle();
        state.Attendance.Add(Fact("A",Start.AddDays(1),AttendanceStatus.Excused)); engine.Reconcile(state,1,Start.AddDays(1));
        state.Assignments.Select(x=>x.EffectiveTeacherId).Should().Equal("B","C","A");
        state.Assignments.Select(x=>x.OriginalTeacherId).Should().Equal("A","B","C");
        state.Changes.Should().HaveCount(2); state.Changes[1].OtherDate.Should().Be(Start.AddDays(2));
        state.Changes[0].AttendanceUpdatedAt.Should().NotBeNull();
    }
    [Fact]
    public void Missing_attendance_is_unknown_and_absent_excluded_inactive_candidates_are_skipped()
    {
        var state = State(); var engine = Engine(); engine.Reconcile(state,1,Start); state.Changes.Should().BeEmpty();
        state.Attendance.AddRange([Fact("A",Start,AttendanceStatus.Absent),Fact("B",Start,AttendanceStatus.Excused)]);
        engine.Reconcile(state,1,Start); state.Assignments.Select(x=>x.EffectiveTeacherId).Should().Equal("C","B","A");
        var excluded = State(); excluded.Plans[0].SettingsJson = JsonSerializer.Serialize(Settings() with { Exclusions=[new("B",Start,Start,"Exclude")] });
        excluded.Attendance.Add(Fact("A",Start,AttendanceStatus.Absent)); engine.Reconcile(excluded,1,Start); excluded.Assignments[0].EffectiveTeacherId.Should().Be("C");
        var inactive = State(); inactive.Teachers.RemoveAll(x=>x.UserId=="B"); inactive.Attendance.Add(Fact("A",Start,AttendanceStatus.Absent)); engine.Reconcile(inactive,1,Start); inactive.Assignments[0].EffectiveTeacherId.Should().Be("C");
        var inactiveOwner=State();inactiveOwner.Teachers.RemoveAll(x=>x.UserId=="A");engine.Reconcile(inactiveOwner,1,Start);
        inactiveOwner.Assignments[0].ExceptionCode.Should().Be("EligibilityReview");inactiveOwner.Assignments[0].EffectiveTeacherId.Should().Be("A");
    }
    [Fact]
    public void Correction_undoes_only_exact_unmodified_pair_and_locks_require_review()
    {
        var state=State();var engine=Engine();state.Attendance.Add(Fact("A",Start,AttendanceStatus.Absent));engine.Reconcile(state,1,Start);
        // Persisted event identity is assigned by SQL; model it here for the undo relation.
        state.Changes[0].Id=1;state.Attendance[0].Status=AttendanceStatus.Present;engine.Reconcile(state,1,Start);
        state.Assignments.Select(x=>x.EffectiveTeacherId).Should().Equal("A","B","C");state.Changes.Last().Kind.Should().Be("Undo");
        var locked=State();locked.Attendance.Add(Fact("A",Start,AttendanceStatus.Absent));engine.Reconcile(locked,1,Start);locked.Changes[0].Id=1;
        locked.Assignments[1].IsLocked=true;locked.Attendance[0].Status=AttendanceStatus.Present;engine.Reconcile(locked,1,Start);
        locked.Assignments[0].EffectiveTeacherId.Should().Be("B");locked.Assignments[0].ExceptionCode.Should().Be("AttendanceCorrectionReview");
    }
    [Fact]
    public void Manual_protection_and_end_of_term_obligation_are_retained()
    {
        var state=State();state.Assignments[0].IsManuallyProtected=true;state.Attendance.Add(Fact("A",Start,AttendanceStatus.Absent));Engine().Reconcile(state,1,Start);
        state.Assignments[0].EffectiveTeacherId.Should().Be("A");state.Assignments[0].ExceptionCode.Should().Be("ManualAbsenceReview");
        var end=State();end.Assignments.RemoveRange(1,2);end.Attendance.Add(Fact("A",Start,AttendanceStatus.Absent));var engine=Engine();engine.Reconcile(end,1,Start);engine.Reconcile(end,1,Start);
        end.Assignments[0].EffectiveTeacherId.Should().Be("B");end.Changes.Should().ContainSingle();DutyRotation.Unresolved(end.Changes[0],end.Changes).Should().BeTrue();
        var none=State();none.Attendance.AddRange([Fact("A",Start,AttendanceStatus.Absent),Fact("B",Start,AttendanceStatus.Absent),Fact("C",Start,AttendanceStatus.Excused)]);engine.Reconcile(none,1,Start);engine.Reconcile(none,1,Start);
        none.Assignments[0].EffectiveTeacherId.Should().Be("A");none.Assignments[0].ExceptionCode.Should().Be("NoEligibleSubstitute");none.Changes.Should().ContainSingle();
        // Releasing/changing a manual decision must not hide a still unresolved exception.
        none.Assignments[0].ExceptionCode=null;none.Assignments[0].Revision++;engine.Reconcile(none,1,Start);
        none.Assignments[0].ExceptionCode.Should().Be("NoEligibleSubstitute");none.Changes.Should().HaveCount(2);
    }
}
