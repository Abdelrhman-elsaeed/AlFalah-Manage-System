using System.Data;
using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace AlFalah.Infrastructure.Repositories;

/// <summary>Every read is explicitly school scoped, including reads that retain invalid references for analysis.</summary>
public class TimetableReviewRepository(AlFalahDbContext db) : ITimetableReviewRepository
{
    public async Task<IReadOnlyList<ReviewTimetableOption>> GetTimetablesAsync(int school, CancellationToken ct) =>
        await db.SchoolTimetables.AsNoTracking().Where(x => x.SchoolId == school && x.School.IsActive && !x.School.IsDeleted).OrderByDescending(x => x.UpdatedAt)
            .Select(x => new ReviewTimetableOption(x.Id, x.Title, x.Revision, x.IsPublished)).ToListAsync(ct);
    public async Task<TimetableValidationContext?> GetValidationContextAsync(int school, int timetableId, CancellationToken ct)
    {
        var timetable = await db.SchoolTimetables.AsTracking().Where(x => x.SchoolId == school && x.Id == timetableId && x.School.IsActive && !x.School.IsDeleted)
            .Include(x => x.Entries).Include(x => x.AcademicYear).SingleOrDefaultAsync(ct);
        if (timetable is null) return null;
        var setup = await db.TimetableSetupProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.SchoolId == school && x.Id == timetable.TimetableSetupProfileId, ct);
        var schedule = await db.Set<BellScheduleRevision>().AsNoTracking().Where(x => x.SchoolId == school && x.Id == timetable.BellScheduleRevisionId)
            .Include(x => x.Template).Include(x => x.Days).ThenInclude(x => x.Periods)
            .Include(x => x.Days).ThenInclude(x => x.Breaks).ThenInclude(x => x.Window).AsSplitQuery().SingleOrDefaultAsync(ct);
        var teachers = await db.TeacherTimetableProfiles.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.SchoolId == school && x.TimetableSetupProfileId == timetable.TimetableSetupProfileId)
            .Include(x => x.Instructor).ThenInclude(x => x.User).Include(x => x.Slots).ThenInclude(x => x.Period).AsSplitQuery().ToListAsync(ct);
        var requirements = await db.Set<ClassSubjectRequirement>().IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.SchoolId == school && x.TimetableSetupProfileId == timetable.TimetableSetupProfileId)
            .Include(x => x.Classroom).Include(x => x.Subject).Include(x => x.AllowedDays).Include(x => x.FixedSlots)
            .Include(x => x.Rooms).AsSplitQuery().ToListAsync(ct);
        var assignments = await db.Set<TeachingAssignment>().AsNoTracking()
            .Where(x => x.SchoolId == school && x.TimetableSetupProfileId == timetable.TimetableSetupProfileId && !x.IsDeleted)
            .Include(x => x.Members).ToListAsync(ct);
        var classrooms = await db.Classrooms.IgnoreQueryFilters().AsNoTracking().Where(x => x.SchoolId == school && x.AcademicYearId == timetable.AcademicYearId).ToListAsync(ct);
        var subjects = await db.Set<SubjectDefinition>().IgnoreQueryFilters().AsNoTracking().Where(x => x.SchoolId == school).ToListAsync(ct);
        var rooms = await db.Set<TimetableRoom>().IgnoreQueryFilters().AsNoTracking().Where(x => x.SchoolId == school).ToListAsync(ct);
        return new(timetable, setup, schedule, teachers, requirements, assignments, classrooms, subjects, rooms);
    }
    public Task<TimetableAnalysisRun?> GetLatestAnalysisRunAsync(int school, int timetableId, CancellationToken ct) =>
        db.TimetableAnalysisRuns.AsTracking().Where(x => x.SchoolId == school && x.SchoolTimetableId == timetableId)
            .Include(x => x.Findings).OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
    public async Task<TimetableAnalysisRun?> GetPublishedSnapshotAnalysisRunAsync(int school, int timetableId, CancellationToken ct)
    {
        // Published schedules are immutable structural versions. Select the first analysis
        // produced for the latest such version; later setup-only analyses cannot redefine it.
        var snapshotCreatedAt = await db.SchoolTimetableVersions.AsNoTracking()
            .Where(x => x.SchoolTimetableId == timetableId &&
                (x.ChangeKind == AlFalah.Domain.Enums.TimetableChangeKind.Published ||
                 x.ChangeKind == AlFalah.Domain.Enums.TimetableChangeKind.DirectSwap ||
                 x.ChangeKind == AlFalah.Domain.Enums.TimetableChangeKind.ThreeWaySwap))
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Select(x => (DateTimeOffset?)x.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (snapshotCreatedAt is null) return null;

        return await db.TimetableAnalysisRuns.AsTracking()
            .Where(x => x.SchoolId == school && x.SchoolTimetableId == timetableId &&
                x.CompletedAt.HasValue && x.StartedAt >= snapshotCreatedAt.Value)
            .Include(x => x.Findings)
            .OrderBy(x => x.StartedAt).ThenBy(x => x.Id)
            .FirstOrDefaultAsync(ct);
    }
    public Task<TimetableAnalysisFinding?> GetFindingByIdAsync(int school, int findingId, CancellationToken ct) =>
        db.TimetableAnalysisFindings.AsTracking().Include(x => x.AnalysisRun).ThenInclude(x => x.Findings)
            .SingleOrDefaultAsync(x => x.Id == findingId && x.AnalysisRun.SchoolId == school && !x.AnalysisRun.SchoolTimetable.IsDeleted, ct);
    public void AddAnalysis(TimetableAnalysisRun run) => db.TimetableAnalysisRuns.Add(run);
    public void AddVersion(SchoolTimetableVersion version) => db.SchoolTimetableVersions.Add(version);
    public async Task<int> NextVersionAsync(int timetableId, CancellationToken ct) =>
        (await db.SchoolTimetableVersions.Where(x => x.SchoolTimetableId == timetableId).MaxAsync(x => (int?)x.VersionNumber, ct) ?? 0) + 1;
    public void AddAudit(AuditLog audit) => db.AuditLogs.Add(audit);
    public async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException ex) { throw new BellScheduleConflictException("Timetable review changed", ex); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new BellScheduleConflictException("Concurrent timetable change", ex); }
    }
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        // Serializable reads protect all rule inputs through repair/publication, not only the timetable row.
        if (!db.Database.IsRelational() || db.Database.CurrentTransaction is not null) return await action(ct);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try { var result = await action(ct); await transaction.CommitAsync(ct); return result; }
            catch (SqlException ex) when (ex.Number == 1205)
            { db.ChangeTracker.Clear(); throw new BellScheduleConflictException("Concurrent timetable change", ex); }
            catch { await transaction.RollbackAsync(ct); db.ChangeTracker.Clear(); throw; }
        });
    }
    public async Task StageMovementsAsync(SchoolTimetable timetable, IReadOnlyList<RepairMovementDto> movements, CancellationToken ct)
    {
        // Temporarily exclude moved rows from filtered uniqueness indexes so a swap can be saved atomically.
        var rows = timetable.Entries.Where(x => movements.Any(m => m.EntryId == x.Id)).ToArray();
        foreach (var row in rows) row.IsDeleted = true;
        await SaveAsync(ct);
        foreach (var row in rows)
        {
            var move = movements.Single(x => x.EntryId == row.Id);
            row.Day = move.ToDay; row.Period = move.ToPeriod; row.InstructorProfileId = move.ToTeacherId;
            row.IsDeleted = false; row.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
