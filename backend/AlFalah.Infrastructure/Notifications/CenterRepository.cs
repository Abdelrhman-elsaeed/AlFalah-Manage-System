using AlFalah.Application.Duty;
using AlFalah.Application.Notifications;
using AlFalah.Domain.Entities.Duty;
using AlFalah.Domain.Entities.Notifications;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AlFalah.Infrastructure.Notifications;

public sealed partial class CenterRepository(AlFalahDbContext db, IOptions<InboxOptions> options, TimeProvider time) : ICenterRepository
{
    public async Task<bool> HasAccessAsync(InboxScope scope, CancellationToken ct) => options.Value.CenterEnabled
        && await db.UserSchoolRoles.AnyAsync(x => x.SchoolId == scope.SchoolId && x.UserId == scope.UserId && x.IsActive
            && x.Role.IsActive && x.User.IsActive && x.School.IsActive, ct);

    internal async Task<IQueryable<PersonalNotification>> VisibleAsync(InboxScope scope, CancellationToken ct)
    {
        var members = db.UserSchoolRoles.Where(x => x.SchoolId == scope.SchoolId && x.UserId == scope.UserId
            && x.IsActive && x.Role.IsActive && x.User.IsActive && x.School.IsActive);
        var roles = await members.Select(x => x.Role.Name!).Distinct().ToArrayAsync(ct);
        var permissions = db.RolePermissions.Where(p => members.Select(m => m.RoleId).Contains(p.RoleId)).Select(p => p.Permission.Name);
        var guardian = roles.Contains(RoleNames.Guardian);
        var officer = roles.Contains(RoleNames.StudentAffairsOfficer);
        var worker = roles.Contains(RoleNames.SocialWorker);
        var teacher = roles.Contains(RoleNames.Instructor);
        var manager = roles.Contains(RoleNames.SchoolManager);
        var complaintDenied = !roles.Contains(RoleNames.SuperAdmin) && (roles.Contains(RoleNames.MainManager) || roles.Contains(RoleNames.Moderator));
        var ordinaryMember = await members.AnyAsync(x => x.RoleId != DutyPermissions.AdministratorRole, ct);
        var now = time.GetUtcNow();
        var date = await AlFalah.Infrastructure.Repositories.DutyGuardianScope.CurrentDate(db, scope.SchoolId, ct);
        var links = db.StudentGuardians.AsNoTracking().Where(g => g.SchoolId == scope.SchoolId && !g.IsDeleted
            && g.GuardianProfile.ApplicationUserId == scope.UserId && g.GuardianProfile.IsActive && !g.GuardianProfile.IsDeleted
            && g.GuardianProfile.ApplicationUser.IsActive && g.Student.IsActive && !g.Student.IsDeleted
            && g.ReceivesNotifications && g.ValidFrom <= date && (g.ValidTo == null || g.ValidTo >= date));
        var threads = await new InboxRepository(db, options, time).ThreadsAsync(scope, ct);
        var source = db.Set<PersonalNotification>().AsNoTracking().Where(e => e.SchoolId == scope.SchoolId && e.UserId == scope.UserId
            && members.Any() && permissions.Contains(e.Permission)
            && (!e.SourceKey.StartsWith("duty-fact:") || !guardian || links.Any(g => g.StudentId == e.StudentId)));
        // The source gate applies to counts/history as well as navigation and outbound push.
        source = source.Where(e =>
            (e.SourceType == "Message" && db.ConversationMessages.Any(m => m.Id == e.SourceId && m.SchoolId == scope.SchoolId && !m.IsDeleted
                && m.SenderUserId != scope.UserId && threads.Any(t => t.Id == m.ConversationThreadId)
                && (m.SourceFactId == null || db.Set<StudentDismissalLateness>().Any(f => f.Id == m.SourceFactId && f.SchoolId == scope.SchoolId && !f.IsRetracted && f.Revision == m.SourceFactRevision))
                && m.Receipts.Any(r => r.RecipientUserId == scope.UserId && r.DeliveryState == MessageDeliveryState.Delivered)))
            || (e.SourceType == "Notification" && db.Notifications.Any(n => n.Id == e.SourceId && n.SchoolId == scope.SchoolId && n.UserId == scope.UserId
                && !n.IsDeleted && !n.IsSuppressed && !n.RequiresApproval && n.DeliveryStatus == NotificationDeliveryStatus.Delivered
                && (n.Type != "PenaltyAbsenceThreshold" || officer && db.StudentTermMetrics.Any(m => m.SchoolId == scope.SchoolId
                    && m.StudentId == n.StudentId && m.StudentId.ToString() + ":" + m.AcademicTermId.ToString() == n.RelatedEntityId
                    && !m.IsDeleted && !m.Student.IsDeleted && m.Student.IsActive))
                && (n.RelatedEntityType != "DailyStudentAttendance" || db.DailyStudentAttendances.Any(x => x.SchoolId == scope.SchoolId && x.Id.ToString() == n.RelatedEntityId && !x.IsDeleted && x.Status != StudentAttendanceStatus.Present))
                && (n.RelatedEntityType != "MorningArrivalDelay" || db.MorningArrivalDelays.Any(x => x.SchoolId == scope.SchoolId && x.Id.ToString() == n.RelatedEntityId && !x.IsDeleted))
                && (n.RelatedEntityType != "SessionDelay" || db.SessionDelays.Any(x => x.SchoolId == scope.SchoolId && x.Id.ToString() == n.RelatedEntityId && !x.IsDeleted))
                && (n.RelatedEntityType != "BehaviorIncident" || db.BehaviorIncidents.Any(x => x.SchoolId == scope.SchoolId && x.Id.ToString() == n.RelatedEntityId && !x.IsDeleted && x.GuardianDispatchDecision == GuardianDispatchDecision.Approved))
                && (n.RelatedEntityType != "AcademicConcern" || db.AcademicConcerns.Any(x => x.SchoolId == scope.SchoolId && x.Id.ToString() == n.RelatedEntityId && !x.IsDeleted && x.GuardianDispatchDecision == GuardianDispatchDecision.Approved))
                && (n.Type != "DismissalLateness" || db.Set<DismissalNoticeRecipient>().Any(r => r.SchoolId == scope.SchoolId && r.CorrelationId == n.CorrelationId
                    && db.Set<DismissalNoticeBatch>().Any(b => b.Id == r.BatchId && b.SchoolId == scope.SchoolId
                        && db.Set<StudentDismissalLateness>().Any(f => f.Id == b.FactId && f.SchoolId == scope.SchoolId && !f.IsRetracted && f.Revision == b.Revision))))
                && (!guardian || links.Any(g => g.StudentId == n.StudentId))))
            || (e.SourceType == "DispatchReview" && officer && db.Notifications.Any(n => n.Id == e.SourceId && n.SchoolId == scope.SchoolId
                && !n.IsDeleted && (n.RelatedEntityType == "BehaviorIncident" && db.BehaviorIncidents.Any(x => x.SchoolId == scope.SchoolId && !x.IsDeleted && x.Id.ToString() == n.RelatedEntityId)
                    || n.RelatedEntityType == "AcademicConcern" && db.AcademicConcerns.Any(x => x.SchoolId == scope.SchoolId && !x.IsDeleted && x.Id.ToString() == n.RelatedEntityId))))
            || (e.SourceType == "Excuse" && db.AbsenceExcuses.Any(x => x.Id == e.SourceId && x.SchoolId == scope.SchoolId && !x.IsDeleted
                && ((e.Audience == "Officer" && officer) || (e.Audience == "Guardian" && guardian
                    && x.GuardianProfile.ApplicationUserId == scope.UserId && links.Any(g => g.StudentId == x.DailyStudentAttendance.StudentId)))))
            || (e.SourceType == "EntryPermit" && db.ClassroomEntryPermits.Any(x => x.Id == e.SourceId && x.SchoolId == scope.SchoolId && !x.IsDeleted
                && ((e.Audience == "PermitTeacher" && teacher && x.TargetInstructorProfile!.UserId == scope.UserId)
                    || (e.Audience == "Guardian" && guardian && links.Any(g => g.StudentId == x.StudentId)) || (e.Audience == "Officer" && officer))))
            || (e.SourceType == "GatePass" && db.GatePasses.Any(x => x.Id == e.SourceId && x.SchoolId == scope.SchoolId && !x.IsDeleted
                && ((e.Audience == "GateTeacher" && teacher && x.CurrentInstructorProfile!.UserId == scope.UserId)
                    || (e.Audience == "Security" && roles.Contains(RoleNames.SecurityGuard)) || (e.Audience == "Officer" && officer)
                    || (e.Audience == "Guardian" && guardian && x.RequestedByGuardianProfile.ApplicationUserId == scope.UserId && links.Any(g => g.StudentId == x.StudentId)))))
            || (e.SourceType == "Referral" && db.StudentReferrals.Any(x => x.Id == e.SourceId && x.SchoolId == scope.SchoolId && !x.IsDeleted
                && ((e.Audience == "Officer" && officer && !worker) || (worker && x.AssignedSocialWorkerUserId == scope.UserId))))
            || (e.SourceType == "CaseAction" && worker && db.StudentCaseActions.Any(x => x.Id == e.SourceId && x.SchoolId == scope.SchoolId && !x.IsDeleted
                && !x.StudentReferral.IsDeleted && x.StudentReferral.AssignedSocialWorkerUserId == scope.UserId))
            || (e.SourceType == "Summon" && db.GuardianSummons.Any(x => x.Id == e.SourceId && x.SchoolId == scope.SchoolId && !x.IsDeleted
                && ((e.Audience == "Officer" && officer && !worker)
                    || (e.Audience == "SummonSocialWorker" && worker && x.StudentReferral!.AssignedSocialWorkerUserId == scope.UserId && !x.StudentReferral.IsDeleted)
                    || (e.Audience == "Guardian" && guardian && x.GuardianProfile.ApplicationUserId == scope.UserId && links.Any(g => g.StudentId == x.StudentId)))))
            || (e.SourceType == "Visit" && manager && db.Visits.Any(x => x.Id == e.SourceId && x.SchoolId == scope.SchoolId && !x.IsDeleted))
            || (e.SourceType == "Complaint" && !complaintDenied && db.Complaints.Any(x => x.Id == e.SourceId && x.SchoolId == scope.SchoolId
                && !x.IsDeleted && ((e.Audience == "ComplaintManager" && manager) || (e.Audience == "ComplaintOwner" && teacher && x.InstructorUserId == scope.UserId))))
            || (e.SourceType == "DutyAssignment" && teacher && db.Set<DutyAssignment>().Any(x => x.Id == e.SourceId && x.SchoolId == scope.SchoolId && (x.EffectiveTeacherId == scope.UserId
                || e.Audience == "DutyPreviousTeacher" && db.Set<DutyAssignmentChange>().Any(c => c.AssignmentId == x.Id && c.SchoolId == scope.SchoolId && c.BeforeTeacherId == scope.UserId))))
            || (e.SourceType == "DutyReport" && ordinaryMember && db.Set<DutyReportDraft>().Any(x => x.Id == e.SourceId && x.SchoolId == scope.SchoolId
                && db.Set<DutyAssignment>().Any(a => a.Id == x.AssignmentId && a.SchoolId == scope.SchoolId && !a.IsCancelled && a.EffectiveTeacherId == x.AuthorUserId)))
            || ((e.SourceType == "StudentAttendance" || e.SourceType == "StaffAttendance") && manager
                && db.Set<AttendanceCompletion>().Any(x => x.Id == e.SourceId && x.SchoolId == scope.SchoolId && x.RosterCount > 0)));
        return source;
    }

