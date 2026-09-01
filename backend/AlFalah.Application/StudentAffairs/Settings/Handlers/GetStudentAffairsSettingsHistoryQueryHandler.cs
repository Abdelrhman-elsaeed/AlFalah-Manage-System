using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Settings;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Settings.Handlers;

public sealed class GetStudentAffairsSettingsHistoryQueryHandler
    : IRequestHandler<GetStudentAffairsSettingsHistoryQuery, ApiResponse<PagedResult<StudentAffairsSettingsHistoryDto>>>
{
    private readonly IStudentAffairsSettingsRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetStudentAffairsSettingsHistoryQueryHandler(
        IStudentAffairsSettingsRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<PagedResult<StudentAffairsSettingsHistoryDto>>> Handle(
        GetStudentAffairsSettingsHistoryQuery query,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        if (!_currentUser.IsAuthenticated || schoolId is null)
        {
            return ApiResponse<PagedResult<StudentAffairsSettingsHistoryDto>>.Fail(SettingsHandlerSupport.AuthenticationRequired);
        }

        if (!_currentUser.HasPermission(PermissionNames.StudentAffairsSettingsView))
        {
            return ApiResponse<PagedResult<StudentAffairsSettingsHistoryDto>>.Fail(SettingsHandlerSupport.PermissionDenied);
        }

        var result = await _repository
            .GetHistoryAsync(schoolId.Value, query.Query, cancellationToken)
            .ConfigureAwait(false);

        return ApiResponse<PagedResult<StudentAffairsSettingsHistoryDto>>.Success(result);
    }
}
