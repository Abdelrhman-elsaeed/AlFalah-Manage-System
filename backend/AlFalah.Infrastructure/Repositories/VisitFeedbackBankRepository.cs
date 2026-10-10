using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class VisitFeedbackBankRepository(AlFalahDbContext db) : IVisitFeedbackBankRepository
{
    public async Task<IReadOnlyList<VisitFeedbackTemplate>> ListAsync(int schoolId, CancellationToken cancellationToken) =>
        await db.VisitFeedbackTemplates.AsNoTracking()
            .Where(x => x.SchoolId == schoolId && !x.IsDeleted)
            .OrderBy(x => x.Kind).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public Task<bool> HasAnyIncludingDeletedAsync(int schoolId, CancellationToken cancellationToken) =>
        db.VisitFeedbackTemplates.AnyAsync(x => x.SchoolId == schoolId, cancellationToken);

    public Task<VisitFeedbackTemplate?> GetAsync(int schoolId, int id, CancellationToken cancellationToken) =>
        db.VisitFeedbackTemplates.SingleOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId && !x.IsDeleted, cancellationToken);

    public async Task AddRangeAsync(IEnumerable<VisitFeedbackTemplate> items, CancellationToken cancellationToken) =>
        await db.VisitFeedbackTemplates.AddRangeAsync(items, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
