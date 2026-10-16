using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;

namespace AlFalah.Application.IntelligentTimetable;

public interface IWeeklyStandbyRepository : ITimetableReviewRepository
{
    Task<bool> HasEditorGrantAsync(int school, string userId, CancellationToken ct);
    Task<IReadOnlyList<string>> GetProtectedCellsAsync(int school, int timetable, DateOnly from, CancellationToken ct);
    void StageStandby(SchoolTimetable timetable, IReadOnlyList<StandbyPlacement> placements, DateTimeOffset now);
}
