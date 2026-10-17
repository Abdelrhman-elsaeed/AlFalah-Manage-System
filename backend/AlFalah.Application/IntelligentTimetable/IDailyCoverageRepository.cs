using AlFalah.Domain.Entities;

namespace AlFalah.Application.IntelligentTimetable;

public interface IDailyCoverageRepository : ITimetableSubstitutionRepository
{
    Task<IReadOnlyList<int>> PublishedTimetablesAsync(int school, DateOnly date, CancellationToken ct);
    Task<IReadOnlyList<DailyCoverageNeed>> NeedsAsync(int school, DateOnly date, CancellationToken ct);
    Task<DailyCoverageNeed?> NeedAsync(int school, int id, CancellationToken ct);
    Task<AlFalah.Shared.Models.PagedResult<DailyCoverageNeed>> TeacherTasksAsync(int school, string userId, int page, int pageSize, CancellationToken ct);
    Task<bool> IsAssignedUserAsync(int school, int need, string userId, CancellationToken ct);
    Task<CoverageRecord?> RecordAsync(int school, Guid requestId, CancellationToken ct);
    Task<IReadOnlyList<CoverageRecord>> ExecutionsAsync(int school, int year, int semester, CancellationToken ct);
    Task<IReadOnlyList<DailyCoverageNeed>> PendingTermAsync(int school, int year, int semester, CancellationToken ct);
    Task<UserSignature?> SignatureAsync(string userId, CancellationToken ct);
    void AddSignal(CoverageReconciliationSignal signal);
    Task<IReadOnlyList<long>> ClaimSignalsAsync(string owner, DateTimeOffset now, CancellationToken ct);
    Task<CoverageReconciliationSignal?> SignalAsync(long id, string owner, CancellationToken ct);
    Task FailedSignalAsync(long id, string owner, DateTimeOffset now, string error, CancellationToken ct);
}
