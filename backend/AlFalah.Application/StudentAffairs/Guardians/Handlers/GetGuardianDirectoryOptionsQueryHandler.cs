using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Guardian;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Guardians.Handlers;

public sealed class GetGuardianDirectoryOptionsQueryHandler
    : IRequestHandler<GetGuardianDirectoryOptionsQuery, ApiResponse<IReadOnlyList<GuardianDirectoryOptionDto>>>
{
    private readonly IGuardianDirectoryRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetGuardianDirectoryOptionsQueryHandler(
        IGuardianDirectoryRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<IReadOnlyList<GuardianDirectoryOptionDto>>> Handle(
        GetGuardianDirectoryOptionsQuery query,
        CancellationToken cancellationToken)
    {
        if (_currentUser.ActiveSchoolId is not int schoolId || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return ApiResponse<IReadOnlyList<GuardianDirectoryOptionDto>>.Fail("Authentication is required");

        if (!_currentUser.HasPermission(PermissionNames.GuardianLinkStudent)
            && !(_currentUser.IsInRole(RoleNames.Secretary)
                && _currentUser.HasPermission(PermissionNames.StudentManage)))
            return ApiResponse<IReadOnlyList<GuardianDirectoryOptionDto>>.Fail("Permission denied");

        var options = await _repository.GetActiveOptionsAsync(schoolId, cancellationToken).ConfigureAwait(false);
        return ApiResponse<IReadOnlyList<GuardianDirectoryOptionDto>>.Success(options);
    }
}
