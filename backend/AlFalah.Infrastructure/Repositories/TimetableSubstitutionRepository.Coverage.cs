using AlFalah.Application.IntelligentTimetable;
using AlFalah.Domain.Entities;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed partial class TimetableSubstitutionRepository : IDailyCoverageRepository
{
    public async Task<bool> IsEffectivePublicationAsync(int school, int timetable, DateOnly date, CancellationToken ct)
    { var ids = await PublishedTimetablesAsync(school, date, ct); return ids.Count == 1 && ids[0] == timetable; }
    public async Task<IReadOnlyList<int>> PublishedTimetablesAsync(int school, DateOnly date, CancellationToken ct) =>
        await db.SchoolTimetables.AsNoTracking().Where(t => t.SchoolId == school && t.IsPublished && !t.IsClone &&
            t.AcademicYear.StartsOn <= date && date <= t.AcademicYear.EndsOn && db.AcademicTerms.Any(a =>
                a.SchoolId == school && a.IsActive && a.AcademicYearId == t.AcademicYearId && a.Semester == t.Semester && a.StartsOn <= date && date <= a.EndsOn))
            .Select(t => t.Id).ToArrayAsync(ct);
    private IQueryable<DailyCoverageNeed> NeedQuery(int school) => db.Set<DailyCoverageNeed>().AsTracking().Where(n => n.SchoolId == school)
        .Include(n => n.ActiveSubstitution).ThenInclude(s => s!.Movements).Include(n => n.Records).AsSplitQuery();
    public async Task<IReadOnlyList<DailyCoverageNeed>> NeedsAsync(int school, DateOnly date, CancellationToken ct) =>
        await NeedQuery(school).Where(n => n.LocalDate == date).OrderBy(n => n.StartsAt).ThenBy(n => n.Id).ToArrayAsync(ct);
    public Task<DailyCoverageNeed?> NeedAsync(int school, int id, CancellationToken ct) => NeedQuery(school).SingleOrDefaultAsync(n => n.Id == id, ct);
    private IQueryable<DailyCoverageNeed> Owned(int school, string userId) => NeedQuery(school).Where(n => n.ActiveSubstitution != null && db.InstructorProfiles.Any(t =>
        t.SchoolId == school && t.UserId == userId && t.IsActive && t.User.IsActive && n.ActiveSubstitution.Movements.Any(m => m.ToTeacherId == t.Id)));
    public Task<bool> IsAssignedUserAsync(int school, int need, string userId, CancellationToken ct) => Owned(school, userId).AnyAsync(n => n.Id == need, ct);
    public async Task<AlFalah.Shared.Models.PagedResult<DailyCoverageNeed>> TeacherTasksAsync(int school, string userId, int page, int pageSize, CancellationToken ct)
    {
        var query = Owned(school, userId);
        return new() { Page = page, PageSize = pageSize, TotalCount = await query.CountAsync(ct),
            Items = await query.OrderBy(n => n.SubmittedAt != null).ThenBy(n => n.LocalDate).ThenBy(n => n.StartsAt).ThenBy(n => n.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct) };
    }
    public Task<CoverageRecord?> RecordAsync(int school, Guid requestId, CancellationToken ct) =>
        db.Set<CoverageRecord>().AsNoTracking().SingleOrDefaultAsync(r => r.SchoolId == school && r.RequestId == requestId, ct);
    public async Task<IReadOnlyList<CoverageRecord>> ExecutionsAsync(int school, int year, int semester, CancellationToken ct) =>
        await db.Set<CoverageRecord>().AsNoTracking().Where(r => r.SchoolId == school && r.Kind == "Execution" &&
            db.SchoolTimetables.Any(t => t.SchoolId == school && t.Id == r.Need.SchoolTimetableId && t.AcademicYearId == year && (int)t.Semester == semester))
            .Include(r => r.Need).ThenInclude(n => n.ActiveSubstitution).ThenInclude(s => s!.Movements).ToArrayAsync(ct);
    public async Task<IReadOnlyList<DailyCoverageNeed>> PendingTermAsync(int school, int year, int semester, CancellationToken ct) =>
        await db.Set<DailyCoverageNeed>().AsNoTracking().Where(n => n.SchoolId == school && n.ActiveSubstitutionId != null && n.SubmittedAt == null && !n.Records.Any(r => r.Kind == "Execution") &&
            db.SchoolTimetables.Any(t => t.SchoolId == school && t.Id == n.SchoolTimetableId && t.AcademicYearId == year && (int)t.Semester == semester)).ToArrayAsync(ct);
    public Task<UserSignature?> SignatureAsync(string userId, CancellationToken ct) => db.Set<UserSignature>().AsNoTracking().SingleOrDefaultAsync(s => s.UserId == userId, ct);
    public void AddRecord(CoverageRecord record) => db.Add(record);
    public void AddSignal(CoverageReconciliationSignal signal) => db.Add(signal);
    public async Task<IReadOnlyList<long>> ClaimSignalsAsync(string owner, DateTimeOffset now, CancellationToken ct)
    {
        var ids = await db.Set<CoverageReconciliationSignal>().AsNoTracking().Where(s => s.ProcessedAt == null &&
            (s.NextAttemptAt == null || s.NextAttemptAt <= now) && (s.LeaseUntil == null || s.LeaseUntil <= now)).OrderBy(s => s.Id).Take(20).Select(s => s.Id).ToArrayAsync(ct);
        var claimed = new List<long>();
        foreach (var id in ids)
        {
            var query = db.Set<CoverageReconciliationSignal>().Where(s => s.Id == id && s.ProcessedAt == null && (s.LeaseUntil == null || s.LeaseUntil <= now));
            if (db.Database.IsRelational())
            { if (await query.ExecuteUpdateAsync(u => u.SetProperty(s => s.LeaseOwner, owner).SetProperty(s => s.LeaseUntil, now.AddMinutes(2)), ct) == 1) claimed.Add(id); }
            else { var row = await query.SingleOrDefaultAsync(ct); if (row != null) { row.LeaseOwner = owner; row.LeaseUntil = now.AddMinutes(2); claimed.Add(id); } }
        }
        if (!db.Database.IsRelational()) await SaveAsync(ct);
        return claimed;
    }
    public Task<CoverageReconciliationSignal?> SignalAsync(long id, string owner, CancellationToken ct) => db.Set<CoverageReconciliationSignal>().AsTracking().SingleOrDefaultAsync(s => s.Id == id && s.LeaseOwner == owner && s.ProcessedAt == null, ct);
    public async Task FailedSignalAsync(long id, string owner, DateTimeOffset now, string error, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); var signal = await SignalAsync(id, owner, ct); if (signal == null) return;
        signal.Attempts++; signal.LastError = error[..Math.Min(2000, error.Length)]; signal.NextAttemptAt = now.AddSeconds(Math.Min(3600, Math.Pow(2, Math.Min(signal.Attempts, 11))));
        signal.LeaseOwner = null; signal.LeaseUntil = null; await SaveAsync(ct);
    }
}
