using AlFalah.Application.Notifications;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Entities.Notifications;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Notifications;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace AlFalah.Tests.StudentAffairs;

public sealed class NotificationCenterTransitionsTests
{
    private static CenterRepository Repo(AlFalahDbContext db) => new(db, Options.Create(new InboxOptions { CenterEnabled = true, JournalEnabled = true }), TimeProvider.System);
    private static async Task Grant(AlFalahDbContext db, string roleId, params string[] names)
    {
        foreach (var name in names)
        {
            var permission = await db.Permissions.SingleOrDefaultAsync(x => x.Name == name);
            if (permission is null) { permission = new() { Name = name }; db.Add(permission); await db.SaveChangesAsync(); }
            if (!await db.RolePermissions.AnyAsync(x => x.RoleId == roleId && x.PermissionId == permission.Id)) db.RolePermissions.Add(new() { RoleId = roleId, PermissionId = permission.Id });
        }
        await db.SaveChangesAsync();
    }
    private static async Task<AlFalahDbContext> Graph()
    {
        var db = await NotificationCenterTests.Seed(); db.CenterProjectionEnabled = false;
        foreach (var (id, role) in new[] { ("guardian", RoleNames.Guardian), ("worker", RoleNames.SocialWorker), ("worker2", RoleNames.SocialWorker), ("guard", RoleNames.SecurityGuard) })
        {
            db.Add(new ApplicationUser { Id = id }); db.Add(new ApplicationRole { Id = id, Name = role });
            db.Add(new UserSchoolRole { SchoolId = 1, UserId = id, RoleId = id });
        }
        db.Add(new Student { Id = 1, SchoolId = 1, FirstName = "Synthetic", LastName = "Student", IsActive = true });
        db.Add(new GuardianProfile { Id = 1, SchoolId = 1, ApplicationUserId = "guardian", IsActive = true });
        db.Add(new StudentGuardian { Id = 1, SchoolId = 1, StudentId = 1, GuardianProfileId = 1, ReceivesNotifications = true, ValidFrom = new(2020, 1, 1) });
        db.Add(new InstructorProfile { Id = 1, SchoolId = 1, UserId = "teacher", IsActive = true });
        db.Add(new AcademicYear { Id = 1, NameAr = "Synthetic" });
        db.Add(new AcademicTerm { Id = 1, SchoolId = 1, AcademicYearId = 1 });
        db.Add(new Classroom { Id = 1, SchoolId = 1, AcademicYearId = 1, ClassLabel = "Synthetic class" });
        db.Add(new DailyStudentAttendance { Id = 1, SchoolId = 1, StudentId = 1, AcademicTermId = 1, ClassroomId = 1 });
        await db.SaveChangesAsync();
        await Grant(db, "guardian", PermissionNames.NotificationViewOwn, PermissionNames.ClassroomEntryPermitView);
        await Grant(db, "teacher", PermissionNames.ClassroomEntryPermitAcknowledge, PermissionNames.GatePassAcknowledgeTeacher);
        await Grant(db, "officer", PermissionNames.AttendanceReviewExcuse, PermissionNames.ClassroomEntryPermitView, PermissionNames.GatePassApprove,
            PermissionNames.GatePassView, PermissionNames.ReferralView, PermissionNames.NotificationApproveDispatch, PermissionNames.SummonReviewAutomationImpact);
        await Grant(db, "worker", PermissionNames.ReferralView, PermissionNames.SummonView);
        await Grant(db, "worker2", PermissionNames.ReferralView, PermissionNames.SummonView);
        await Grant(db, "guard", PermissionNames.GatePassExecute);
        db.CenterProjectionEnabled = true; return db;
    }

