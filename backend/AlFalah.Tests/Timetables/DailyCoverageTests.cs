using AlFalah.Application.IntelligentTimetable;
using FluentAssertions;
using Xunit;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.Common;
using AlFalah.Application.DTOs.Attendance;
using AlFalah.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlFalah.Tests.Timetables;

public sealed class DailyCoverageTests
{
    internal static readonly DateOnly Date = new(2026, 9, 13);
    internal sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 9, 13, 6, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    [Fact]
    public void Free_teacher_without_scheduled_standby_cannot_cover_a_lesson()
    {
        var context = TimetableReviewPhase7Tests.Context();
        var engine = new TimetableSwapEngine(new TimetableValidationEngine());
        var candidates = engine.Candidates(context, 1, "Substitution", new(2026, 9, 13),
            new(2026, 9, 13, 4, 5, 0, TimeSpan.Zero), context.Timetable.Entries.ToDictionary(e => e.Id, e => e.InstructorProfileId), default);
        candidates.Should().ContainSingle().Which.Color.Should().Be("Red");
    }

    [Fact]
    public async Task Repeated_absence_reconciliation_prepares_one_need_without_approving_cover()
    {
        await using var db = await Seed(); var repo = new TimetableSubstitutionRepository(db);
        var reconciler = new DailyCoverageReconciler(repo, new Clock());
        await reconciler.ReconcileAsync(1, Date, default); await reconciler.ReconcileAsync(1, Date, default);
        var needs = await repo.NeedsAsync(1, Date, default);
        needs.Should().ContainSingle().Which.State.Should().Be("Needed");
        needs.Single().ActiveSubstitutionId.Should().BeNull();
        (await repo.GetChangesAsync(1, 1, Date, default)).Should().BeEmpty();
    }

