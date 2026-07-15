using AlFalah.Application.DTOs.Schools;

namespace AlFalah.Application.Interfaces;

public interface ISchoolLocationService
{
    Task<IReadOnlyList<SchoolLocationDto>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<SchoolLocationDto> CreateAsync(SchoolLocationCreateRequestDto request, CancellationToken cancellationToken = default);
}