    [Fact]
    public async Task DirectAttendanceAndDelayNoticesResolveTheirExactSourceAndDisappearAfterCorrectionOrDeletion()
    {
        await using var db = await Graph(); var repo = Repo(db);
        var attendance = await db.DailyStudentAttendances.SingleAsync(); attendance.Status = StudentAttendanceStatus.Absent;
        var morning = new MorningArrivalDelay { SchoolId = 1, StudentId = 1, AcademicTermId = 1, SchoolLocalDate = new(2026, 10, 18), DelayMinutes = 12 };
        var session = new SessionDelay { SchoolId = 1, StudentId = 1, AcademicTermId = 1, ClassroomId = 1, ReportedByInstructorProfileId = 1, Period = 3, DelayMinutes = 7 };
        db.AddRange(morning, session); await db.SaveChangesAsync();
        foreach (var (type, id) in new[] { ("DailyStudentAttendance", attendance.Id), ("MorningArrivalDelay", morning.Id), ("SessionDelay", session.Id) })
            db.Add(new Notification { SchoolId = 1, UserId = "guardian", StudentId = 1, RelatedEntityType = type, RelatedEntityId = id.ToString(), DeliveryStatus = NotificationDeliveryStatus.Delivered });
        await db.SaveChangesAsync();
        var entries = await repo.ListAsync(new(1, "guardian"), new(), null, default);
        entries.Should().HaveCount(3);
        foreach (var entry in entries)
        {
            (await repo.TargetAsync(entry, default)).Route.Should().Be("/notifications/detail/" + entry.Id);
            (await repo.DetailAsync(entry, default)).Rows.Should().HaveCountGreaterThan(1);
        }
        attendance.Status = StudentAttendanceStatus.Present; morning.IsDeleted = true; session.IsDeleted = true; await db.SaveChangesAsync();
        (await repo.CountAsync(new(1, "guardian"), default)).Should().Be(0);
    }

    [Fact]
    public async Task ThresholdAlertKeepsItsCanonicalNoticeAndRequiresCurrentOfficerRoleAndMetric()
    {
        await using var db = await Graph(); var repo = Repo(db);
        db.Add(new StudentTermMetric { SchoolId = 1, StudentId = 1, AcademicTermId = 1, Count = 5 });
        var notice = new Notification { SchoolId = 1, UserId = "officer", StudentId = 1, Type = "PenaltyAbsenceThreshold", RelatedEntityType = "StudentTermMetric", RelatedEntityId = "1:1", DeliveryStatus = NotificationDeliveryStatus.Delivered };
        db.Add(notice); await db.SaveChangesAsync();
        var entry = (await repo.ListAsync(new(1, "officer"), new(), null, default)).Single();
        (await repo.DetailAsync(entry, default)).Action!.Route.Should().Be("/student-affairs/records/1");
        notice.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync();
        (await repo.CountAsync(new(1, "officer"), default)).Should().Be(1);
        (await db.UserSchoolRoles.SingleAsync(x => x.UserId == "officer")).IsActive = false; await db.SaveChangesAsync();
        (await repo.CountAsync(new(1, "officer"), default)).Should().Be(0);
    }

    [Fact]
    public async Task ExcuseSubmissionAndBothDecisionsReachOnlyReviewerAndOwningGuardian()
    {
        await using var db = await Graph(); var repo = Repo(db);
        var excuse = new AbsenceExcuse { SchoolId = 1, DailyStudentAttendanceId = 1, GuardianProfileId = 1, IdempotencyKey = "synthetic" };
        db.Add(excuse); await db.SaveChangesAsync();
        (await repo.ListAsync(new(1, "officer"), new(), null, default)).Should().ContainSingle(x => x.SourceType == "Excuse");
        (await repo.CountAsync(new(1, "guardian"), default)).Should().Be(0);
        foreach (var status in new[] { AbsenceExcuseStatus.Accepted, AbsenceExcuseStatus.Rejected })
        {
            excuse.Status = status; await db.SaveChangesAsync();
            var entries = await repo.ListAsync(new(1, "guardian"), new(), null, default);
            entries.Should().Contain(x => x.SourceKey.EndsWith(status.ToString()));
            var item = entries.First(x => x.SourceKey.EndsWith(status.ToString()));
            (await repo.DetailAsync(item, default)).Action!.Query!["excuseId"].Should().Be(excuse.Id.ToString());
        }
        (await repo.CountAsync(new(1, "teacher"), default)).Should().Be(0);
        var link = await db.StudentGuardians.SingleAsync(); link.ReceivesNotifications = false; await db.SaveChangesAsync();
        (await repo.CountAsync(new(1, "guardian"), default)).Should().Be(0);
    }

