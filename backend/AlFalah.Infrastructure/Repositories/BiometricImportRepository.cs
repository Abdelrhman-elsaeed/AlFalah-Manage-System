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
            .Where(settings => settings.SchoolId == schoolId && !settings.IsDeleted)
            .Select(settings => new BiometricImportSettingsSnapshot(
                settings.ArrivalCutoffLocalTime,
                settings.ArrivalGraceMinutes,
                _context.SchoolTimetables
                    .Where(timetable => timetable.SchoolId == schoolId
                        && timetable.IsPublished
                        && timetable.BellScheduleRevision != null)
                    .OrderByDescending(timetable => timetable.PublishedAt)
                    .Select(timetable => timetable.BellScheduleRevision!.SchoolTimeZoneId)
                    .FirstOrDefault() ?? string.Empty))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<BiometricEnrollmentSnapshot>> GetEnrollmentsAsync(
        int schoolId,
        IReadOnlyCollection<string> identityNumbers,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        if (identityNumbers.Count == 0) return Array.Empty<BiometricEnrollmentSnapshot>();
        return await _context.StudentEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.SchoolId == schoolId
                && enrollment.Status == StudentEnrollmentStatus.Active
                && !enrollment.IsDeleted
                && enrollment.EnrolledOn <= toDate
                && (enrollment.WithdrawnOn == null || enrollment.WithdrawnOn >= fromDate)
                && enrollment.Student.SchoolId == schoolId
                && enrollment.Student.IsActive
                && !enrollment.Student.IsDeleted
                && (!string.IsNullOrEmpty(enrollment.Student.IdentityNumber) && identityNumbers.Contains(enrollment.Student.IdentityNumber)
                    || (enrollment.Student.NationalId != null && identityNumbers.Contains(enrollment.Student.NationalId)))
                && enrollment.AcademicTerm.SchoolId == schoolId
                && enrollment.AcademicTerm.IsActive
                && !enrollment.AcademicTerm.IsDeleted
                && enrollment.AcademicTerm.StartsOn <= toDate
                && enrollment.AcademicTerm.EndsOn >= fromDate
                && enrollment.Classroom.SchoolId == schoolId
                && enrollment.Classroom.IsActive
                && !enrollment.Classroom.IsDeleted
                && enrollment.Classroom.AcademicYearId == enrollment.AcademicTerm.AcademicYearId)
            .Select(enrollment => new BiometricEnrollmentSnapshot(
                enrollment.StudentId,
                !string.IsNullOrEmpty(enrollment.Student.IdentityNumber) ? enrollment.Student.IdentityNumber : enrollment.Student.NationalId!,
                enrollment.AcademicTermId,
                enrollment.AcademicTerm.StartsOn > enrollment.EnrolledOn
                    ? enrollment.AcademicTerm.StartsOn : enrollment.EnrolledOn,
                enrollment.WithdrawnOn != null && enrollment.WithdrawnOn < enrollment.AcademicTerm.EndsOn
                    ? enrollment.WithdrawnOn.Value : enrollment.AcademicTerm.EndsOn))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Dictionary<(int StudentId, DateOnly Date), MorningArrivalDelay>> GetExistingDelaysForUpdateAsync(
        int schoolId,
        IReadOnlyCollection<int> studentIds,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        if (studentIds.Count == 0) return new();
        var delays = await _context.MorningArrivalDelays
            .AsTracking()
            .Where(delay => delay.SchoolId == schoolId
                && studentIds.Contains(delay.StudentId)
                && delay.SchoolLocalDate >= fromDate
                && delay.SchoolLocalDate <= toDate
                && !delay.IsDeleted)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return delays
            .GroupBy(d => (d.StudentId, d.SchoolLocalDate))
            .ToDictionary(g => g.Key, g => g.First());
    }

    public void AddRange(IEnumerable<MorningArrivalDelay> delays) =>
        _context.MorningArrivalDelays.AddRange(delays);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        _context.SaveChangesAsync(cancellationToken);
}
