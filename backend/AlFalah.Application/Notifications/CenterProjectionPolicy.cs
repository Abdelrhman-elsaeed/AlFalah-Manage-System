using AlFalah.Application.Duty;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Duty;
using AlFalah.Domain.Entities.Notifications;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;

namespace AlFalah.Application.Notifications;

/// <summary>Approved source transitions and audiences. No database or transport decisions live here.</summary>
public static class CenterProjectionPolicy
{
    public static IReadOnlyList<CenterFact> Project(SourceChange change)
    {
        var result = new List<CenterFact>();
        bool Changed(params string[] fields) => change.Added || fields.Any(change.Changed.Contains);
        CenterAudience Officer(string permission) => new("Officer", permission);
        CenterAudience Guardian() => new("Guardian", PermissionNames.NotificationViewOwn);
        void Add(int school, string key, string category, string type, int id, string title, IEnumerable<CenterAudience> audience,
            int? student = null, string preview = "افتح الإشعار للاطلاع على التفاصيل الحالية.", DateOnly? date = null, DateTimeOffset? expiry = null)
            => result.Add(new(school, key, category, type, id, title, preview, audience.ToArray(), student, date, expiry));

        switch (change.Source)
        {
            case Notification n when n.SchoolId is { } school && !n.IsDeleted && !n.IsSuppressed:
                if (n.RequiresApproval && Changed(nameof(n.RequiresApproval), nameof(n.IsSuppressed)))
                    Add(school, $"review:{n.Id}", "approvals", "DispatchReview", n.Id, "إشعار ولي أمر بانتظار الاعتماد",
                        [Officer(PermissionNames.NotificationApproveDispatch)], n.StudentId, "راجع الإشعار واتخذ القرار من مسار الاعتماد.");
                else if (!n.RequiresApproval && n.DeliveryStatus == NotificationDeliveryStatus.Delivered
                    && Changed(nameof(n.RequiresApproval), nameof(n.DeliveryStatus)))
                {
                    // These have a primary workflow projection. Timetable suggestions are not selected N2 scope.
                    if (n.Type == "SchoolDuty" || n.RelatedEntityType is nameof(ClassroomEntryPermit) or nameof(GatePass) or nameof(AbsenceExcuse)
                        or nameof(GuardianSummon) or nameof(StudentReferral) or nameof(Complaint)
                        || n.Type is "TeacherTimetableChanged" or "CoverageAttention") break;
                    var isMessage = n.RelatedEntityType == nameof(ConversationMessage) && int.TryParse(n.RelatedEntityId, out _);
                    var dutyFact = n.Type == "DismissalLateness";
                    var category = isMessage ? "messages" : dutyFact ? "duty" : "students";
                    var key = isMessage ? $"message:{n.RelatedEntityId}" : n.CorrelationId is { } correlation && dutyFact
                        ? $"duty-fact:{correlation:N}" : $"notification:{n.Id}";
                    result.Add(new(school, key, category, isMessage ? "Message" : "Notification", isMessage ? int.Parse(n.RelatedEntityId!) : n.Id,
                        n.Title, n.Message, [new("Own", isMessage ? PermissionNames.MessagingViewOwn : PermissionNames.NotificationViewOwn, n.UserId)], n.StudentId,
                        NotificationId: n.Id, MessageId: isMessage ? int.Parse(n.RelatedEntityId!) : null));
                }
                break;
            case AbsenceExcuse e when !e.IsDeleted && Changed(nameof(e.Status)):
                Add(e.SchoolId, $"excuse:{e.Id}:{e.Status}", "students", "Excuse", e.Id,
                    e.Status == AbsenceExcuseStatus.Pending ? "عذر غياب بانتظار المراجعة" : "تمت مراجعة عذر الغياب",
                    e.Status == AbsenceExcuseStatus.Pending ? [Officer(PermissionNames.AttendanceReviewExcuse)] : [Guardian()]);
                break;
            case ClassroomEntryPermit p when !p.IsDeleted && Changed(nameof(p.Status), nameof(p.TargetInstructorProfileId), nameof(p.ValidUntil)):
                Add(p.SchoolId, $"permit:{p.Id}:{p.Status}", "students", "EntryPermit", p.Id,
                    p.Status == ClassroomEntryPermitStatus.Issued ? "تصريح دخول فصل بانتظار الإقرار" : "تحديث تصريح دخول الفصل",
                    [new("PermitTeacher", PermissionNames.ClassroomEntryPermitAcknowledge), Guardian(), Officer(PermissionNames.ClassroomEntryPermitView)], p.StudentId,
                    expiry: p.ValidUntil);
                break;
            case GatePass g when !g.IsDeleted && Changed(nameof(g.Status), nameof(g.SecurityAcknowledgedAt), nameof(g.CurrentInstructorProfileId)):
                var audiences = new List<CenterAudience>();
                if (g.Status == GatePassStatus.Requested) audiences.Add(Officer(PermissionNames.GatePassApprove));
                else
                {
                    audiences.Add(Guardian());
                    audiences.Add(Officer(PermissionNames.GatePassView));
                    if (g.Status is GatePassStatus.Approved or GatePassStatus.SecurityAcknowledged)
                    {
                        audiences.Add(new("GateTeacher", PermissionNames.GatePassAcknowledgeTeacher));
                        audiences.Add(new("Security", PermissionNames.GatePassExecute));
                    }
                }
                Add(g.SchoolId, $"gate:{g.Id}:{g.Status}", "students", "GatePass", g.Id,
                    g.Status == GatePassStatus.Requested ? "طلب استئذان خروج جديد" : g.Status == GatePassStatus.Exited ? "تم تسجيل خروج الطالب" : "تحديث استئذان الخروج",
                    audiences, g.StudentId, expiry: g.ApprovedWindowEndsAt);
                break;
            case StudentReferral r when !r.IsDeleted && Changed(nameof(r.Status), nameof(r.AssignedSocialWorkerUserId)):
                var assignmentKey = r.AssignedSocialWorkerUserId is null ? "unassigned" : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(r.AssignedSocialWorkerUserId)));
                Add(r.SchoolId, $"referral:{r.Id}:{r.Status}:{assignmentKey}", "cases", "Referral", r.Id,
                    "تحديث إحالة طالب", [Officer(PermissionNames.ReferralView), new("AssignedSocialWorker", PermissionNames.ReferralView)], r.StudentId);
                break;
            case StudentCaseAction a when !a.IsDeleted && change.Added:
                Add(a.SchoolId, $"case-action:{a.Id}", "cases", "CaseAction", a.Id, "متابعة جديدة للحالة",
                    [new("AssignedSocialWorker", PermissionNames.ReferralView)]);
                break;
            case GuardianSummon s when !s.IsDeleted && Changed(nameof(s.Status), nameof(s.ScheduledAt), nameof(s.OfficerReviewFlaggedAt), nameof(s.OfficerReviewedAt)):
                var summonAudience = new List<CenterAudience> { new("SummonSocialWorker", PermissionNames.SummonView) };
                if (s.ScheduledAt is not null) summonAudience.Add(Guardian());
                if (s.RequiresOfficerReview) summonAudience.Add(Officer(PermissionNames.SummonReviewAutomationImpact));
                Add(s.SchoolId, $"summon:{s.Id}:{s.Status}:{s.ScheduledAt?.UtcTicks}:{s.OfficerReviewFlaggedAt?.UtcTicks}:{s.OfficerReviewedAt?.UtcTicks}",
                    s.RequiresOfficerReview ? "approvals" : "students", "Summon", s.Id,
                    s.RequiresOfficerReview ? "استدعاء يحتاج مراجعة أثر الأتمتة" : "تحديث استدعاء ولي الأمر", summonAudience, s.StudentId);
                break;
            case GuardianSummonAppointmentHistory h when change.Added && h.Action == "NoShow":
                Add(h.SchoolId, $"summon-no-show:{h.Id}", "students", "Summon", h.GuardianSummonId,
                    "تحديث متابعة موعد الاستدعاء", [new("SummonSocialWorker", PermissionNames.SummonView), Guardian()],
                    preview: "تم تسجيل تحديث لحضور موعد الاستدعاء. افتح الاستدعاء للاطلاع على حالته الحالية.");
                break;
            case GatePassTransition t when change.Added && t.ActorRole == RoleNames.Instructor && t.Reason == "Acknowledged by teacher":
                Add(t.SchoolId, $"gate-teacher-ack:{t.Id}", "students", "GatePass", t.GatePassId, "تم إقرار المعلم باستئذان الخروج",
                    [Officer(PermissionNames.GatePassView), new("Security", PermissionNames.GatePassExecute), Guardian()]);
                break;
            case Visit v when !v.IsDeleted && v.Status == VisitStatus.PendingApproval && Changed(nameof(v.Status)):
                Add(v.SchoolId, $"visit:{v.Id}:approval:{v.SubmittedAt?.UtcTicks}", "approvals", "Visit", v.Id, "تقييم زيارة بانتظار الاعتماد",
                    [new("Manager", PermissionNames.VisitApprove)], preview: "تم إرسال تقييم الزيارة للمراجعة والاعتماد.");
                break;
            case Complaint c when !c.IsDeleted && change.Added:
                Add(c.SchoolId, $"complaint:{c.Id}:submitted", "complaints", "Complaint", c.Id, "شكوى جديدة بانتظار المراجعة",
                    [new("ComplaintManager", PermissionNames.ComplaintView)], preview: "تم تقديم شكوى على تقرير زيارة. افتحها للمراجعة.");
                break;
            case Complaint c when !c.IsDeleted && Changed(nameof(c.Status), nameof(c.ResolutionNote), nameof(c.VisitReopenedAt)):
                Add(c.SchoolId, $"complaint:{c.Id}:{c.Status}:{c.HandledAt?.UtcTicks}", "complaints", "Complaint", c.Id,
                    "رد جديد على شكواك", [new("ComplaintOwner", PermissionNames.ComplaintView, c.InstructorUserId)],
                    preview: c.ResolutionNote ?? "تم تحديث حالة الشكوى. افتحها للاطلاع على القرار.");
                break;
            case DutyReportDraft d when d.SubmittedAt is not null && Changed(nameof(d.SubmittedAt)):
                Add(d.SchoolId, $"duty-report:{d.Id}", "duty", "DutyReport", d.Id, "تم توقيع تقرير المناوبة وإرساله",
                    [new("DutyManager", DutyPermissions.Manage)], preview: "التقرير الموقّع متاح للمراجعة ضمن مستندات المناوبة.");
                break;
            case DutyAssignmentChange c when change.Added && c.AssignmentId is { } assignmentId && c.BeforeTeacherId != c.AfterTeacherId && c.BeforeTeacherId.Length > 0:
                Add(c.SchoolId, $"duty-assignment:{assignmentId}:{c.AfterRevision}", "duty", "DutyAssignment", assignmentId,
                    "تغيير تكليف المناوبة", [new("DutyPreviousTeacher", DutyPermissions.View, c.BeforeTeacherId)],
                    preview: $"تم تعديل تكليف المناوبة ليوم {c.Date:yyyy-MM-dd}.", date: c.Date);
                break;
            case DutyAssignment a when Changed(nameof(a.Revision), nameof(a.IsCancelled), nameof(a.EffectiveTeacherId)):
                Add(a.SchoolId, $"duty-assignment:{a.Id}:{a.Revision}", "duty", "DutyAssignment", a.Id,
                    a.IsCancelled ? "تم إلغاء تكليف المناوبة" : "تحديث تكليف المناوبة",
                    [new("DutyTeacher", DutyPermissions.View, a.EffectiveTeacherId)],
                    preview: $"راجع تكليف المناوبة ليوم {a.Date:yyyy-MM-dd}.", date: a.Date);
                break;
            case AttendanceCompletion c when c.RosterCount > 0 && change.Added:
                Add(c.SchoolId, c.ClassroomId is { } classroom ? $"students:{classroom}:{c.Date:yyyy-MM-dd}" : $"staff:{c.Date:yyyy-MM-dd}",
                    "attendance", c.ClassroomId is null ? "StaffAttendance" : "StudentAttendance", c.Id,
                    c.ClassroomId is null ? "اكتمل رصد حضور الموظفين" : "اكتمل رصد حضور الفصل",
                    [new("Manager", c.ClassroomId is null ? PermissionNames.AttendanceView : PermissionNames.StudentAffairsDashboardSchoolOversight)],
                    preview: $"اكتمل رصد الحضور ليوم {c.Date:yyyy-MM-dd} ({c.RosterCount} في الكشف).", date: c.Date);
                break;
        }
        return result;
    }
}
