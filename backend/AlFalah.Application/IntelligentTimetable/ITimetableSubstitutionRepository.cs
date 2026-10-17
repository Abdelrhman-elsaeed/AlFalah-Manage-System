using AlFalah.Domain.Entities;

namespace AlFalah.Application.IntelligentTimetable;

public interface ITimetableSubstitutionRepository : ITimetableReviewRepository
{
    Task<IReadOnlyList<TimetableSubstitution>> GetChangesAsync(int school, int timetable, DateOnly? date, CancellationToken ct);
    Task<TimetableSubstitution?> FindRequestAsync(int school, Guid requestId, CancellationToken ct);
    void AddChange(TimetableSubstitution change);
    Task<IReadOnlyList<int>> GetAbsentTeachersAsync(int school, DateOnly date, CancellationToken ct);
    Task<bool> HasEditorGrantAsync(int school, string userId, CancellationToken ct);
    Task<DailyCoverageNeed?> CoverageNeedAsync(int school, int timetable, DateOnly date, int entry, CancellationToken ct);
    void AddNeed(DailyCoverageNeed need);
    void AddRecord(CoverageRecord record);
    Task<bool> IsEffectivePublicationAsync(int school, int timetable, DateOnly date, CancellationToken ct);
}
