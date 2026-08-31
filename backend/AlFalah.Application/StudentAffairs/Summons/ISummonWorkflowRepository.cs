using AlFalah.Application.StudentAffairs.DTOs.Summons;
using AlFalah.Domain.Entities.StudentAffairs;

namespace AlFalah.Application.StudentAffairs.Summons;

public interface ISummonWorkflowRepository
{
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

    void SetExpectedRowVersion(GuardianSummon summon, byte[] rowVersion);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    Task<SummonDto?> GetDtoAsync(int schoolId, int summonId, CancellationToken cancellationToken);
}