    internal static async Task<AlFalahDbContext> Seed()
    {
        var db = await TimetableReviewPhase7Tests.Seed();
        var year = await db.AcademicYears.SingleAsync(); year.StartsOn = new(2026, 8, 1); year.EndsOn = new(2027, 7, 31);
        var timetable = await db.SchoolTimetables.SingleAsync(); timetable.IsPublished = true;
        db.AcademicTerms.Add(new() { SchoolId = 1, AcademicYearId = 1, Semester = TimetableSemester.First, IsActive = true,
            StartsOn = new(2026, 8, 1), EndsOn = new(2027, 1, 31), CreatedByUserId = "manager", UpdatedByUserId = "manager" });
        db.AttendanceRecords.Add(new() { SchoolId = 1, UserId = "teacher1", AttendanceDate = Date, Status = AttendanceStatus.Absent, RecordedByUserId = "manager" });
        db.SchoolTimetableEntries.Add(new() { SchoolId = 1, SchoolTimetableId = 1, InstructorProfileId = 2, Day = TimetableDay.Sunday, Period = 1, EntryType = TimetableEntryType.Standby });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return db;
    }
    internal static (DailyCoverageService Coverage, CoverageExecutionService Execution) Services(AlFalahDbContext db, Clock clock, User? user = null)
    {
        user ??= new(); var repo = new TimetableSubstitutionRepository(db); var validator = new TimetableValidationEngine();
        var review = new TimetableReviewService(repo, validator, new(validator), user);
        var substitutions = new TimetableSubstitutionService(repo, new(validator), review, validator, user, clock);
        var access = new CoverageAccess(repo, user);
        return (new(repo, new(repo, clock), access, new(validator), substitutions, clock), new(repo, access, clock, review));
    }
    internal static async Task<CoverageNeedDto> Assign(AlFalahDbContext db, Clock clock)
    {
        var s = Services(db, clock).Coverage; await s.RefreshAsync(Date, default);
        var request = new CoveragePreviewRequest(Date, "Fair", [], clock.Now.AddMinutes(4));
        var preview = await s.PreviewAsync(request, default);
        preview.Assignments.Should().ContainSingle().Which.TeacherId.Should().Be(2);
        var approval = new CoverageApproveRequest(Guid.NewGuid(), request, preview.ProposalId, null);
        var board = await s.ApproveAsync(approval, default);
        await s.ApproveAsync(approval, default); // A lost response must not duplicate work.
        (await db.Set<TimetableSubstitution>().CountAsync()).Should().Be(1);
        return board.Needs.Single();
    }
    internal static User Teacher => new("teacher2", RoleNames.Instructor);
    internal static string Signature(byte variant = 0)
    {
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aGHoAAAAASUVORK5CYII=");
        bytes[^1] = variant; return "data:image/png;base64," + Convert.ToBase64String(bytes);
    }
    [Fact]
    public async Task Preview_is_non_mutating_and_approval_has_one_effective_owner_and_one_event()
    {
        await using var db = await Seed(); var clock = new Clock();
        var n = await Assign(db, clock); n.State.Should().Be("Assigned");
        n.AssignedTeacherId.Should().Be(2);
        (await db.OutboxMessages.CountAsync()).Should().Be(1);
        var repo = new TimetableSubstitutionRepository(db);
        var context = (await repo.GetValidationContextAsync(1, 1, default))!;
        var effective = TimetableSubstitutionService.Effective(context, await repo.GetChangesAsync(1, 1, Date, default));
        effective.Timetable.Entries.Single(e => e.EntryType == TimetableEntryType.Lesson).InstructorProfileId.Should().Be(2);
        context.Timetable.Entries.Single(e => e.EntryType == TimetableEntryType.Lesson).InstructorProfileId.Should().Be(1);
    }
    [Fact]
    public async Task Signed_receipt_and_execution_are_separate_retained_versions_and_correction_counts_once()
    {
        await using var db = await Seed(); var clock = new Clock(); var n = await Assign(db, clock);
        db.Set<UserSignature>().Add(new() { UserId = "teacher2", SignatureDrawnData = Signature(), UpdatedAt = clock.Now }); await db.SaveChangesAsync();
        var teacher = Services(db, clock, Teacher).Execution;
        var receipt = new CoverageActionRequest(Guid.NewGuid(), n.Revision, n.SubstitutionId!.Value);
        var detail = await teacher.ActAsync(n.Id, "Acknowledgement", receipt, default);
        detail.CanExecute.Should().BeTrue(); detail.Need.SubmittedAt.Should().BeNull();
        await teacher.ActAsync(n.Id, "Acknowledgement", receipt, default);
        detail = await teacher.SaveDraftAsync(n.Id, new(detail.Need.Revision, n.SubstitutionId.Value, "Worked through revision exercises", "No incidents"), default);
        var sig = await db.Set<UserSignature>().SingleAsync(); sig.SignatureDrawnData = Signature(1); sig.UpdatedAt = clock.Now.AddMinutes(1); await db.SaveChangesAsync();
        var submit = new CoverageActionRequest(Guid.NewGuid(), detail.Need.Revision, n.SubstitutionId.Value);
        detail = await teacher.ActAsync(n.Id, "Execution", submit, default);
        await teacher.ActAsync(n.Id, "Execution", submit, default);
        detail.Need.State.Should().Be("Executed");
        detail.History.Single(r => r.Kind == "Acknowledgement").SignatureHash.Should().NotBe(detail.History.Single(r => r.Kind == "Execution").SignatureHash);
        detail = await Services(db, clock).Execution.ActAsync(n.Id, "Reopen", new(Guid.NewGuid(), detail.Need.Revision, n.SubstitutionId.Value, "Clarify activity"), default);
        detail = await teacher.SaveDraftAsync(n.Id, new(detail.Need.Revision, n.SubstitutionId.Value, "Corrected execution", ""), default);
        await teacher.ActAsync(n.Id, "Execution", new(Guid.NewGuid(), detail.Need.Revision, n.SubstitutionId.Value), default);
        var board = await Services(db, clock).Coverage.BoardAsync(Date, default);
        board.Teachers.Single(t => t.Id == 2).ExecutedPeriods.Should().Be(1);
        board.Teachers.Single(t => t.Id == 2).PendingPeriods.Should().Be(0);
        (await db.Set<CoverageRecord>().CountAsync(r => r.Kind == "Execution")).Should().Be(2);
        var record = await db.Set<CoverageRecord>().FirstAsync(r => r.Kind == "Execution"); record.Narrative = "tamper";
        await db.Invoking(d => d.SaveChangesAsync()).Should().ThrowAsync<InvalidOperationException>();
    }
    [Fact]
    public async Task Absent_substitute_blocks_execution_and_corrected_original_absence_requires_review()
    {
        await using var db = await Seed(); var clock = new Clock(); var n = await Assign(db, clock);
        db.AttendanceRecords.Add(new() { SchoolId = 1, UserId = "teacher2", AttendanceDate = Date, Status = AttendanceStatus.Absent, RecordedByUserId = "manager" }); await db.SaveChangesAsync();
        await new DailyCoverageReconciler(new TimetableSubstitutionRepository(db), clock).ReconcileAsync(1, Date, default);
        var detail = await Services(db, clock, Teacher).Execution.DetailAsync(n.Id, default);
        detail.Need.State.Should().Be("ReviewRequired"); detail.CanAcknowledge.Should().BeFalse();
        (await db.Set<TimetableSubstitution>().CountAsync()).Should().Be(1);
        var absent = await db.AttendanceRecords.SingleAsync(r => r.UserId == "teacher1"); absent.Status = AttendanceStatus.Present; await db.SaveChangesAsync();
        await new DailyCoverageReconciler(new TimetableSubstitutionRepository(db), clock).ReconcileAsync(1, Date, default);
        detail = await Services(db, clock).Execution.DetailAsync(n.Id, default);
        detail.Need.IsRequired.Should().BeFalse(); detail.Need.SubstitutionId.Should().Be(n.SubstitutionId);
    }
    [Fact]
    public async Task Stale_preview_missing_signature_wrong_owner_and_foreign_school_are_rejected()
    {
        await using var db = await Seed(); var clock = new Clock(); var s = Services(db, clock).Coverage;
        var board = await s.RefreshAsync(Date, default); var request = new CoveragePreviewRequest(Date, "Fair", [], clock.Now.AddMinutes(4));
        var preview = await s.PreviewAsync(request, default);
        var need = await db.Set<DailyCoverageNeed>().SingleAsync(); need.Revision++; await db.SaveChangesAsync();
        await s.Invoking(x => x.ApproveAsync(new(Guid.NewGuid(), request, preview.ProposalId, null), default)).Should().ThrowAsync<BellScheduleConflictException>();
        var n = await Assign(db, clock); var teacher = Services(db, clock, Teacher).Execution;
        await teacher.Invoking(x => x.ActAsync(n.Id, "Acknowledgement", new(Guid.NewGuid(), n.Revision, n.SubstitutionId!.Value), default)).Should().ThrowAsync<ArgumentException>();
        await Services(db, clock, new("teacher1", RoleNames.Instructor)).Execution.Invoking(x => x.DetailAsync(n.Id, default)).Should().ThrowAsync<UnauthorizedAccessException>();
        await Services(db, clock, new(School: 2)).Execution.Invoking(x => x.DetailAsync(n.Id, default)).Should().ThrowAsync<KeyNotFoundException>();
        await teacher.Invoking(x => x.SaveDraftAsync(n.Id, new(n.Revision - 1, n.SubstitutionId!.Value, "text", ""), default)).Should().ThrowAsync<BellScheduleConflictException>();
    }
    [Fact]
    public async Task Manual_requires_a_choice_and_simultaneous_needs_cannot_double_book_one_standby_teacher()
    {
        await using var db = await Seed(); var clock = new Clock();
        var c = (await new TimetableSubstitutionRepository(db).GetValidationContextAsync(1, 1, default))!;
        var third = new InstructorProfile { Id = 3, SchoolId = 1, UserId = "teacher3", IsActive = true, User = new() { Id = "teacher3", FirstName = "Absent", LastName = "Three", IsActive = true } };
        db.Add(new TeacherTimetableProfile { Id = 3, SchoolId = 1, TimetableSetupProfileId = 1, Instructor = third, BellScheduleRevisionId = 1, MaximumWeeklyPeriods = 20 });
        var subject = new SubjectDefinition { Id = 3, SchoolId = 1, Name = "Science" }; db.Add(subject);
        db.Add(new ClassSubjectRequirement { Id = 3, SchoolId = 1, TimetableSetupProfileId = 1, ClassroomId = 2, SubjectId = 3, IndividualPeriodCount = 1 });
        db.Add(new TeachingAssignment { Id = 3, SchoolId = 1, TimetableSetupProfileId = 1, ClassSubjectRequirementId = 3, Members = [new() { SchoolId = 1, TimetableSetupProfileId = 1, TeacherTimetableProfileId = 3, AllocatedPeriodCount = 1 }] });
        db.Add(new SchoolTimetableEntry { Id = 30, SchoolId = 1, SchoolTimetableId = 1, InstructorProfileId = 3, Day = TimetableDay.Sunday, Period = 1, EntryType = TimetableEntryType.Lesson, ClassroomId = 2, SubjectId = 3, ClassSubjectRequirementId = 3, ClassLabel = "Class 2", Subject = "Science" });
        db.AttendanceRecords.Add(new() { SchoolId = 1, UserId = "teacher3", AttendanceDate = Date, Status = AttendanceStatus.Excused, RecordedByUserId = "manager" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear(); var service = Services(db, clock).Coverage;
        var board = await service.RefreshAsync(Date, default); board.Needs.Should().HaveCount(2);
        var manual = new CoveragePreviewRequest(Date, "Manual", [], clock.Now.AddMinutes(4));
        (await service.PreviewAsync(manual, default)).Assignments.Should().BeEmpty();
        var fair = await service.PreviewAsync(manual with { Mode = "Fair" }, default);
        fair.Assignments.Should().ContainSingle(); fair.UncoveredNeedIds.Should().ContainSingle();
        var pinned = board.Needs.Last();
        fair = await service.PreviewAsync(manual with { Mode = "Fair", Choices = [new(pinned.Id, 2)] }, default);
        fair.Assignments.Should().ContainSingle().Which.NeedId.Should().Be(pinned.Id);
        await service.Invoking(s => s.PreviewAsync(manual with { Choices = board.Needs.Select(n => new CoverageChoice(n.Id, 2)).ToArray() }, default)).Should().ThrowAsync<ArgumentException>();
        (await db.Set<TimetableSubstitution>().CountAsync()).Should().Be(0);
    }
    [Fact]
    public async Task Receipt_can_precede_lesson_but_execution_cannot_and_unable_is_not_execution()
    {
        await using var db = await Seed(); var clock = new Clock { Now = new(2026, 9, 13, 4, 0, 0, TimeSpan.Zero) }; var n = await Assign(db, clock);
        db.Add(new UserSignature { UserId = "teacher2", SignatureDrawnData = Signature() }); await db.SaveChangesAsync();
        var teacher = Services(db, clock, Teacher).Execution;
        var d = await teacher.ActAsync(n.Id, "Acknowledgement", new(Guid.NewGuid(), n.Revision, n.SubstitutionId!.Value), default);
        d.CanExecute.Should().BeFalse();
        d = await teacher.SaveDraftAsync(n.Id, new(d.Need.Revision, n.SubstitutionId.Value, "Draft before lesson", ""), default);
        await teacher.Invoking(s => s.ActAsync(n.Id, "Execution", new(Guid.NewGuid(), d.Need.Revision, n.SubstitutionId.Value), default)).Should().ThrowAsync<ArgumentException>();
        d = await teacher.ActAsync(n.Id, "Unable", new(Guid.NewGuid(), d.Need.Revision, n.SubstitutionId.Value, "Unexpected emergency"), default);
        d.Need.State.Should().Be("Unable"); d.CanEditDraft.Should().BeFalse();
        (await Services(db, clock).Coverage.BoardAsync(Date, default)).Teachers.Single(t => t.Id == 2).ExecutedPeriods.Should().Be(0);
        await Services(db, clock).Execution.ActAsync(n.Id, "Release", new(Guid.NewGuid(), d.Need.Revision, n.SubstitutionId.Value, "Arrange another teacher"), default);
        (await teacher.MyTasksAsync(1, 10, default)).TotalCount.Should().Be(0);
        var repo = new TimetableSubstitutionRepository(db); var c = (await repo.GetValidationContextAsync(1, 1, default))!;
        TimetableSubstitutionService.Effective(c, await repo.GetChangesAsync(1, 1, Date, default)).Timetable.Entries.Single(e => e.EntryType == TimetableEntryType.Lesson).InstructorProfileId.Should().Be(1);
    }
    [Fact]
    public async Task Secretary_attendance_enqueues_durable_coverage_once_and_worker_retries_do_not_duplicate_needs()
    {
        await using var db = await Seed(); var clock = new Clock();
        db.Roles.AddRange(new() { Id = "sec", Name = RoleNames.Secretary }, new() { Id = "teach", Name = RoleNames.Instructor });
        db.UserSchoolRoles.AddRange(new() { UserId = "manager", SchoolId = 1, RoleId = "sec", IsActive = true },
            new() { UserId = "teacher1", SchoolId = 1, RoleId = "teach", IsActive = true }, new() { UserId = "teacher2", SchoolId = 1, RoleId = "teach", IsActive = true });
        await db.SaveChangesAsync(); var user = new User(Role: RoleNames.Secretary);
        var attendance = new AttendanceService(db, user, new SchoolScopeGuard(db, user, NullLogger<SchoolScopeGuard>.Instance));
        var sheet = new SaveAttendanceSheetRequestDto(Date, [new("teacher1", AttendanceStatus.Absent, null), new("teacher2", AttendanceStatus.Present, null)]);
        await attendance.SaveSheetAsync(sheet); await attendance.SaveSheetAsync(sheet);
        (await db.Set<CoverageReconciliationSignal>().CountAsync()).Should().Be(1);
        var repo = new TimetableSubstitutionRepository(db); var ids = await repo.ClaimSignalsAsync("worker-one", clock.Now, default); ids.Should().ContainSingle();
        (await repo.ClaimSignalsAsync("worker-two", clock.Now, default)).Should().BeEmpty();
        await new DailyCoverageReconciler(repo, clock).ProcessSignalAsync(ids[0], "worker-one", default);
        await new DailyCoverageReconciler(repo, clock).ProcessSignalAsync(ids[0], "worker-one", default);
        (await db.Set<DailyCoverageNeed>().CountAsync()).Should().Be(1); (await db.Set<TimetableSubstitution>().CountAsync()).Should().Be(0);
        (await db.Set<CoverageReconciliationSignal>().SingleAsync()).ProcessedAt.Should().NotBeNull();
    }
    [Fact]
    public async Task Delegated_editor_can_manage_but_instructor_and_undelegated_viewer_cannot()
    {
        await using var db = await Seed(); var clock = new Clock(); var moderator = new User(Role: RoleNames.Moderator);
        await Services(db, clock, moderator).Coverage.Invoking(s => s.RefreshAsync(Date, default)).Should().ThrowAsync<UnauthorizedAccessException>();
        db.TimetableEditorGrants.Add(new() { SchoolId = 1, ModeratorUserId = "manager", GrantedByUserId = "manager" }); await db.SaveChangesAsync();
        var board = await Services(db, clock, moderator).Coverage.RefreshAsync(Date, default);
        board.CanManage.Should().BeTrue(); board.CanOverride.Should().BeFalse();
        await Services(db, clock, new(Role: RoleNames.Instructor)).Coverage.Invoking(s => s.RefreshAsync(Date, default)).Should().ThrowAsync<UnauthorizedAccessException>();
    }
    [Fact]
    public async Task Reassignment_invalidates_the_old_teacher_draft_and_preserves_previous_receipt()
    {
        await using var db = await Seed(); var clock = new Clock(); var n = await Assign(db, clock);
        var third = new InstructorProfile { Id = 3, SchoolId = 1, UserId = "teacher3", IsActive = true, User = new() { Id = "teacher3", FirstName = "Replacement", LastName = "Three", IsActive = true } };
        db.Add(new TeacherTimetableProfile { Id = 3, SchoolId = 1, TimetableSetupProfileId = 1, Instructor = third, BellScheduleRevisionId = 1, MaximumWeeklyPeriods = 20 });
        db.Add(new SchoolTimetableEntry { SchoolId = 1, SchoolTimetableId = 1, InstructorProfileId = 3, Day = TimetableDay.Sunday, Period = 1, EntryType = TimetableEntryType.Standby });
        db.Add(new UserSignature { UserId = "teacher2", SignatureDrawnData = Signature() }); await db.SaveChangesAsync();
        var owner = Services(db, clock, Teacher).Execution;
        var detail = await owner.ActAsync(n.Id, "Acknowledgement", new(Guid.NewGuid(), n.Revision, n.SubstitutionId!.Value), default);
        detail = await owner.SaveDraftAsync(n.Id, new(detail.Need.Revision, n.SubstitutionId.Value, "Previous teacher draft", ""), default);
        var service = Services(db, clock).Coverage; var request = new CoveragePreviewRequest(Date, "Manual", [new(n.Id, 3)], clock.Now.AddMinutes(4));
        var preview = await service.PreviewAsync(request, default);
        await service.ApproveAsync(new(Guid.NewGuid(), request, preview.ProposalId, "Replacement changed before execution"), default);
        await owner.Invoking(s => s.SaveDraftAsync(n.Id, new(detail.Need.Revision, n.SubstitutionId.Value, "Stale write", ""), default)).Should().ThrowAsync<UnauthorizedAccessException>();
        var replacement = await Services(db, clock, new("teacher3", RoleNames.Instructor)).Execution.DetailAsync(n.Id, default);
        replacement.Need.Narrative.Should().BeEmpty(); replacement.Need.AcknowledgedAt.Should().BeNull();
        replacement.History.Should().Contain(r => r.Kind == "Acknowledgement" && r.Actor == "teacher2");
        replacement.History.Should().Contain(r => r.Kind == "Reassignment" && r.Narrative == "Previous teacher draft");
    }
    internal sealed record User(string Id = "manager", string Role = RoleNames.SchoolManager, int School = 1) : ICurrentUserService
    {
        public string? UserId => Id; public string? Username => Id; public int? ActiveSchoolId => School;
        public string? PreferredLanguage => "ar"; public bool IsAuthenticated => true;
        public bool IsInRole(string role) => role == Role;
        public bool HasPermission(string p) => p == PermissionNames.TimetableView || Role == RoleNames.Secretary && p == PermissionNames.AttendanceManage;
        public IEnumerable<string> GetRoles() => [Role]; public IEnumerable<string> GetPermissions() => [PermissionNames.TimetableView];
        public bool IsGlobalAdmin() => false; public bool IsSchoolScopedRole() => true;
    }
}
