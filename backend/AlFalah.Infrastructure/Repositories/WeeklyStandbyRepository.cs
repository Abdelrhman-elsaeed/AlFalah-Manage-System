using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class WeeklyStandbyRepository(AlFalahDbContext db) : TimetableReviewRepository(db), IWeeklyStandbyRepository
{
    public Task<bool> HasEditorGrantAsync(int school, string userId, CancellationToken ct) =>
        db.TimetableEditorGrants.AnyAsync(x => x.SchoolId == school && x.ModeratorUserId == userId, ct);

    public async Task<IReadOnlyList<string>> GetProtectedCellsAsync(int school, int timetable, DateOnly from, CancellationToken ct)
    {
        var movements = await db.Set<TimetableSubstitutionMovement>().AsNoTracking()
            .Where(x => x.SchoolId == school && x.Substitution.SchoolTimetableId == timetable &&
                x.Substitution.Kind == "Substitution" && x.Substitution.LocalDate >= from)
            .Select(x => new { x.Substitution.LocalDate, x.SchoolTimetableEntryId, x.TimetableSubstitutionId,
                x.ToTeacherId, Day = x.ToDay ?? x.Day, x.ToPeriod }).ToListAsync(ct);
        return movements.OrderByDescending(x => x.TimetableSubstitutionId)
            .DistinctBy(x => (x.LocalDate, x.SchoolTimetableEntryId))
            .Select(x => WeeklyStandbyEngine.Key(x.ToTeacherId, x.Day, x.ToPeriod)).Distinct().ToArray();
    }

    public void StageStandby(SchoolTimetable timetable, IReadOnlyList<StandbyPlacement> placements, DateTimeOffset now)
    {
        var desired = placements.ToDictionary(x => WeeklyStandbyEngine.Key(x.InstructorProfileId, x.Day, x.Period));
        foreach (var row in timetable.Entries.Where(x => !x.IsDeleted && x.EntryType == TimetableEntryType.Standby).ToArray())
        {
            if (!desired.Remove(WeeklyStandbyEngine.Key(row.InstructorProfileId, (int)row.Day, row.Period), out var p))
            { row.IsDeleted = true; row.DeletedAt = now; }
            else { row.IsStandbyLocked = p.IsLocked; row.StandbySource = p.Source; row.UpdatedAt = now; }
        }
        foreach (var p in desired.Values) timetable.Entries.Add(new SchoolTimetableEntry {
            SchoolId = timetable.SchoolId, SchoolTimetableId = timetable.Id, InstructorProfileId = p.InstructorProfileId,
            Day = (TimetableDay)p.Day, Period = p.Period, EntryType = TimetableEntryType.Standby,
            IsStandbyLocked = p.IsLocked, StandbySource = p.Source, CreatedAt = now, UpdatedAt = now });
    }
}
