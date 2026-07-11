using AlFalah.Application.DTOs.UserSchoolRoles;

namespace AlFalah.Application.Interfaces;

/// <summary>
/// UserSchoolRole assignment service. Manages which users belong to which schools
/// with which role. A user can be assigned to multiple schools (e.g. a Moderator
/// across two schools).
/// </summary>
public interface IUserSchoolRoleService
{
    /// <summary>Create a new assignment. Rejects duplicates (same User+School+Role triple).</summary>
    Task<UserSchoolRoleDetailDto> CreateAsync(UserSchoolRoleCreateRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Soft-delete the assignment (sets IsActive = false, IsDeleted = true).</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>List assignments, optionally filtered by school.</summary>
    Task<IReadOnlyList<UserSchoolRoleDetailDto>> GetBySchoolAsync(int? schoolId, CancellationToken cancellationToken = default);
}