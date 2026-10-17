using AlFalah.Application.Notifications;
using AlFalah.Domain.Entities.Notifications;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Notifications;

public sealed partial class CenterRepository
{
    public async Task<CenterDetail> DetailAsync(PersonalNotification e, CancellationToken ct)
    {
        var rows = new List<CenterDetailRow>();
        CenterTarget? action = null;
        CenterTarget Link(string route, string key, int value) => new(true, route, new Dictionary<string, string> { [key] = value.ToString() });
        switch (e.SourceType)
        {
            case "Notification":
                var notice = await db.Notifications.AsNoTracking().SingleAsync(x => x.Id == e.SourceId && x.SchoolId == e.SchoolId && x.UserId == e.UserId, ct);
                rows.Add(new("مرجع المصدر", $"{notice.RelatedEntityType} · {notice.RelatedEntityId}"));
                if (int.TryParse(notice.RelatedEntityId, out var sourceId))
                {
                    switch (notice.RelatedEntityType)
                    {
                        case "DailyStudentAttendance":
                            var attendance = await db.DailyStudentAttendances.AsNoTracking().Where(x => x.Id == sourceId && x.SchoolId == e.SchoolId)
                                .Select(x => new { x.AttendanceDate, x.Status, x.ClassroomId }).SingleAsync(ct);
                            rows.Add(new("التاريخ", attendance.AttendanceDate.ToString("yyyy-MM-dd")));
                            rows.Add(new("الفصل", attendance.ClassroomId.ToString()));
                            rows.Add(new("حالة الحضور", attendance.Status.ToString() switch { "Absent" => "غائب", "AbsentExcused" => "غائب بعذر", _ => "تم الرصد" }));
                            break;
                        case "MorningArrivalDelay":
                            var morning = await db.MorningArrivalDelays.AsNoTracking().Where(x => x.Id == sourceId && x.SchoolId == e.SchoolId)
                                .Select(x => new { x.SchoolLocalDate, x.DelayMinutes, x.Reason }).SingleAsync(ct);
                            rows.Add(new("التاريخ", morning.SchoolLocalDate.ToString("yyyy-MM-dd")));
                            rows.Add(new("التأخر بالدقائق", morning.DelayMinutes.ToString()));
                            if (morning.Reason is not null) rows.Add(new("السبب", morning.Reason));
                            break;
                        case "SessionDelay":
                            var session = await db.SessionDelays.AsNoTracking().Where(x => x.Id == sourceId && x.SchoolId == e.SchoolId)
                                .Select(x => new { x.OccurredAt, x.Period, x.DelayMinutes }).SingleAsync(ct);
                            rows.Add(new("الوقت", session.OccurredAt.ToString("yyyy-MM-dd HH:mm")));
                            rows.Add(new("الحصة", session.Period.ToString()));
                            if (session.DelayMinutes is { } minutes) rows.Add(new("التأخر بالدقائق", minutes.ToString()));
                            break;
                        case "BehaviorIncident":
                            var behavior = await db.BehaviorIncidents.AsNoTracking().Where(x => x.Id == sourceId && x.SchoolId == e.SchoolId)
                                .Select(x => new { x.OccurredAt, x.Description, x.CategoryCode }).SingleAsync(ct);
                            rows.Add(new("الوقت", behavior.OccurredAt.ToString("yyyy-MM-dd HH:mm")));
                            rows.Add(new("التصنيف", behavior.CategoryCode)); rows.Add(new("الوصف", behavior.Description)); break;
                        case "AcademicConcern":
                            var academic = await db.AcademicConcerns.AsNoTracking().Where(x => x.Id == sourceId && x.SchoolId == e.SchoolId)
                                .Select(x => new { x.OccurredAt, x.Description, x.Category }).SingleAsync(ct);
                            rows.Add(new("الوقت", academic.OccurredAt.ToString("yyyy-MM-dd HH:mm")));
                            rows.Add(new("التصنيف", academic.Category)); rows.Add(new("الوصف", academic.Description)); break;
                    }
                }
                if (notice.Type == "PenaltyAbsenceThreshold" && notice.StudentId is { } student)
                {
                    rows.Add(new("الفصل الدراسي", notice.RelatedEntityId?.Split(':').LastOrDefault() ?? ""));
                    // Existing officer profile query enforces Student.View separately; no implied decision authority.
                    action = Link("/student-affairs/records/" + student, "thresholdNoticeId", notice.Id);
                }
                break;
            case "StudentAttendance": case "StaffAttendance":
                var completion = await db.Set<AttendanceCompletion>().SingleAsync(x => x.Id == e.SourceId && x.SchoolId == e.SchoolId, ct);
                rows.Add(new("التاريخ", completion.Date.ToString("yyyy-MM-dd")));
                if (completion.ClassroomId is { } classroom)
                {
                    var sheet = await db.DailyStudentAttendances.AsNoTracking().Where(x => x.SchoolId == e.SchoolId && x.ClassroomId == classroom && x.AttendanceDate == completion.Date && !x.IsDeleted)
                        .OrderBy(x => x.Student.FirstName).Select(x => new { FullName = x.Student.FirstName + " " + (x.Student.MiddleName ?? "") + " " + x.Student.LastName, x.Status }).ToArrayAsync(ct);
                    rows.Add(new("الفصل", classroom.ToString()));
                    rows.AddRange(sheet.Select(x => new CenterDetailRow(x.FullName, x.Status.ToString() switch { "Present" => "حاضر", "Absent" => "غائب", "AbsentExcused" => "غائب بعذر", _ => "تم الرصد" })));
                }
                else
                {
                    var sheet = await db.AttendanceRecords.AsNoTracking().Where(x => x.SchoolId == e.SchoolId && x.AttendanceDate == completion.Date)
                        .OrderBy(x => x.User.FirstName).Select(x => new { Name = x.User.FirstName + " " + x.User.LastName, x.Status }).ToArrayAsync(ct);
                    rows.AddRange(sheet.Select(x => new CenterDetailRow(x.Name, x.Status.ToString() switch { "Present" => "حاضر", "Absent" => "غائب", "Late" => "متأخر", "Excused" => "بعذر", _ => "تم الرصد" })));
                }
                break;
            case "Excuse":
                var excuse = await db.AbsenceExcuses.Where(x => x.Id == e.SourceId && x.SchoolId == e.SchoolId).Select(x => new { x.GuardianNotes, x.ReviewReason, x.Status }).SingleAsync(ct);
                rows.Add(new("العذر", excuse.GuardianNotes ?? "لم تُضف ملاحظات"));
                if (excuse.ReviewReason is not null) rows.Add(new("نتيجة المراجعة", excuse.ReviewReason));
                action = Link(e.Audience == "Officer" ? "/student-affairs/officer/excuses" : "/student-affairs/guardian/excuses", "excuseId", e.SourceId); break;
            case "EntryPermit":
                var permit = await db.ClassroomEntryPermits.Where(x => x.Id == e.SourceId && x.SchoolId == e.SchoolId).Select(x => new { x.Reason, x.ValidUntil, x.ClassroomId }).SingleAsync(ct);
                rows.Add(new("السبب", permit.Reason)); rows.Add(new("الصلاحية حتى", permit.ValidUntil.ToString("yyyy-MM-dd HH:mm")));
                action = Link(e.Audience == "PermitTeacher" ? "/student-affairs/teacher" : e.Audience == "Guardian" ? "/student-affairs/guardian/activity" : "/student-affairs/officer/entry-permits", "permitId", e.SourceId); break;
            case "GatePass":
                var gate = await db.GatePasses.Where(x => x.Id == e.SourceId && x.SchoolId == e.SchoolId).Select(x => new { x.Reason, x.RequestedExitAt, x.RejectionReason }).SingleAsync(ct);
                rows.Add(new("سبب الاستئذان", gate.Reason)); rows.Add(new("موعد الخروج", gate.RequestedExitAt.ToString("yyyy-MM-dd HH:mm")));
                if (gate.RejectionReason is not null) rows.Add(new("نتيجة المراجعة", gate.RejectionReason));
                action = Link(e.Audience == "GateTeacher" ? "/student-affairs/teacher" : e.Audience == "Security" ? "/student-affairs/security" : e.Audience == "Guardian" ? "/student-affairs/gate-passes/mine/new" : "/student-affairs/gate-passes", "gatePassId", e.SourceId); break;
            case "Referral":
                // Officer view intentionally excludes confidential case actions/resolution notes.
                action = Link(e.Audience == "Officer" ? "/student-affairs/officer/referrals" : "/student-affairs/cases", "referralId", e.SourceId); break;
            case "CaseAction":
                var follow = await db.StudentCaseActions.Where(x => x.Id == e.SourceId && x.SchoolId == e.SchoolId).Select(x => new { x.Description, x.Result, x.StudentReferralId }).SingleAsync(ct);
                rows.Add(new("المتابعة", follow.Description)); if (follow.Result is not null) rows.Add(new("النتيجة", follow.Result));
                action = Link("/student-affairs/cases", "referralId", follow.StudentReferralId); break;
            case "Summon":
                var summon = await db.GuardianSummons.Where(x => x.Id == e.SourceId && x.SchoolId == e.SchoolId).Select(x => new { x.ScheduledAt, x.Location, x.Instructions }).SingleAsync(ct);
                if (summon.ScheduledAt is { } scheduled) rows.Add(new("الموعد", scheduled.ToString("yyyy-MM-dd HH:mm")));
                if (summon.Location is not null) rows.Add(new("المكان", summon.Location));
                if (summon.Instructions is not null) rows.Add(new("التعليمات", summon.Instructions));
                action = Link(e.Audience == "Officer" ? "/student-affairs/officer/automation-reviews" : e.Audience == "Guardian" ? "/student-affairs/guardian/activity" : "/student-affairs/summons", "summonId", e.SourceId); break;
            case "DispatchReview": action = Link("/student-affairs/notification-approvals", "notificationId", e.SourceId); break;
        }
        return new(e.Title, e.Preview, await TaskStateAsync(e, ct), e.SourceType, e.SourceId, rows, action);
    }
}