    [Fact]
    public async Task PermitLifecycleAndGatePassStagesHaveExactCurrentRecipientScope()
    {
        await using var db = await Graph(); var repo = Repo(db);
        var permit = new ClassroomEntryPermit { SchoolId = 1, StudentId = 1, ClassroomId = 1, TargetInstructorProfileId = 1, ValidUntil = DateTimeOffset.UtcNow.AddHours(1) };
        db.Add(permit); await db.SaveChangesAsync();
        foreach (var status in new[] { ClassroomEntryPermitStatus.Issued, ClassroomEntryPermitStatus.AcknowledgedByTeacher, ClassroomEntryPermitStatus.Revoked, ClassroomEntryPermitStatus.Expired })
        {
            permit.Status = status; await db.SaveChangesAsync();
            var item = (await repo.ListAsync(new(1, "teacher"), new(), null, default)).First(x => x.SourceType == "EntryPermit");
            (await repo.DetailAsync(item, default)).Action!.Query!["permitId"].Should().Be(permit.Id.ToString());
            (await repo.TaskStateAsync(item, default)).Should().Be(status == ClassroomEntryPermitStatus.Issued ? "pending" : "resolved");
        }
        var gate = new GatePass { SchoolId = 1, StudentId = 1, RequestedByGuardianProfileId = 1, CurrentInstructorProfileId = 1,
            ApprovedWindowEndsAt = DateTimeOffset.UtcNow.AddHours(1) }; db.Add(gate); await db.SaveChangesAsync();
        (await repo.ListAsync(new(1, "guardian"), new(), null, default)).Should().NotContain(x => x.SourceType == "GatePass");
        foreach (var status in new[] { GatePassStatus.Approved, GatePassStatus.SecurityAcknowledged, GatePassStatus.Exited, GatePassStatus.Cancelled, GatePassStatus.Rejected, GatePassStatus.Expired })
        {
            gate.Status = status; await db.SaveChangesAsync();
            var item = (await repo.ListAsync(new(1, "guardian"), new(), null, default)).First(x => x.SourceType == "GatePass");
            (await repo.DetailAsync(item, default)).Action!.Query!["gatePassId"].Should().Be(gate.Id.ToString());
        }
        db.Add(new GatePassTransition { SchoolId = 1, GatePassId = gate.Id, ActorRole = RoleNames.Instructor, ActorUserId = "teacher", Reason = "Acknowledged by teacher" }); await db.SaveChangesAsync();
        (await repo.ListAsync(new(1, "guard"), new(), null, default)).Should().Contain(x => x.SourceKey.StartsWith("gate-teacher-ack:"));
        (await repo.ListAsync(new(1, "manager"), new(), null, default)).Should().BeEmpty();
        permit.TargetInstructorProfileId = null; gate.CurrentInstructorProfileId = null; await db.SaveChangesAsync();
        (await repo.ListAsync(new(1, "teacher"), new(), null, default)).Should().BeEmpty();
    }

