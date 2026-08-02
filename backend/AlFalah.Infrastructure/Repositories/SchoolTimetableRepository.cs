using System.Data;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class SchoolTimetableRepository : ISchoolTimetableRepository
{
    private readonly AlFalahDbContext _context;

    public SchoolTimetableRepository(AlFalahDbContext context) => _context = context;

    public IQueryable<SchoolTimetable> GetAll() => _context.SchoolTimetables.AsNoTracking();

    public IQueryable<SchoolTimetableVersion> GetVersions(int timetableId) =>
        _context.SchoolTimetableVersions.AsNoTracking().Where(x => x.SchoolTimetableId == timetableId);

    public IQueryable<InstructorProfile> GetTeachers(int schoolId) =>
        _context.InstructorProfiles.AsNoTracking()
            .Where(x => x.SchoolId == schoolId && x.IsActive && x.User.IsActive)
            .OrderBy(x => x.User.FirstName).ThenBy(x => x.User.LastName);

    public IQueryable<AcademicYear> GetAcademicYears() =>
        _context.AcademicYears.AsNoTracking().OrderByDescending(x => x.StartsOn);

    public IQueryable<School> GetSchools() => _context.Schools.AsNoTracking();

    public IQueryable<ApplicationUser> GetModerators(int schoolId) =>
        _context.Users.AsNoTracking()
            .Where(user => user.IsActive && user.UserSchoolRoles.Any(assignment =>
                assignment.SchoolId == schoolId
                && assignment.IsActive
                && assignment.Role.Name == RoleNames.Moderator))
            .OrderBy(user => user.FirstName).ThenBy(user => user.LastName);

    public IQueryable<TimetableEditorGrant> GetGrants(int schoolId) =>
        _context.TimetableEditorGrants.AsNoTracking().Where(x => x.SchoolId == schoolId);

    public Task<List<TimetableEditorGrant>> GetTrackedGrantsAsync(
        int schoolId,
        CancellationToken cancellationToken = default) =>
        _context.TimetableEditorGrants.Where(x => x.SchoolId == schoolId).ToListAsync(cancellationToken);

    public Task<SchoolTimetable?> GetTrackedWithEntriesAsync(
        int timetableId,
        CancellationToken cancellationToken = default) =>
        _context.SchoolTimetables
            .Include(x => x.Entries)
            .FirstOrDefaultAsync(x => x.Id == timetableId, cancellationToken);

    public Task<bool> AcademicYearExistsAsync(int academicYearId, CancellationToken cancellationToken = default) =>
        _context.AcademicYears.AnyAsync(x => x.Id == academicYearId, cancellationToken);

    public async Task AddAsync(SchoolTimetable timetable, CancellationToken cancellationToken = default) =>
        await _context.SchoolTimetables.AddAsync(timetable, cancellationToken);

    public async Task AddVersionAsync(SchoolTimetableVersion version, CancellationToken cancellationToken = default) =>
        await _context.SchoolTimetableVersions.AddAsync(version, cancellationToken);

    public async Task AddEntriesAsync(IEnumerable<SchoolTimetableEntry> entries, CancellationToken cancellationToken = default) =>
        await _context.SchoolTimetableEntries.AddRangeAsync(entries, cancellationToken);

    public async Task AddGrantsAsync(IEnumerable<TimetableEditorGrant> grants, CancellationToken cancellationToken = default) =>
        await _context.TimetableEditorGrants.AddRangeAsync(grants, cancellationToken);

    public void SoftDeleteEntries(IEnumerable<SchoolTimetableEntry> entries, DateTimeOffset deletedAt)
    {
        foreach (var entry in entries)
        {
            entry.IsDeleted = true;
            entry.DeletedAt = deletedAt;
        }
    }

    public void SoftDeleteGrants(IEnumerable<TimetableEditorGrant> grants, string userId, DateTimeOffset deletedAt)
    {
        foreach (var grant in grants)
        {
            grant.IsDeleted = true;
            grant.DeletedAt = deletedAt;
            grant.DeletedByUserId = userId;
        }
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        if (_context.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true)
        {
            await action(cancellationToken);
            return;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await action(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
