using AlFalah.Domain.Entities;

namespace AlFalah.Application.Interfaces;

public interface IVisitFeedbackBankRepository
{
    Task<IReadOnlyList<VisitFeedbackTemplate>> ListAsync(int schoolId, CancellationToken cancellationToken);
    Task<bool> HasAnyIncludingDeletedAsync(int schoolId, CancellationToken cancellationToken);
    Task<VisitFeedbackTemplate?> GetAsync(int schoolId, int id, CancellationToken cancellationToken);
    Task AddRangeAsync(IEnumerable<VisitFeedbackTemplate> items, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
