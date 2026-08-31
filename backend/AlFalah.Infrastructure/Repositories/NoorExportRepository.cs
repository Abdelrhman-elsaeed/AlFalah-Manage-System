using AlFalah.Application.StudentAffairs.Attendance;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class NoorExportRepository : INoorExportRepository
{
    private readonly AlFalahDbContext _context;

    public NoorExportRepository(AlFalahDbContext context) => _context = context;

    public Task<NoorAbsenceCorrectionBatch?> GetBatchAsync(
        int schoolId,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        _context.NoorAbsenceCorrectionBatches
            .AsNoTracking()
            .Include(batch => batch.Items)
            .Where(batch => batch.SchoolId == schoolId && batch.IdempotencyKey == idempotencyKey)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<NoorAcceptedExcuseSnapshot>> GetAcceptedExcusesAsync(
        int schoolId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken) =>
        await _context.DailyStudentAttendances
            .AsNoTracking()
            .Where(attendance => attendance.SchoolId == schoolId
                && attendance.AttendanceDate >= fromDate
                && attendance.AttendanceDate <= toDate
                && attendance.Status == StudentAttendanceStatus.AbsentExcused
                && attendance.ExcuseStatus == AbsenceExcuseStatus.Accepted)
            .OrderBy(attendance => attendance.AttendanceDate)
            .ThenBy(attendance => attendance.Student.StudentNumber)
            .Select(attendance => new NoorAcceptedExcuseSnapshot(
                attendance.Id,
                attendance.StudentId,
                (attendance.Student.FirstName + " " + (attendance.Student.MiddleName ?? string.Empty)
                    + " " + attendance.Student.LastName).Trim(),
                attendance.Student.NationalId,
                attendance.AttendanceDate,
                attendance.ExcuseStatus!.Value))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(NoorAbsenceCorrectionBatch batch) =>
        _context.NoorAbsenceCorrectionBatches.Add(batch);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        _context.SaveChangesAsync(cancellationToken);
}
