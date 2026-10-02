using AlFalah.Application.StudentAffairs.DTOs.Guardian;
using AlFalah.Application.StudentAffairs.Guardians;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class GuardianDirectoryRepository : IGuardianDirectoryRepository
{
    private readonly AlFalahDbContext _context;

    public GuardianDirectoryRepository(AlFalahDbContext context) => _context = context;

    public async Task<IReadOnlyList<GuardianDirectoryOptionDto>> GetActiveOptionsAsync(
        int schoolId,
        CancellationToken cancellationToken) =>
        await _context.GuardianProfiles
            .AsNoTracking()
            .Where(profile => profile.SchoolId == schoolId
                && profile.IsActive
                && !profile.IsDeleted
                && profile.ApplicationUser.IsActive
                && !profile.ApplicationUser.IsDeleted)
            .OrderBy(profile => profile.ApplicationUser.FirstName)
            .ThenBy(profile => profile.ApplicationUser.LastName)
            .Select(profile => new GuardianDirectoryOptionDto(
                profile.Id,
                (profile.ApplicationUser.FirstName + " " + profile.ApplicationUser.LastName).Trim(),
                profile.ApplicationUser.UserName ?? string.Empty,
                profile.ApplicationUser.PhoneNumber))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<bool> IsActiveAsync(
        int schoolId,
        int guardianProfileId,
        CancellationToken cancellationToken) =>
        _context.GuardianProfiles.AsNoTracking().AnyAsync(
            profile => profile.SchoolId == schoolId
                && profile.Id == guardianProfileId
                && profile.IsActive
                && !profile.IsDeleted
                && profile.ApplicationUser.IsActive
                && !profile.ApplicationUser.IsDeleted,
            cancellationToken);
}
