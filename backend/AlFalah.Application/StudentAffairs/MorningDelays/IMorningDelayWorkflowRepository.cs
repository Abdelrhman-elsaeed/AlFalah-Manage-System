using AlFalah.Application.StudentAffairs.DTOs.Delays;
using AlFalah.Domain.Entities.StudentAffairs;

namespace AlFalah.Application.StudentAffairs.MorningDelays;

public sealed record MorningDelayEnrollmentSnapshot(int AcademicTermId);

public interface IMorningDelayWorkflowRepository
{
    Task<MorningDelayEnrollmentSnapshot?> GetActiveEnrollmentAsync(
        int schoolId,
        int studentId,
        DateOnly onDate,
        CancellationToken cancellationToken);

    Task<MorningDelayDto?> GetExistingAsync(
        int schoolId,
        int studentId,
        DateOnly schoolLocalDate,
        CancellationToken cancellationToken);

    void Add(MorningArrivalDelay delay);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    Task<MorningDelayDto?> GetDtoAsync(int schoolId, int delayId, CancellationToken cancellationToken);
}
