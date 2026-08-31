using AlFalah.Application.StudentAffairs.DTOs.Delays;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Application.StudentAffairs.MorningDelays;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class MorningDelayWorkflowRepository : IMorningDelayWorkflowRepository
{
    private readonly AlFalahDbContext _context;

    public MorningDelayWorkflowRepository(AlFalahDbContext context) => _context = context;

    public Task<MorningDelayEnrollmentSnapshot?> GetActiveEnrollmentAsync(
        int schoolId,
        int studentId,
        DateOnly onDate,
        CancellationToken cancellationToken) =>
        _context.StudentEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.SchoolId == schoolId
                && enrollment.StudentId == studentId
                && enrollment.Status == StudentEnrollmentStatus.Active
                && enrollment.EnrolledOn <= onDate
                && (enrollment.WithdrawnOn == null || enrollment.WithdrawnOn >= onDate)
                && enrollment.Student.SchoolId == schoolId
                && enrollment.Student.IsActive
                && enrollment.AcademicTerm.SchoolId == schoolId
                && enrollment.AcademicTerm.IsActive
                && enrollment.AcademicTerm.StartsOn <= onDate
                && enrollment.AcademicTerm.EndsOn >= onDate)
            .Select(enrollment => new MorningDelayEnrollmentSnapshot(enrollment.AcademicTermId))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<MorningDelayDto?> GetExistingAsync(
        int schoolId,
        int studentId,
        DateOnly schoolLocalDate,
        CancellationToken cancellationToken)
    {
        var delayId = await _context.MorningArrivalDelays
            .AsNoTracking()
            .Where(delay => delay.SchoolId == schoolId
                && delay.StudentId == studentId
                && delay.SchoolLocalDate == schoolLocalDate)
            .Select(delay => (int?)delay.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return delayId is null
            ? null
            : await GetDtoAsync(schoolId, delayId.Value, cancellationToken).ConfigureAwait(false);
    }

    public void Add(MorningArrivalDelay delay) => _context.MorningArrivalDelays.Add(delay);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        _context.SaveChangesAsync(cancellationToken);

    public async Task<MorningDelayDto?> GetDtoAsync(
        int schoolId,
        int delayId,
        CancellationToken cancellationToken)
    {
        var projection = await _context.MorningArrivalDelays
            .AsNoTracking()
            .Where(delay => delay.Id == delayId && delay.SchoolId == schoolId)
            .Select(delay => new
            {
                delay.Id,
                delay.StudentId,
                delay.Student.StudentNumber,
                StudentDisplayName = (delay.Student.FirstName + " "
                    + (delay.Student.MiddleName ?? string.Empty) + " "
                    + delay.Student.LastName).Trim(),
                delay.Student.IsActive,
                Enrollment = delay.Student.Enrollments
                    .Where(enrollment => enrollment.SchoolId == schoolId
                        && enrollment.AcademicTermId == delay.AcademicTermId
                        && enrollment.Status == StudentEnrollmentStatus.Active)
                    .Select(enrollment => new
                    {
                        enrollment.ClassroomId,
                        enrollment.Classroom.ClassLabel
                    })
                    .FirstOrDefault(),
                delay.AcademicTermId,
                delay.ArrivalAt,
                delay.CutoffTimeSnapshot,
                delay.DelayMinutes,
                delay.Reason
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (projection is null) return null;

        var metric = await _context.StudentTermMetrics
            .AsNoTracking()
            .Where(item => item.SchoolId == schoolId
                && item.StudentId == projection.StudentId
                && item.AcademicTermId == projection.AcademicTermId
                && item.MetricCode == StudentTermMetricCode.MorningArrivalDelay)
            .Select(item => new { item.Count, item.RecalculatedAt })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var offset = projection.ArrivalAt.Offset;
        var timezone = offset == TimeSpan.Zero ? "UTC" : $"UTC{offset:+hh\\:mm;-hh\\:mm}";

        return new MorningDelayDto(
            projection.Id,
            new StudentSummaryDto(
                projection.StudentId,
                projection.StudentNumber,
                projection.StudentDisplayName,
                projection.Enrollment?.ClassroomId,
                projection.Enrollment?.ClassLabel,
                projection.IsActive,
                null),
            projection.ArrivalAt,
            projection.ArrivalAt.ToString("HH:mm"),
            timezone,
            projection.CutoffTimeSnapshot,
            projection.DelayMinutes,
            projection.Reason,
            new MetricBadgeDto(
                StudentTermMetricCode.MorningArrivalDelay,
                metric?.Count ?? 0,
                0,
                null,
                "None",
                projection.ArrivalAt,
                metric?.RecalculatedAt ?? projection.ArrivalAt),
            null,
            string.Empty);
    }
}
