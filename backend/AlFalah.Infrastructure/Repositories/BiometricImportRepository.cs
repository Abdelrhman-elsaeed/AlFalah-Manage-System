using AlFalah.Application.StudentAffairs.Biometrics;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class BiometricImportRepository : IBiometricImportRepository
{
    private readonly AlFalahDbContext _context;

    public BiometricImportRepository(AlFalahDbContext context) => _context = context;

    public Task<BiometricImportSettingsSnapshot?> GetSettingsAsync(
        int schoolId,
        CancellationToken cancellationToken) =>
        _context.SchoolStudentAffairsSettings
            .AsNoTracking()
            .Where(settings => settings.SchoolId == schoolId)
            .Select(settings => new BiometricImportSettingsSnapshot(
                settings.ArrivalCutoffLocalTime,
                settings.ArrivalGraceMinutes))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<BiometricEnrollmentSnapshot>> GetEnrollmentsAsync(
        int schoolId,
        IReadOnlyCollection<string> nationalIds,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        if (nationalIds.Count == 0) return Array.Empty<BiometricEnrollmentSnapshot>();
        return await _context.StudentEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.SchoolId == schoolId
                && enrollment.Status == StudentEnrollmentStatus.Active
                && enrollment.EnrolledOn <= toDate
                && (enrollment.WithdrawnOn == null || enrollment.WithdrawnOn >= fromDate)
                && enrollment.Student.SchoolId == schoolId
                && enrollment.Student.IsActive
                && enrollment.Student.NationalId != null
                && enrollment.AcademicTerm.SchoolId == schoolId
                && enrollment.AcademicTerm.StartsOn <= toDate
                && enrollment.AcademicTerm.EndsOn >= fromDate)
            .Select(enrollment => new BiometricEnrollmentSnapshot(
                enrollment.StudentId,
                enrollment.Student.NationalId!,
                enrollment.AcademicTermId,
                enrollment.AcademicTerm.StartsOn > enrollment.EnrolledOn
                    ? enrollment.AcademicTerm.StartsOn : enrollment.EnrolledOn,
                enrollment.WithdrawnOn != null && enrollment.WithdrawnOn < enrollment.AcademicTerm.EndsOn
                    ? enrollment.WithdrawnOn.Value : enrollment.AcademicTerm.EndsOn))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlySet<(int StudentId, DateOnly Date)>> GetExistingDelayKeysAsync(
        int schoolId,
        IReadOnlyCollection<int> studentIds,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        if (studentIds.Count == 0) return new HashSet<(int, DateOnly)>();
        var keys = await _context.MorningArrivalDelays
            .AsNoTracking()
            .Where(delay => delay.SchoolId == schoolId
                && studentIds.Contains(delay.StudentId)
                && delay.SchoolLocalDate >= fromDate
                && delay.SchoolLocalDate <= toDate)
            .Select(delay => new { delay.StudentId, delay.SchoolLocalDate })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return keys.Select(key => (key.StudentId, key.SchoolLocalDate)).ToHashSet();
    }

    public void AddRange(IEnumerable<MorningArrivalDelay> delays) =>
        _context.MorningArrivalDelays.AddRange(delays);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        _context.SaveChangesAsync(cancellationToken);
}