    public async Task<IReadOnlyList<PersonalNotification>> ListAsync(InboxScope scope, CenterQuery query, CenterPosition? before, CancellationToken ct)
    {
        var source = await VisibleAsync(scope, ct);
        if (query.Unread) source = source.Where(e => e.ReadAtUtc == null);
        if (!string.IsNullOrEmpty(query.Category)) source = source.Where(e => e.Category == query.Category);
        if (before is not null) source = source.Where(e => e.CreatedAtUtc < before.At || e.CreatedAtUtc == before.At && e.Id.CompareTo(before.Id) < 0);
        return await source.OrderByDescending(e => e.CreatedAtUtc).ThenByDescending(e => e.Id).Take(query.Limit).ToArrayAsync(ct);
    }
    public async Task<int> CountAsync(InboxScope scope, CancellationToken ct) => await (await VisibleAsync(scope, ct)).CountAsync(x => x.ReadAtUtc == null, ct);
    public async Task<PersonalNotification?> FindAsync(InboxScope scope, Guid id, CancellationToken ct) =>
        await (await VisibleAsync(scope, ct)).SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<bool> ReadAsync(InboxScope scope, Guid? id, CancellationToken ct)
    {
        // Each batch journals atomically. Read-all is bounded per transaction, but drains all eligible rows.
        do
        {
            var source = (await VisibleAsync(scope, ct)).AsTracking().Where(x => x.ReadAtUtc == null);
            if (id is not null) source = source.Where(x => x.Id == id);
            var rows = await source.OrderBy(x => x.Id).Take(250).ToArrayAsync(ct);
            if (rows.Length == 0) return id is null || await FindAsync(scope, id.Value, ct) is not null;
            foreach (var row in rows) row.ReadAtUtc = time.GetUtcNow();
            var sourceIds = rows.Where(x => x.NotificationId != null).Select(x => x.NotificationId!.Value).ToArray();
            var sourceNotifications = await db.Notifications.Where(x => x.SchoolId == scope.SchoolId && x.UserId == scope.UserId && sourceIds.Contains(x.Id)).ToArrayAsync(ct);
            foreach (var sourceNotification in sourceNotifications) { sourceNotification.IsRead = true; sourceNotification.ReadAt ??= time.GetUtcNow(); }
            await db.SaveChangesAsync(ct);
            if (id is not null || rows.Length < 250) return true;
        } while (!ct.IsCancellationRequested);
        return true;
    }
}
