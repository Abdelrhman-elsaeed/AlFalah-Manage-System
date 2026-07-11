using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// School lookup service — used by the login page to populate the school selector.
/// </summary>
public class SchoolLookupService
{
    private readonly AlFalahDbContext _context;

    public SchoolLookupService(AlFalahDbContext context)
    {
        _context = context;
    }

    /// <summary>Returns active schools for display in login dropdown.</summary>
    public async Task<List<(int Id, string Name, string City, SchoolStage Stage, string? LogoUrl)>> GetActiveSchoolsAsync()
    {
        return await _context.Schools
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new ValueTuple<int, string, string, SchoolStage, string?>(
                s.Id, s.Name, s.City, s.Stage, s.LogoUrl))
            .ToListAsync();
    }
}
