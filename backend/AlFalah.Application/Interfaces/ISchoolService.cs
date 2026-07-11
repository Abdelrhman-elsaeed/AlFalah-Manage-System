using AlFalah.Application.DTOs.Schools;
using AlFalah.Shared.Models;

namespace AlFalah.Application.Interfaces;

/// <summary>
/// School CRUD + lifecycle service. All methods enforce SchoolId scoping where relevant
/// and return DTOs (never domain entities).
/// </summary>
public interface ISchoolService
{
    Task<PagedResult<SchoolListItemDto>> GetPagedAsync(SchoolListQuery query, CancellationToken cancellationToken = default);

    Task<SchoolDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<SchoolDetailDto> CreateAsync(SchoolCreateRequestDto request, CancellationToken cancellationToken = default);

    Task<SchoolDetailDto> UpdateAsync(int id, SchoolUpdateRequestDto request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assign (or replace) the School Manager. If the school already has an active
    /// SchoolManager user-school-role, that role is deactivated so this school has
    /// EXACTLY ONE active SchoolManager.
    /// </summary>
    Task<SchoolDetailDto> AssignManagerAsync(int schoolId, AssignSchoolManagerRequestDto request, CancellationToken cancellationToken = default);

    Task ActivateAsync(int id, CancellationToken cancellationToken = default);

    Task DeactivateAsync(int id, CancellationToken cancellationToken = default);
}