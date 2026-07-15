using AlFalah.Domain.Entities;

namespace AlFalah.Application.Interfaces;

public interface ISchoolLocationRepository
{
    IQueryable<SchoolLocation> Query();
    Task<SchoolLocation?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(SchoolLocation location, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
