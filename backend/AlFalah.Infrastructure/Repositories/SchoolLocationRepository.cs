using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class SchoolLocationRepository : ISchoolLocationRepository
{
    private readonly AlFalahDbContext _context;

    public SchoolLocationRepository(AlFalahDbContext context) => _context = context;

    public IQueryable<SchoolLocation> Query() => _context.SchoolLocations.AsNoTracking();

    public Task<SchoolLocation?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.SchoolLocations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.IsActive, cancellationToken);

    public Task AddAsync(SchoolLocation location, CancellationToken cancellationToken = default) =>
        _context.SchoolLocations.AddAsync(location, cancellationToken).AsTask();

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _context.SaveChangesAsync(cancellationToken);
}
