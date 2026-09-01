using AlFalah.Application.StudentAffairs.DTOs.Summons;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Shared.Models;

namespace AlFalah.Application.StudentAffairs.Summons;

public sealed record SummonEnrollmentSnapshot(int AcademicTermId);

public interface ISummonWorkflowRepository
{
    Task<PagedResult<SummonDto>> GetSummonsAsync(
        int schoolId,
        SummonListQuery query,
        CancellationToken cancellationToken);

    Task<PagedResult<SummonDto>> GetMySummonsAsync(
        int schoolId,
        string guardianUserId,
        SummonListQuery query,
        CancellationToken cancellationToken);

    Task<SummonDto?> GetDtoAsync(
        int schoolId,
        int summonId,
        CancellationToken cancellationToken);

    Task<SummonHistoryDto?> GetHistoryAsync(
        int schoolId,
        int summonId,
        CancellationToken cancellationToken);

    Task<GuardianSummon?> GetForUpdateAsync(
        int schoolId,
        int summonId,
        CancellationToken cancellationToken);

    Task<bool> IsGuardianLinkActiveAsync(
        int schoolId,
        int guardianProfileId,
        int studentId,
        DateOnly onDate,
        CancellationToken cancellationToken);

    Task<bool> IsAssignedToAsync(
        int schoolId,
        int summonId,
        string socialWorkerUserId,
        CancellationToken cancellationToken);

    Task<SummonEnrollmentSnapshot?> GetActiveEnrollmentAsync(
        int schoolId,
        int studentId,
        DateOnly onDate,
        CancellationToken cancellationToken);

    void Add(GuardianSummon summon);
    void SetExpectedRowVersion(GuardianSummon summon, byte[] rowVersion);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
