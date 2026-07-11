using AlFalah.Application.DTOs.Users;
using AlFalah.Shared.Models;

namespace AlFalah.Application.Interfaces;

/// <summary>
/// User CRUD service. Phase 2 scope: create / update users with role
/// SchoolManager | Moderator | Instructor. MainManager and SuperAdmin are out of
/// scope (seeded/managed by the platform owner separately).
/// </summary>
public interface IUserService
{
    Task<PagedResult<UserListItemDto>> GetPagedAsync(UserListQuery query, CancellationToken cancellationToken = default);

    Task<UserDetailDto> GetByIdAsync(string userId, CancellationToken cancellationToken = default);

    Task<UserDetailDto> CreateAsync(UserCreateRequestDto request, CancellationToken cancellationToken = default);

    Task<UserDetailDto> UpdateAsync(string userId, UserUpdateRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Soft-deactivates a user (sets IsActive = false).</summary>
    Task DeactivateAsync(string userId, CancellationToken cancellationToken = default);
}