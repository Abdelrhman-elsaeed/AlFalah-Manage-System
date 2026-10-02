using AlFalah.Application.StudentAffairs.DTOs.Guardian;

namespace AlFalah.Application.StudentAffairs.Guardians;

public interface IGuardianDirectoryRepository
{
    Task<IReadOnlyList<GuardianDirectoryOptionDto>> GetActiveOptionsAsync(
        int schoolId,
        CancellationToken cancellationToken);

    Task<bool> IsActiveAsync(
        int schoolId,
        int guardianProfileId,
        CancellationToken cancellationToken);
}
