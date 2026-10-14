using System.Data;
using System.Text.Json;
using AlFalah.Application.Duty;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Duty;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class DutyRepository(AlFalahDbContext db) : IDutyRepository
{
    public async Task<DutyAccess?> AccessAsync(int schoolId, string userId, CancellationToken ct)
    {
        var school = await db.Schools.AsNoTracking().Where(x => x.Id == schoolId && x.IsActive).Select(x => new { x.Name, x.ManagerUserId }).SingleOrDefaultAsync(ct);
        if (school is null || !await db.Users.AnyAsync(x => x.Id == userId && x.IsActive, ct)) return null;
        var roles = await db.UserSchoolRoles.AsNoTracking().Where(x => x.SchoolId == schoolId && x.UserId == userId && x.IsActive && x.Role.IsActive && x.RoleId!=DutyPermissions.AdministratorRole).Select(x => x.Role.Name).ToListAsync(ct);
        var permissions = await (from membership in db.UserSchoolRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on membership.RoleId equals grant.RoleId
            where membership.SchoolId == schoolId && membership.UserId == userId && membership.IsActive && membership.Role.IsActive
            select grant.Permission.Name).Distinct().ToListAsync(ct);
        return new(school.Name, roles.Count > 0 && permissions.Contains(DutyPermissions.Manage),
            roles.Contains(RoleNames.Instructor) && permissions.Contains(DutyPermissions.View));
    }
    public Task<string?> PlanZoneAsync(int schoolId, int termId, CancellationToken ct) => db.Set<DutyPlan>().AsNoTracking()
        .Where(x=>x.SchoolId==schoolId && x.AcademicTermId==termId).Select(x=>x.TimeZoneId).SingleOrDefaultAsync(ct);
    public Task<List<DutyDashboardDay>> DashboardDaysAsync(int schoolId, DateOnly rangeFrom, DateOnly rangeTo, CancellationToken ct) =>
        (from a in db.Set<DutyAssignment>().AsNoTracking()
         join u in db.Users.IgnoreQueryFilters().AsNoTracking() on a.EffectiveTeacherId equals u.Id
         where a.SchoolId==schoolId && !a.IsCancelled && a.Date>=rangeFrom && a.Date<=rangeTo
         orderby a.Date descending, a.Id
         select new DutyDashboardDay(a.Id,a.Date,u.FirstName+" "+u.LastName,a.ExceptionCode!=null)).ToListAsync(ct);
    public async Task<Dictionary<int,TimeOnly?>> DismissalTimesAsync(DutyPlan plan,IReadOnlyList<DutyAssignment> assignments,CancellationToken ct)
    {
        var ids=assignments.Select(x=>x.Id).ToArray();
        var retained=await db.Set<DutyAssignmentChange>().AsNoTracking().Where(x=>x.SchoolId==plan.SchoolId && ids.Contains(x.AssignmentId??0)
            && x.Kind=="SettingsTimingPreserved").OrderByDescending(x=>x.Id).ToListAsync(ct);
        var days=await db.Set<BellScheduleDay>().AsNoTracking().Where(x=>x.BellScheduleRevisionId==plan.BellScheduleRevisionId).Include(x=>x.Periods).ToListAsync(ct);
        var configured=DutyRotation.Settings(plan).DismissalAt;
        return assignments.ToDictionary(a=>a.Id,a=> {
            var old=retained.FirstOrDefault(x=>x.AssignmentId==a.Id&&x.AfterRevision==a.Revision);
            if(old is not null)return JsonSerializer.Deserialize<TimeOnly?>(old.SettingsSnapshotJson!);
            if(configured is not null)return configured;
            var day=days.SingleOrDefault(x=>x.Day==((int)a.Date.DayOfWeek+1)%7+1);
            return day is not {IsStudyDay:true}?null:(day.UsesDefaultSchedule?days.SingleOrDefault(x=>x.Day==0)?.Periods:day.Periods)?.Select(x=>(TimeOnly?)x.EndLocalTime).Max();
        });
    }
    public Task<List<DutyTerm>> TermsAsync(int schoolId, CancellationToken ct) => db.AcademicTerms.AsNoTracking()
        .Where(x => x.SchoolId == schoolId && x.StartsOn >= x.AcademicYear.StartsOn && x.EndsOn <= x.AcademicYear.EndsOn)
        .OrderByDescending(x => x.IsActive).ThenByDescending(x => x.StartsOn).Select(x => new DutyTerm(x.Id, x.AcademicYearId, x.AcademicYear.NameAr, (int)x.Semester, x.StartsOn, x.EndsOn)).ToListAsync(ct);
    public async Task<DutyTiming> TimingAsync(int schoolId, DutyTerm term, CancellationToken ct)
    {
        var timing = await db.SchoolTimetables.AsNoTracking().Where(x => x.SchoolId == schoolId && x.AcademicYearId == term.YearId
            && (int)x.Semester == term.Semester && x.IsPublished && !x.IsClone && !x.TimingsRequireRevalidation)
            .OrderByDescending(x => x.PublishedAt).ThenByDescending(x => x.Id)
            .Select(x => x.BellScheduleRevisionId).FirstOrDefaultAsync(ct);
        // A configured timetable calendar is usable before lesson publication.
        // Only the template attached to this school's year/term setup is trusted.
        timing ??= await (from setup in db.TimetableSetupProfiles.AsNoTracking()
            join revision in db.Set<BellScheduleRevision>().AsNoTracking() on setup.BellScheduleTemplateId equals revision.BellScheduleTemplateId
            where setup.SchoolId == schoolId && setup.AcademicYearId == term.YearId && (int)setup.Semester == term.Semester
                && !setup.IsDeleted && revision.SchoolId == schoolId && revision.Template.IsActive && !revision.Template.IsDeleted
                && revision.Template.AcademicYearId == term.YearId && (int)revision.Template.Semester == term.Semester
                && revision.Revision == revision.Template.Revision
            orderby setup.UpdatedAt descending, setup.Id descending
            select (int?)revision.Id).FirstOrDefaultAsync(ct);
        if (timing is null) return new(null, "Africa/Cairo", []);
        var zone = await db.Set<BellScheduleRevision>().Where(x => x.Id == timing).Select(x => x.SchoolTimeZoneId).SingleAsync(ct);
        var days = await db.Set<BellScheduleDay>().Where(x => x.BellScheduleRevisionId == timing && x.Day != 0 && x.IsStudyDay).Select(x => x.Day).ToListAsync(ct);
        // TimetableDay: Saturday=1, Sunday=2 ... Friday=7. DateOnly uses Sunday=0.
        return new(timing, zone, days.Select(x => (x + 5) % 7).Order().ToArray());
    }
    public async Task<DutyState> LoadAsync(int schoolId, CancellationToken ct) => new()
    {
        Plans = await db.Set<DutyPlan>().Where(x => x.SchoolId == schoolId).Include(x => x.Participants).Include(x => x.Closures).ToListAsync(ct),
        Assignments = await db.Set<DutyAssignment>().Where(x => x.SchoolId == schoolId).OrderBy(x => x.Date).ToListAsync(ct),
        Changes = await db.Set<DutyAssignmentChange>().Where(x => x.SchoolId == schoolId).OrderBy(x => x.Id).ToListAsync(ct),
        Attendance = await db.AttendanceRecords.AsNoTracking().Where(x => x.SchoolId == schoolId).ToListAsync(ct),
        Executions = await ExecutionsAsync(schoolId, ct),
        Names = await db.Users.IgnoreQueryFilters().AsNoTracking().Where(x => db.UserSchoolRoles.IgnoreQueryFilters().Any(r => r.SchoolId == schoolId && r.UserId == x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FirstName + " " + x.LastName, ct),
        Teachers = await db.InstructorProfiles.AsNoTracking().Where(x => x.SchoolId == schoolId && x.IsActive && x.User.IsActive
            && db.UserSchoolRoles.Any(r => r.SchoolId == schoolId && r.UserId == x.UserId && r.IsActive && r.Role.Name == RoleNames.Instructor))
            .OrderBy(x => x.User.FirstName).Select(x => new DutyTeacher(x.Id, x.UserId, x.User.FirstName + " " + x.User.LastName, true)).ToListAsync(ct)
    };
    private async Task<List<DutyExecutionSummary>> ExecutionsAsync(int schoolId, CancellationToken ct)
    {
        var drafts = await (from d in db.Set<DutyReportDraft>().AsNoTracking()
            join a in db.Set<DutyAssignment>().AsNoTracking() on d.AssignmentId equals a.Id
            where d.SchoolId == schoolId
            select new DutyExecutionSummary(a.Id, a.Date, d.AuthorUserId, d.AssignmentRevision,
                db.Set<DutySignedDocument>().Any(x => x.AssignmentId == a.Id && x.SchoolId == schoolId && x.AuthorUserId == d.AuthorUserId && x.Kind == "Letter"),
                d.SubmittedAt != null, d.SavedAt, d.SubmittedAt)).ToListAsync(ct);
        var letters = await (from document in db.Set<DutySignedDocument>().AsNoTracking()
                join a in db.Set<DutyAssignment>().AsNoTracking() on document.AssignmentId equals a.Id
                where document.SchoolId == schoolId && document.Kind == "Letter"
                    && !db.Set<DutyReportDraft>().Any(d => d.SchoolId == schoolId && d.AssignmentId == a.Id && d.AuthorUserId == document.AuthorUserId)
                select new DutyExecutionSummary(a.Id, a.Date, document.AuthorUserId, document.AssignmentRevision, true, false, null, document.SignedAt)).ToListAsync(ct);
        return drafts.Concat(letters).ToList();
    }
    public Task<List<DutyReportDraft>> ExecutionDraftsAsync(int schoolId, int assignmentId, CancellationToken ct) => db.Set<DutyReportDraft>().AsNoTracking()
        .Where(x => x.SchoolId == schoolId && x.AssignmentId == assignmentId).ToListAsync(ct);
    public async Task<T> AtomicAsync<T>(int schoolId, Func<Task<T>> action, CancellationToken ct)
    {
        if (!db.Database.IsRelational()) return await action();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (db.Database.IsSqlServer())
            {
                var resource = $"AlFalah:Duty:{schoolId}";
                await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @result int; EXEC @result = sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000; IF @result < 0 THROW 51000, 'Duty school lock timeout', 1;", ct);
            }
            try { var result = await action(); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return result; }
            catch { await tx.RollbackAsync(ct); db.ChangeTracker.Clear(); throw; }
        });
    }
    public Task<DutyRequestReceipt?> ReceiptAsync(int schoolId, Guid requestId, CancellationToken ct) => db.Set<DutyRequestReceipt>().SingleOrDefaultAsync(x => x.SchoolId == schoolId && x.RequestId == requestId, ct);
    public Task<List<DutyReconciliationOperation>> PendingAsync(CancellationToken ct) => db.Set<DutyReconciliationOperation>().AsNoTracking().Where(x => x.CompletedAt == null && x.NextAttemptAt <= DateTimeOffset.UtcNow).OrderBy(x => x.Id).Take(50).ToListAsync(ct);
    public Task<DutyReconciliationOperation?> OperationAsync(long id, CancellationToken ct) => db.Set<DutyReconciliationOperation>().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<int> PendingCountAsync(int schoolId, CancellationToken ct) => db.Set<DutyReconciliationOperation>().CountAsync(x => x.SchoolId == schoolId && x.CompletedAt == null, ct);
    public void Add(object entity) => db.Add(entity);
    public void Remove(object entity) => db.Remove(entity);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    public void Notify(int schoolId, string userId, string key, string message) => db.Notifications.Add(new Notification
    {
        SchoolId = schoolId, UserId = userId, Title = "المناوبة / School duty", Message = message, Type = "SchoolDuty",
        RelatedEntityType = "SchoolDuty", RelatedEntityId = "/timetable?tab=duty", DeduplicationKey = key,
        DeliveryStatus = AlFalah.Domain.Enums.StudentAffairs.NotificationDeliveryStatus.Delivered, DeliveredAt = DateTimeOffset.UtcNow
    });
}
