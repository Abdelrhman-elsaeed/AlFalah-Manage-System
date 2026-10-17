using AlFalah.Application.Notifications;
using AlFalah.Application.Duty;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Duty;
using AlFalah.Domain.Entities.Notifications;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Data;

public partial class AlFalahDbContext
{
    public bool CenterProjectionEnabled { get; set; }
    private WebPushOptions pushOptions = new();
    private List<SourceChange> CaptureCenterChanges() => !CenterProjectionEnabled ? [] : ChangeTracker.Entries()
        .Where(e => e.State is EntityState.Added or EntityState.Modified)
        .Where(e => e.Entity is Notification or MessageReceipt or AbsenceExcuse or ClassroomEntryPermit or GatePass
            or StudentReferral or StudentCaseAction or GuardianSummon or GuardianSummonAppointmentHistory or GatePassTransition or Visit or Complaint or DutyAssignment or DutyAssignmentChange or DutyReportDraft or AttendanceCompletion)
        .Select(e => new SourceChange(e.Entity, e.State == EntityState.Added,
            e.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name).ToHashSet())).ToList();

    private async Task<List<PersonalNotification>> ProjectCenterAsync(List<SourceChange> changes, CancellationToken ct)
    {
        var facts = changes.SelectMany(CenterProjectionPolicy.Project).ToList();
        foreach (var change in changes.Where(x => x.Source is MessageReceipt))
        {
            var r = (MessageReceipt)change.Source;
            if (r.DeliveryState != MessageDeliveryState.Delivered || !change.Added && !change.Changed.Contains(nameof(r.DeliveryState))) continue;
            var m = await ConversationMessages.AsNoTracking().Include(x => x.ConversationThread).SingleAsync(x => x.Id == r.ConversationMessageId && x.SchoolId == r.SchoolId, ct);
            if (m.IsDeleted || m.SenderUserId == r.RecipientUserId) continue;
            facts.Add(new(r.SchoolId, m.SystemCorrelationId is { } correlation ? $"duty-fact:{correlation:N}" : $"message:{m.Id}",
                m.SystemCorrelationId is null ? "messages" : "duty", "Message", m.Id, m.IsSystemOrigin ? "إشعار من المدرسة" : "رسالة جديدة",
                m.Body, [new("Own", PermissionNames.MessagingViewOwn, r.RecipientUserId)], StudentId: m.ConversationThread.StudentId, MessageId: m.Id));
        }
        var created = new List<PersonalNotification>();
        foreach (var read in changes.Where(x => x.Source is Notification { IsRead: true } && x.Changed.Contains(nameof(Notification.IsRead))))
        {
            var notification = (Notification)read.Source;
            var entries = await Set<PersonalNotification>().Where(x => x.SchoolId == notification.SchoolId && x.UserId == notification.UserId && x.NotificationId == notification.Id).ToArrayAsync(ct);
            foreach (var row in entries) row.ReadAtUtc ??= notification.ReadAt ?? inboxTime.GetUtcNow();
        }
        foreach (var fact in facts.OrderBy(x => x.SchoolId).ThenBy(x => x.Key, StringComparer.Ordinal))
        foreach (var audience in fact.Audiences)
        {
            var users = await AudienceAsync(fact, audience, ct);
            foreach (var user in users)
            {
                if (Database.IsSqlServer())
                {
                    var resource = "AlFalah:Center:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                        System.Text.Encoding.UTF8.GetBytes($"{fact.SchoolId}:{user}:{fact.Key}")));
                    await Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; IF @r<0 THROW 51000,'Center lock timeout',1;", ct);
                }
                var row = Set<PersonalNotification>().Local.FirstOrDefault(x => x.SchoolId == fact.SchoolId && x.UserId == user && x.SourceKey == fact.Key)
                    ?? await Set<PersonalNotification>().SingleOrDefaultAsync(x => x.SchoolId == fact.SchoolId && x.UserId == user && x.SourceKey == fact.Key, ct);
                if (row is null)
                {
                    row = new() { SchoolId = fact.SchoolId, UserId = user, SourceKey = fact.Key, CreatedAtUtc = inboxTime.GetUtcNow() };
                    Add(row); created.Add(row);
                }
                // A secondary dashboard/message surface enriches the same entry without resetting read state.
                if (row.MessageId is null || fact.MessageId is not null && fact.NotificationId is null)
                {
                    row.SourceType = fact.SourceType; row.SourceId = fact.SourceId; row.Title = fact.Title.Length > 250 ? fact.Title[..250] : fact.Title;
                    row.Preview = fact.Preview.Length > 800 ? fact.Preview[..800] : fact.Preview;
                    row.Permission = audience.Permission; row.Audience = audience.Kind;
                }
                row.Category = fact.Category; row.StudentId ??= fact.StudentId;
                row.NotificationId ??= fact.NotificationId; row.MessageId ??= fact.MessageId;
                row.LocalDate = fact.Date; row.ExpiresAtUtc = fact.Expires; row.UpdatedAtUtc = inboxTime.GetUtcNow();
                if (row.SourceType == "DutyAssignment" && row.LocalDate is { } date)
                {
                    var zone = await Set<DutyAssignment>().Where(x => x.Id == row.SourceId && x.SchoolId == row.SchoolId)
                        .Join(Set<DutyPlan>(), a => a.DutyPlanId, p => p.Id, (a, p) => p.TimeZoneId).SingleAsync(ct);
                    row.ExpiresAtUtc = TimeZoneInfo.ConvertTimeToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeZoneInfo.FindSystemTimeZoneById(zone));
                }
            }
        }
        // State-only decisions also invalidate existing entries; no new push is scheduled.
        foreach (var change in changes)
        {
            var source = change.Source;
            var type = source switch { Visit => "Visit", Complaint => "Complaint", DutyReportDraft => "DutyReport", GatePass => "GatePass",
                ClassroomEntryPermit => "EntryPermit", AbsenceExcuse => "Excuse", StudentReferral => "Referral", GuardianSummon => "Summon", Notification => "DispatchReview", _ => "" };
            if (type.Length == 0) continue;
            var sourceId = (int)Entry(source).Property("Id").CurrentValue!;
            var existing = await Set<PersonalNotification>().Where(x => x.SourceType == type && x.SourceId == sourceId).ToArrayAsync(ct);
            foreach (var row in existing) row.UpdatedAtUtc = inboxTime.GetUtcNow();
        }
        return created;
    }

    private async Task<string[]> AudienceAsync(CenterFact fact, CenterAudience audience, CancellationToken ct)
    {
        var members = UserSchoolRoles.AsNoTracking().Where(x => x.SchoolId == fact.SchoolId && x.IsActive && x.Role.IsActive
            && x.User.IsActive && x.School.IsActive && x.Role.RolePermissions.Any(p => p.Permission.Name == audience.Permission));
        if (audience.UserId is not null) members = members.Where(x => x.UserId == audience.UserId);
        var role = audience.Kind switch {
            "Officer" => RoleNames.StudentAffairsOfficer, "Manager" or "ComplaintManager" => RoleNames.SchoolManager,
            "Guardian" => RoleNames.Guardian, "PermitTeacher" or "GateTeacher" or "DutyTeacher" or "DutyPreviousTeacher" or "ComplaintOwner" => RoleNames.Instructor,
            "AssignedSocialWorker" or "SummonSocialWorker" => RoleNames.SocialWorker, "Security" => RoleNames.SecurityGuard, _ => null };
        if (role is not null) members = members.Where(x => x.Role.Name == role);
        if (audience.Kind.StartsWith("Complaint")) members = members.Where(x => !UserSchoolRoles.Any(m => m.SchoolId == fact.SchoolId && m.UserId == x.UserId
            && m.IsActive && m.Role.IsActive && (m.Role.Name == RoleNames.MainManager || m.Role.Name == RoleNames.Moderator))
            || UserSchoolRoles.Any(m => m.SchoolId == fact.SchoolId && m.UserId == x.UserId && m.IsActive && m.Role.IsActive && m.Role.Name == RoleNames.SuperAdmin));
        var users = await members.Select(x => x.UserId).Distinct().ToArrayAsync(ct);
        var result = new List<string>();
        foreach (var user in users)
        {
            var allowed = audience.Kind switch {
                "PermitTeacher" => await ClassroomEntryPermits.AnyAsync(x => x.Id == fact.SourceId && x.SchoolId == fact.SchoolId && x.TargetInstructorProfile!.UserId == user, ct),
                "GateTeacher" => await GatePasses.AnyAsync(x => x.Id == fact.SourceId && x.SchoolId == fact.SchoolId && x.CurrentInstructorProfile!.UserId == user, ct),
                "AssignedSocialWorker" when fact.SourceType == "CaseAction" => await StudentCaseActions.AnyAsync(x => x.Id == fact.SourceId && x.SchoolId == fact.SchoolId && x.StudentReferral.AssignedSocialWorkerUserId == user, ct),
                "AssignedSocialWorker" => await StudentReferrals.AnyAsync(x => x.Id == fact.SourceId && x.SchoolId == fact.SchoolId && x.AssignedSocialWorkerUserId == user, ct),
                "SummonSocialWorker" => await GuardianSummons.AnyAsync(x => x.Id == fact.SourceId && x.SchoolId == fact.SchoolId && x.StudentReferral!.AssignedSocialWorkerUserId == user, ct),
                "Guardian" => await GuardianSourceAsync(fact, user, ct),
                "DutyManager" => await UserSchoolRoles.AnyAsync(x => x.SchoolId == fact.SchoolId && x.UserId == user && x.IsActive && x.Role.IsActive && x.RoleId != DutyPermissions.AdministratorRole, ct)
                    && await Set<DutySignedDocument>().AnyAsync(x => x.SchoolId == fact.SchoolId && x.DraftId == fact.SourceId && x.Kind == "Report" && x.SignatureBytes.Length > 0, ct),
                _ => true
            };
            if (allowed) result.Add(user);
        }
        return result.ToArray();
    }

    private async Task<bool> GuardianSourceAsync(CenterFact fact, string user, CancellationToken ct)
    {
        int? student = fact.StudentId;
        if (student is null && fact.SourceType == "GatePass") student = await GatePasses.Where(x => x.Id == fact.SourceId && x.SchoolId == fact.SchoolId)
            .Select(x => (int?)x.StudentId).SingleOrDefaultAsync(ct);
        if (fact.SourceType == "Excuse") student = await AbsenceExcuses.Where(x => x.Id == fact.SourceId && x.SchoolId == fact.SchoolId && x.GuardianProfile.ApplicationUserId == user)
            .Select(x => (int?)x.DailyStudentAttendance.StudentId).SingleOrDefaultAsync(ct);
        if (student is null && fact.SourceType == "Summon") student = await GuardianSummons.Where(x => x.Id == fact.SourceId && x.SchoolId == fact.SchoolId)
            .Select(x => (int?)x.StudentId).SingleOrDefaultAsync(ct);
        if (student is null) return false;
        if (fact.SourceType == "GatePass" && !await GatePasses.AnyAsync(x => x.Id == fact.SourceId && x.SchoolId == fact.SchoolId && x.RequestedByGuardianProfile.ApplicationUserId == user, ct)) return false;
        if (fact.SourceType == "Summon" && !await GuardianSummons.AnyAsync(x => x.Id == fact.SourceId && x.SchoolId == fact.SchoolId && x.GuardianProfile.ApplicationUserId == user, ct)) return false;
        var date = await Repositories.DutyGuardianScope.CurrentDate(this, fact.SchoolId, ct);
        return await StudentGuardians.AnyAsync(x => x.SchoolId == fact.SchoolId && x.StudentId == student && !x.IsDeleted && x.ReceivesNotifications
            && x.Student.IsActive && !x.Student.IsDeleted && x.GuardianProfile.IsActive && !x.GuardianProfile.IsDeleted && x.GuardianProfile.ApplicationUserId == user
            && x.ValidFrom <= date && (x.ValidTo == null || x.ValidTo >= date), ct);
    }

    private async Task SnapshotPushTargetsAsync(List<PersonalNotification> created, CancellationToken ct)
    {
        if (!pushOptions.DispatchEnabled) return;
        var now = inboxTime.GetUtcNow();
        foreach (var row in created)
        {
            var expiry = row.ExpiresAtUtc is { } end && end < now.AddHours(24) ? end : now.AddHours(24);
            if (expiry <= now) { row.PushDisposition = "Expired"; continue; }
            var devices = await Set<BrowserPushSubscription>().AsNoTracking().Where(x => x.SchoolId == row.SchoolId && x.UserId == row.UserId
                && x.RevokedAtUtc == null && x.VapidKeyId == pushOptions.VapidKeyId).ToArrayAsync(ct);
            row.PushDisposition = devices.Length == 0 ? "NoActiveSubscription" : "Scheduled";
            foreach (var device in devices) Add(new BrowserPushTarget { EntryId = row.Id, SubscriptionId = device.Id,
                BindingVersion = device.BindingVersion, ExpiresAtUtc = expiry, NextAttemptAtUtc = now });
        }
    }
}
