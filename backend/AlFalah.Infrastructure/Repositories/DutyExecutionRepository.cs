using System.Text.Json;
using AlFalah.Application.Duty;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Duty;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using AlFalah.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class DutyExecutionRepository(AlFalahDbContext db) : IDutyExecutionRepository
{
    public Task<DutyAssignment?> AssignmentAsync(int school, int id, CancellationToken ct) => db.Set<DutyAssignment>().SingleOrDefaultAsync(x => x.SchoolId == school && x.Id == id, ct);
    public Task<DutyPlan> PlanAsync(int school, int id, CancellationToken ct) => db.Set<DutyPlan>().SingleAsync(x => x.SchoolId == school && x.Id == id, ct);
    public async Task<string> SchoolTimeZoneAsync(int school, CancellationToken ct) => await db.Set<DutyPlan>().AsNoTracking()
        .Where(x => x.SchoolId == school).OrderByDescending(x => x.PublishedAt).Select(x => x.TimeZoneId).FirstOrDefaultAsync(ct) ?? "Africa/Cairo";
    public async Task<DutyExecutionHeader> HeaderAsync(int school, DutyAssignment a, DutyPlan plan, CancellationToken ct)
    {
        var s = await db.Schools.AsNoTracking().Where(x => x.Id == school).Select(x => new {
            x.Name, x.City, Header = x.ReportSettings == null ? null : x.ReportSettings.ReportHeaderText,
            Footer = x.ReportSettings == null ? null : x.ReportSettings.ReportFooterText,
            Logo = x.ReportSettings == null ? x.LogoUrl : x.ReportSettings.LogoUrl,
            Manager = x.Manager == null ? "" : x.Manager.FirstName + " " + x.Manager.LastName }).SingleAsync(ct);
        var term = await db.AcademicTerms.Where(x => x.SchoolId == school && x.Id == plan.AcademicTermId)
            .Select(x => new { Year = x.AcademicYear.NameAr, Semester = (int)x.Semester }).SingleAsync(ct);
        var names = await db.Users.IgnoreQueryFilters().Where(x => x.Id == a.OriginalTeacherId || x.Id == a.EffectiveTeacherId)
            .Select(x => new { x.Id, Name = x.FirstName + " " + x.LastName }).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        return new(s.Name, s.City, s.Header, s.Footer, s.Logo, s.Manager, term.Year, term.Semester,
            names[a.OriginalTeacherId], names[a.EffectiveTeacherId], a.OriginalTeacherId != a.EffectiveTeacherId);
    }
    public Task<UserSignature?> SignatureAsync(string user, CancellationToken ct) => db.UserSignatures.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == user, ct);
    public async Task<TimeOnly?> DismissalAsync(DutyPlan plan, DateOnly date, CancellationToken ct)
    {
        if (plan.BellScheduleRevisionId is null) return null;
        var days = await db.Set<BellScheduleDay>().AsNoTracking().Where(x => x.BellScheduleRevisionId == plan.BellScheduleRevisionId)
            .Include(x => x.Periods).ToListAsync(ct);
        var day = days.SingleOrDefault(x => x.Day == ((int)date.DayOfWeek + 1) % 7 + 1);
        if (day is null || !day.IsStudyDay) return null;
        var periods = day.UsesDefaultSchedule ? days.SingleOrDefault(x => x.Day == 0)?.Periods : day.Periods;
        return periods?.Select(x => (TimeOnly?)x.EndLocalTime).Max();
    }
    public Task<List<DutyReportDraft>> DraftsAsync(int school, int assignment, CancellationToken ct) => db.Set<DutyReportDraft>().Where(x => x.SchoolId == school && x.AssignmentId == assignment).ToListAsync(ct);
    public Task<List<DutySignedDocument>> DocumentsAsync(int school, int assignment, CancellationToken ct) => db.Set<DutySignedDocument>().AsNoTracking().Where(x => x.SchoolId == school && x.AssignmentId == assignment).OrderBy(x => x.Id).ToListAsync(ct);
    public Task<DutySignedDocument?> DocumentAsync(int school, int id, CancellationToken ct) => db.Set<DutySignedDocument>().AsNoTracking().SingleOrDefaultAsync(x => x.SchoolId == school && x.Id == id, ct);
    private IQueryable<StudentEnrollment> Roster(int school, int term, DateOnly date) => db.StudentEnrollments.AsNoTracking().Where(x => x.SchoolId == school
        && x.AcademicTermId == term && x.Status == StudentEnrollmentStatus.Active && x.Student.IsActive && x.Classroom.IsActive
        && x.Classroom.AcademicYearId == x.AcademicTerm.AcademicYearId
        && x.EnrolledOn <= date && (x.WithdrawnOn == null || x.WithdrawnOn >= date));
    private static IQueryable<DutyStudent> Project(IQueryable<StudentEnrollment> query) => query.Select(x => new DutyStudent(x.StudentId, x.Id, x.ClassroomId,
        (x.Student.FirstName + " " + (x.Student.MiddleName ?? "") + " " + x.Student.LastName).Trim(), x.Classroom.ClassLabel, x.Classroom.GradeLevel, x.Student.StudentNumber));
    public async Task<DutyRoster> RosterAsync(int school, DutyPlan plan, DateOnly date, DutyRosterQuery query, CancellationToken ct)
    {
        var all = Roster(school, plan.AcademicTermId, date);
        var classes = await all.GroupBy(x => new { x.ClassroomId, x.Classroom.ClassLabel, x.Classroom.GradeLevel })
            .OrderBy(x => x.Key.GradeLevel).ThenBy(x => x.Key.ClassLabel).Select(x => new DutyClass(x.Key.ClassroomId, x.Key.ClassLabel, x.Key.GradeLevel, x.Count())).ToListAsync(ct);
        var filtered = all;
        if (query.Grade is not null) filtered = filtered.Where(x => x.Classroom.GradeLevel == query.Grade);
        if (query.ClassroomId is not null) filtered = filtered.Where(x => x.ClassroomId == query.ClassroomId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            filtered = filtered.Where(x => (x.Student.FirstName + " " + (x.Student.MiddleName ?? "") + " " + x.Student.LastName).Contains(search) || x.Student.StudentNumber.Contains(search));
        }
        var count = await filtered.CountAsync(ct); var page = Math.Max(1, query.Page);
        var items = await Project(filtered.OrderBy(x => x.Classroom.GradeLevel).ThenBy(x => x.Classroom.ClassLabel).ThenBy(x => x.Student.FirstName).ThenBy(x => x.StudentId))
            .Skip((page - 1) * 24).Take(24).ToListAsync(ct);
        return new(new() { Items = items, TotalCount = count, Page = page, PageSize = 24 }, classes);
    }
    public Task<DutyStudent?> StudentAsync(int school, DutyPlan plan, DateOnly date, int student, int enrollment, CancellationToken ct) =>
        Project(Roster(school, plan.AcademicTermId, date).Where(x => x.StudentId == student && x.Id == enrollment)).SingleOrDefaultAsync(ct);
    public Task<List<DutyStudent>> StudentsAsync(int school, DutyPlan plan, DateOnly date, IReadOnlyList<int> studentIds, CancellationToken ct) =>
        Project(Roster(school, plan.AcademicTermId, date).Where(x => studentIds.Contains(x.StudentId))).ToListAsync(ct);
    public async Task<List<StudentDismissalLateness>> FactsAsync(int school, int assignment, CancellationToken ct)
    {
        var date = await db.Set<DutyAssignment>().Where(x => x.SchoolId == school && x.Id == assignment).Select(x => x.Date).SingleAsync(ct);
        return await db.Set<StudentDismissalLateness>().Where(x => x.SchoolId == school && x.DutyDate == date).OrderBy(x => x.Id).ToListAsync(ct);
    }
    public Task<StudentDismissalLateness?> FactAsync(int school, int id, CancellationToken ct) => db.Set<StudentDismissalLateness>().SingleOrDefaultAsync(x => x.SchoolId == school && x.Id == id, ct);
    public Task<StudentDismissalLateness?> ExistingFactAsync(int school, int student, DateOnly date, CancellationToken ct) => db.Set<StudentDismissalLateness>().SingleOrDefaultAsync(x => x.SchoolId == school && x.StudentId == student && x.DutyDate == date, ct);
    public async Task<IReadOnlyList<DutyNoticeStatus>> NoticeStatusAsync(int school, int fact, CancellationToken ct)
        => (await NoticeStatusesAsync(school, [fact], ct)).GetValueOrDefault(fact) ?? [];
    public async Task<Dictionary<int, IReadOnlyList<DutyNoticeStatus>>> NoticeStatusesAsync(int school, IReadOnlyList<int> facts, CancellationToken ct)
    {
        var batches = await db.Set<DismissalNoticeBatch>().AsNoTracking().Where(x => x.SchoolId == school && facts.Contains(x.FactId)).OrderBy(x => x.Revision).ToListAsync(ct);
        var ids = batches.Select(x => x.Id).ToArray();
        var recipients = await db.Set<DismissalNoticeRecipient>().AsNoTracking().Where(x => x.SchoolId == school && ids.Contains(x.BatchId)).ToListAsync(ct);
        var notificationIds = recipients.Where(x => x.NotificationId != null).Select(x => x.NotificationId!.Value).ToArray();
        var reads = await db.Notifications.AsNoTracking().Where(x => x.SchoolId == school && notificationIds.Contains(x.Id) && x.IsRead).Select(x => x.Id).ToListAsync(ct);
        var messageIds = recipients.Where(x => x.MessageId != null).Select(x => x.MessageId!.Value).ToArray();
        var messageReads = await db.MessageReceipts.AsNoTracking().Where(x => x.SchoolId == school && messageIds.Contains(x.ConversationMessageId) && x.ReadAt != null).Select(x => x.ConversationMessageId).ToListAsync(ct);
        return batches.GroupBy(b => b.FactId).ToDictionary(g => g.Key, g => (IReadOnlyList<DutyNoticeStatus>)g.Select(b => { var r = recipients.Where(x => x.BatchId == b.Id).ToArray(); return new DutyNoticeStatus(b.Revision, b.State, r.Length,
            r.Count(x => x.DashboardState == "Delivered"), r.Count(x => x.MessageState == "Delivered"), r.Count(x => reads.Contains(x.NotificationId ?? 0)), r.Count(x => messageReads.Contains(x.MessageId ?? 0))); }).ToArray());
    }
    public async Task<DutyDismissalStatistics> StatisticsAsync(int school, DutyDismissalQuery query, CancellationToken ct)
    {
        var all = db.Set<StudentDismissalLateness>().AsNoTracking().Where(x => x.SchoolId == school && !x.IsRetracted);
        if (query.From is not null) all = all.Where(x => x.DutyDate >= query.From);
        if (query.To is not null) all = all.Where(x => x.DutyDate <= query.To);
        if (query.TermId is not null) all = all.Where(x => x.AcademicTermId == query.TermId);
        if (query.ClassroomId is not null) all = all.Where(x => x.ClassroomId == query.ClassroomId);
        var count = await all.CountAsync(ct); var unique = await all.Select(x => x.StudentId).Distinct().CountAsync(ct);
        var rows = await all.OrderByDescending(x => x.DutyDate).ThenBy(x => x.Id).Skip((Math.Max(1, query.Page) - 1) * 24).Take(24).ToListAsync(ct);
        var statuses = await NoticeStatusesAsync(school, rows.Select(x => x.Id).ToArray(), ct);
        var items = rows.Select(f => ToDto(f, statuses.GetValueOrDefault(f.Id) ?? [])).ToList();
        return new(unique, count, new() { Items = items, TotalCount = count, Page = Math.Max(1, query.Page), PageSize = 24 });
    }
    public static DutyLateDto ToDto(StudentDismissalLateness f, IReadOnlyList<DutyNoticeStatus> status) => new(f.Id, f.Revision, f.StudentId, f.EnrollmentId, f.ClassroomId,
        f.StudentName, f.Classroom, f.DutyDate, f.ObservedAt, f.DismissalAt, f.DepartureAt, f.Action, f.Notes, f.IsRetracted, status);
    public Task<List<DismissalNoticeBatch>> PendingNoticesAsync(CancellationToken ct) => db.Set<DismissalNoticeBatch>().AsNoTracking().Where(x => x.State != "Delivered" && x.NextAttemptAt <= DateTimeOffset.UtcNow).OrderBy(x => x.Id).Take(30).ToListAsync(ct);
    public Task<DismissalNoticeBatch?> BatchAsync(int school, int id, CancellationToken ct) => db.Set<DismissalNoticeBatch>().SingleOrDefaultAsync(x => x.SchoolId == school && x.Id == id, ct);
    public Task<List<DismissalNoticeBatch>> FactBatchesAsync(int school, int fact, CancellationToken ct) => db.Set<DismissalNoticeBatch>().Where(x => x.SchoolId == school && x.FactId == fact).ToListAsync(ct);
    public Task<List<DismissalNoticeRecipient>> NoticeRecipientsAsync(int school, int batch, CancellationToken ct) => db.Set<DismissalNoticeRecipient>().Where(x => x.SchoolId == school && x.BatchId == batch).ToListAsync(ct);
    public Task<List<string>> RecipientsAsync(int school, int student, DateOnly currentDate, CancellationToken ct) => db.StudentGuardians.AsNoTracking()
        .Where(x => x.SchoolId == school && x.StudentId == student && x.ReceivesNotifications && x.ValidFrom <= currentDate && (x.ValidTo == null || x.ValidTo >= currentDate)
            && x.GuardianProfile.IsActive && x.GuardianProfile.ApplicationUser.IsActive
            && db.UserSchoolRoles.Any(r => r.SchoolId == school && r.UserId == x.GuardianProfile.ApplicationUserId && r.IsActive && r.Role.Name == "Guardian"))
        .OrderByDescending(x => x.IsPrimary).Select(x => x.GuardianProfile.ApplicationUserId).Distinct().ToListAsync(ct);
    public async Task DeliverMessageAsync(StudentDismissalLateness fact, DismissalNoticeBatch batch, DismissalNoticeRecipient recipient, CancellationToken ct)
    {
        var key = $"dismissal:{fact.Id}:{batch.Revision}:{recipient.RecipientUserId}";
        var existing = await db.ConversationMessages.SingleOrDefaultAsync(x => x.SchoolId == fact.SchoolId && x.SystemCorrelationId == recipient.CorrelationId, ct);
        if (existing is not null) { recipient.MessageId = existing.Id; recipient.ThreadId = existing.ConversationThreadId; recipient.MessageState = "Delivered"; return; }
        var thread = await db.ConversationThreads.Include(x => x.Participants).Where(x => x.SchoolId == fact.SchoolId && x.StudentId == fact.StudentId
            && x.ThreadType == ConversationThreadType.SystemNotice && x.Participants.Any(p => p.ApplicationUserId == recipient.RecipientUserId)).SingleOrDefaultAsync(ct);
        var now = DateTimeOffset.UtcNow;
        if (thread is null)
        {
            thread = new() { SchoolId = fact.SchoolId, StudentId = fact.StudentId, ThreadType = ConversationThreadType.SystemNotice,
                Status = ConversationThreadStatus.Closed, Subject = "النظام · تأخر الانصراف / System · Dismissal lateness",
                CreatedByUserId = fact.AuthorUserId, UpdatedByUserId = fact.AuthorUserId };
            thread.Participants.Add(new() { SchoolId = fact.SchoolId, ApplicationUserId = recipient.RecipientUserId, ParticipantRoleSnapshot = "Guardian", CreatedByUserId = fact.AuthorUserId, UpdatedByUserId = fact.AuthorUserId });
            db.Add(thread); await db.SaveChangesAsync(ct);
        }
        var message = new ConversationMessage { SchoolId = fact.SchoolId, ConversationThreadId = thread.Id, SenderUserId = null,
            IsSystemOrigin = true, SystemCorrelationId = recipient.CorrelationId, SourceFactId = fact.Id, SourceFactRevision = batch.Revision,
            Body = batch.Content, SentAt = now, ReleasedAt = now, OfficeHoursDisposition = OfficeHoursDisposition.SentImmediately,
            IdempotencyKey = key, IdempotencyPayloadHash = DutyRotation.Digest(batch.Content), CreatedByUserId = fact.AuthorUserId, UpdatedByUserId = fact.AuthorUserId };
        message.Receipts.Add(new() { SchoolId = fact.SchoolId, RecipientUserId = recipient.RecipientUserId, DeliveryState = MessageDeliveryState.Delivered, DeliveredAt = now });
        db.Add(message); thread.UpdatedAt = now; await db.SaveChangesAsync(ct);
        recipient.MessageId = message.Id; recipient.ThreadId = thread.Id; recipient.MessageState = "Delivered";
        // This narrow path creates no generic MessageReleased event/dashboard alert.
    }
    public async Task DeliverDashboardAsync(StudentDismissalLateness fact, DismissalNoticeBatch batch, DismissalNoticeRecipient recipient, CancellationToken ct)
    {
        var key = $"dismissal:{fact.Id}:{batch.Revision}:{recipient.RecipientUserId}";
        var notification = await db.Notifications.SingleOrDefaultAsync(x => x.SchoolId == fact.SchoolId && x.UserId == recipient.RecipientUserId && x.DeduplicationKey == key, ct);
        if (notification is null)
        {
            notification = new() { SchoolId = fact.SchoolId, UserId = recipient.RecipientUserId, StudentId = fact.StudentId,
                Title = batch.Revision == 1 ? "النظام · تأخر الانصراف / System · Dismissal lateness" : "النظام · تصحيح تأخر الانصراف / System · Dismissal correction",
                Message = batch.Content, Type = "DismissalLateness", RelatedEntityType = "DismissalLateness", RelatedEntityId = fact.Id.ToString(),
                CorrelationId = recipient.CorrelationId, DeduplicationKey = key, DeliveryStatus = NotificationDeliveryStatus.Delivered, DeliveredAt = DateTimeOffset.UtcNow };
            db.Add(notification); await db.SaveChangesAsync(ct);
        }
        recipient.NotificationId = notification.Id; recipient.DashboardState = "Delivered";
    }
    private IQueryable<DismissalNoticeRecipient> GuardianRows(int school, string user, DateOnly date) => db.Set<DismissalNoticeRecipient>().AsNoTracking().Where(x => x.SchoolId == school && x.RecipientUserId == user
        && db.UserSchoolRoles.Any(r => r.SchoolId == school && r.UserId == user && r.IsActive && r.Role.Name == "Guardian")
        && db.Set<DismissalNoticeBatch>().Any(b => b.Id == x.BatchId && b.SchoolId == school && db.StudentGuardians.Any(g => g.SchoolId == school
            && g.StudentId == db.Set<StudentDismissalLateness>().Where(f => f.Id == b.FactId).Select(f => f.StudentId).First()
            && g.GuardianProfile.ApplicationUserId == user && g.GuardianProfile.IsActive && g.GuardianProfile.ApplicationUser.IsActive
            && g.ValidFrom <= date && (g.ValidTo == null || g.ValidTo >= date))));
    public async Task<List<DutyGuardianNotice>> GuardianNoticesAsync(int school, string user, DateOnly currentDate, CancellationToken ct) =>
        await (from r in GuardianRows(school, user, currentDate)
            join b in db.Set<DismissalNoticeBatch>() on r.BatchId equals b.Id
            join f in db.Set<StudentDismissalLateness>() on b.FactId equals f.Id
            join n in db.Notifications on r.NotificationId equals n.Id
            orderby n.CreatedAt descending
            select new DutyGuardianNotice(n.Id, f.Id, b.Revision, f.StudentName, f.DutyDate, n.Message,
                r.ThreadId == null ? null : "/student-affairs/messages?conversationId=" + r.ThreadId, n.IsRead, n.CreatedAt)).Take(40).ToListAsync(ct);
    public async Task MarkGuardianNoticeReadAsync(int school, string user, int notification, DateOnly currentDate, CancellationToken ct)
    {
        if (!await GuardianRows(school, user, currentDate).AnyAsync(x => x.NotificationId == notification, ct)) throw new DutyException("DutyForbidden", "الإشعار غير متاح. / Notice unavailable.", 403);
        var row = await db.Notifications.SingleAsync(x => x.SchoolId == school && x.UserId == user && x.Id == notification, ct);
        row.IsRead = true; row.ReadAt ??= DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct);
    }
    private Task<DutyAssignmentChange?> Timing(int school, int assignment, int revision, CancellationToken ct) => db.Set<DutyAssignmentChange>().AsNoTracking()
        .Where(x => x.SchoolId == school && x.AssignmentId == assignment && x.AfterRevision == revision && x.Kind == "TimingException").OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
    public async Task<string?> TimingReasonAsync(int school, int assignment, int revision, CancellationToken ct) => (await Timing(school, assignment, revision, ct))?.Reason;
    public async Task<TimeOnly?> DismissalOverrideAsync(int school, int assignment, int revision, CancellationToken ct)
    {
        var value = (await Timing(school, assignment, revision, ct))?.SettingsSnapshotJson;
        return value is null ? null : JsonSerializer.Deserialize<TimeOnly?>(value);
    }
    public Task<bool> UnavailableAsync(int school, string user, DateOnly date, CancellationToken ct) => db.AttendanceRecords.AsNoTracking()
        .AnyAsync(x => x.SchoolId == school && x.UserId == user && x.AttendanceDate == date && x.Status != AlFalah.Domain.Enums.AttendanceStatus.Present, ct);
}