    [Fact]
    public async Task ReferralsCaseFollowupSummonAndAutomationRespectAssignmentAndDoNotProduceFromDashboardReads()
    {
        await using var db = await Graph(); var repo = Repo(db);
        var referral = new StudentReferral { SchoolId = 1, StudentId = 1, AssignedSocialWorkerUserId = "worker" }; db.Add(referral); await db.SaveChangesAsync();
        foreach (var status in Enum.GetValues<StudentReferralStatus>()) { referral.Status = status; await db.SaveChangesAsync(); }
        var action = new StudentCaseAction { SchoolId = 1, StudentReferralId = referral.Id, Description = "confidential", ActorUserId = "worker" }; db.Add(action); await db.SaveChangesAsync();
        var item = (await repo.ListAsync(new(1, "worker"), new(), null, default)).First(x => x.SourceType == "CaseAction");
        (await repo.DetailAsync(item, default)).Rows.Should().Contain(x => x.Value == "confidential");
        (await repo.ListAsync(new(1, "officer"), new(), null, default)).Should().NotContain(x => x.SourceType == "CaseAction");
        var summon = new GuardianSummon { SchoolId = 1, StudentId = 1, StudentReferralId = referral.Id, GuardianProfileId = 1 };
        db.Add(summon); await db.SaveChangesAsync();
        summon.ScheduledAt = DateTimeOffset.UtcNow.AddDays(1); await db.SaveChangesAsync();
        summon.ScheduledAt = summon.ScheduledAt.Value.AddHours(1); await db.SaveChangesAsync();
        db.Add(new GuardianSummonAppointmentHistory { SchoolId = 1, GuardianSummonId = summon.Id, GuardianProfileId = 1, Action = "NoShow", ActorUserId = "worker" });
        await db.SaveChangesAsync();
        (await repo.ListAsync(new(1, "guardian"), new(), null, default)).Should().Contain(x => x.SourceKey.StartsWith("summon-no-show:"));
        foreach (var status in Enum.GetValues<GuardianSummonStatus>()) { summon.Status = status; await db.SaveChangesAsync(); }
        summon.RequiresOfficerReview = true; summon.OfficerReviewFlaggedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync();
        var review = (await repo.ListAsync(new(1, "officer"), new(), null, default)).First(x => x.SourceType == "Summon");
        (await repo.DetailAsync(review, default)).Action!.Query!["summonId"].Should().Be(summon.Id.ToString());
        var before = await db.Set<PersonalNotification>().CountAsync();
        for (var read = 0; read < 10; read++) { await repo.CountAsync(new(1, "worker"), default); await repo.ListAsync(new(1, "worker"), new(), null, default); }
        (await db.Set<PersonalNotification>().CountAsync()).Should().Be(before);
        referral.AssignedSocialWorkerUserId = "worker2"; await db.SaveChangesAsync();
        (await repo.ListAsync(new(1, "worker"), new(), null, default)).Should().BeEmpty();
        (await repo.FindAsync(new(1, "worker"), item.Id, default)).Should().BeNull();
    }

    [Fact]
    public async Task GuardianDispatchReviewIsSeparateFromApprovalVisibilityAndManagerApproval()
    {
        await using var db = await Graph(); var repo = Repo(db);
        var notification = new Notification { SchoolId = 1, UserId = "guardian", StudentId = 1, RelatedEntityType = "BehaviorIncident",
            RelatedEntityId = "1", Title = "private behavior", RequiresApproval = true };
        var behavior = new BehaviorIncident { Id = 1, SchoolId = 1, StudentId = 1, AcademicTermId = 1 };
        db.Add(behavior);
        db.Add(notification); await db.SaveChangesAsync();
        (await repo.ListAsync(new(1, "guardian"), new(), null, default)).Should().BeEmpty();
        (await repo.ListAsync(new(1, "manager"), new(), null, default)).Should().BeEmpty();
        var review = (await repo.ListAsync(new(1, "officer"), new(), null, default)).Single();
        review.Preview.Should().NotContain("private");
        behavior.GuardianDispatchDecision = GuardianDispatchDecision.Approved;
        notification.RequiresApproval = false; notification.DeliveryStatus = NotificationDeliveryStatus.Delivered; await db.SaveChangesAsync();
        (await repo.CountAsync(new(1, "guardian"), default)).Should().Be(1);
        (await repo.TaskStateAsync(review, default)).Should().Be("resolved");
        notification.IsSuppressed = true; await db.SaveChangesAsync();
        (await repo.CountAsync(new(1, "guardian"), default)).Should().Be(0);
        var visit = new Visit { SchoolId = 1, Status = VisitStatus.PendingApproval, SubmittedAt = DateTimeOffset.UtcNow };
        db.Add(visit); await db.SaveChangesAsync();
        (await repo.CountAsync(new(1, "manager"), default)).Should().Be(1);
        visit.Status = VisitStatus.Approved; await db.SaveChangesAsync();
        var approval = (await repo.ListAsync(new(1, "manager"), new(), null, default)).Single();
        (await repo.TaskStateAsync(approval, default)).Should().Be("resolved");
    }
}
