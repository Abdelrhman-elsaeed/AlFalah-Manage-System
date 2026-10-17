using AlFalah.Application.Notifications;
using AlFalah.Domain.Entities.Duty;
using AlFalah.Domain.Entities.Notifications;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Notifications;

public sealed partial class CenterRepository
{
    public async Task<string> TaskStateAsync(PersonalNotification e, CancellationToken ct) =>
        (await TaskStatesAsync([e], ct)).GetValueOrDefault(e.Id, "info");

    public async Task<IReadOnlyDictionary<Guid, string>> TaskStatesAsync(IReadOnlyList<PersonalNotification> entries, CancellationToken ct)
    {
        if (entries.Count == 0) return new Dictionary<Guid, string>();
        var ids = entries.Select(x => x.Id).ToArray();
        var now = time.GetUtcNow();
        // One SQL projection for a whole page; no per-entry round trips or tracked source graphs.
        return await db.Set<PersonalNotification>().AsNoTracking().Where(e => ids.Contains(e.Id)).Select(e => new { e.Id,
            State = e.SourceType == "DispatchReview" ? (db.Notifications.Any(x => x.Id == e.SourceId && x.SchoolId == e.SchoolId && x.RequiresApproval && !x.IsSuppressed) ? "pending" : "resolved")
            : e.SourceType == "Visit" ? (db.Visits.Any(x => x.Id == e.SourceId && x.SchoolId == e.SchoolId && x.Status == VisitStatus.PendingApproval) ? "pending" : "resolved")
            : e.SourceType == "Complaint" ? (db.Complaints.Any(x => x.Id == e.SourceId && x.SchoolId == e.SchoolId && (x.Status == ComplaintStatus.Open || x.Status == ComplaintStatus.InReview)) ? "pending" : "resolved")
            : e.SourceType == "Excuse" ? (db.AbsenceExcuses.Any(x => x.Id == e.SourceId && x.SchoolId == e.SchoolId && x.Status == AbsenceExcuseStatus.Pending) ? "pending" : "resolved")
            : e.SourceType == "EntryPermit" ? (db.ClassroomEntryPermits.Any(x => x.Id == e.SourceId && x.SchoolId == e.SchoolId && x.Status == ClassroomEntryPermitStatus.Issued && x.ValidUntil > now) ? "pending" : "resolved")
            : e.SourceType == "GatePass" ? (db.GatePasses.Any(x => x.Id == e.SourceId && x.SchoolId == e.SchoolId && (x.Status == GatePassStatus.Requested || x.Status == GatePassStatus.Approved || x.Status == GatePassStatus.SecurityAcknowledged)
                && (x.ApprovedWindowEndsAt == null || x.ApprovedWindowEndsAt > now)) ? "pending" : "resolved")
            : e.SourceType == "Referral" ? (db.StudentReferrals.Any(x => x.Id == e.SourceId && x.SchoolId == e.SchoolId && x.Status != StudentReferralStatus.Resolved && x.Status != StudentReferralStatus.Closed) ? "pending" : "resolved")
            : e.SourceType == "Summon" ? (db.GuardianSummons.Any(x => x.Id == e.SourceId && x.SchoolId == e.SchoolId
                && (x.RequiresOfficerReview || x.Status == GuardianSummonStatus.Pending || x.Status == GuardianSummonStatus.UnderObservation)) ? "pending" : "resolved")
            : e.SourceType == "DutyReport" ? (db.Set<DutyReportDraft>().Any(x => x.Id == e.SourceId && x.SchoolId == e.SchoolId && x.SubmittedAt == null) ? "correction" : "resolved")
            : e.SourceType == "DutyAssignment" ? (db.Set<DutyAssignment>().Any(x => x.Id == e.SourceId && x.SchoolId == e.SchoolId && x.IsCancelled) ? "cancelled" : "info") : "info"
        }).ToDictionaryAsync(x => x.Id, x => x.State, ct);
    }

    public async Task<CenterTarget> TargetAsync(PersonalNotification e, CancellationToken ct)
    {
        var query = new Dictionary<string, string>();
        string? route = null;
        var id = e.SourceId.ToString();
        switch (e.SourceType)
        {
            case "Message":
                route = "/student-affairs/messages";
                query["conversationId"] = (await db.ConversationMessages.Where(x => x.Id == e.SourceId && x.SchoolId == e.SchoolId).Select(x => x.ConversationThreadId).SingleAsync(ct)).ToString();
                query["messageId"] = id; break;
            case "Complaint": route = "/complaints"; query["complaintId"] = id; break;
            case "Visit": route = "/visits"; query["visitId"] = id; break;
            case "DutyAssignment":
                var assignment = await db.Set<DutyAssignment>().SingleAsync(x => x.Id == e.SourceId && x.SchoolId == e.SchoolId, ct);
                if (assignment.EffectiveTeacherId != e.UserId) { route = "/notifications/detail/" + e.Id; break; }
                route = "/school-duty/execution"; query["execution"] = id; query["date"] = assignment.Date.ToString("yyyy-MM-dd"); break;
            case "DutyReport":
                var draft = await db.Set<DutyReportDraft>().SingleAsync(x => x.Id == e.SourceId && x.SchoolId == e.SchoolId, ct);
                var report = await db.Set<DutySignedDocument>().Where(x => x.SchoolId == e.SchoolId && x.DraftId == draft.Id && x.Kind == "Report")
                    .OrderByDescending(x => x.SignedAt).Select(x => new { x.Id, x.AssignmentId }).FirstOrDefaultAsync(ct);
                if (report is null) return new(false);
                route = "/school-duty/documents"; query["execution"] = report.AssignmentId.ToString(); query["documentId"] = report.Id.ToString(); query["review"] = "true"; break;
            case "StudentAttendance": case "StaffAttendance":
                var completion = await db.Set<AttendanceCompletion>().SingleAsync(x => x.Id == e.SourceId && x.SchoolId == e.SchoolId, ct);
                // The manager's notification detail is a read-only authorized sheet; secretary workflows stay unchanged.
                route = "/notifications/sheet/" + e.Id; query["date"] = completion.Date.ToString("yyyy-MM-dd");
                if (completion.ClassroomId is { } classroom) query["classroomId"] = classroom.ToString(); break;
            case "Notification":
                route = "/notifications/detail/" + e.Id; break;
            default:
                // A selected source detail loads the existing authorized query, then offers the existing workflow.
                route = "/notifications/detail/" + e.Id; break;
        }
        return new(true, route, query, e.Title, await TaskStateAsync(e, ct));
    }
}
