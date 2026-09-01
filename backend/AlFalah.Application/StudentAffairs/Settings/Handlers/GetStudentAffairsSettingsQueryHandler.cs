using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Settings;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Settings.Handlers;

public sealed class GetStudentAffairsSettingsQueryHandler
    : IRequestHandler<GetStudentAffairsSettingsQuery, ApiResponse<SchoolStudentAffairsSettingsDto>>
{
    private readonly IStudentAffairsSettingsRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public GetStudentAffairsSettingsQueryHandler(
        IStudentAffairsSettingsRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<SchoolStudentAffairsSettingsDto>> Handle(
        GetStudentAffairsSettingsQuery query,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        if (!_currentUser.IsAuthenticated || schoolId is null)
        {
            return ApiResponse<SchoolStudentAffairsSettingsDto>.Fail(SettingsHandlerSupport.AuthenticationRequired);
        }

        if (!_currentUser.HasPermission(PermissionNames.StudentAffairsSettingsView))
        {
            return ApiResponse<SchoolStudentAffairsSettingsDto>.Fail(SettingsHandlerSupport.PermissionDenied);
        }

        var settings = await _repository
            .GetSettingsDtoAsync(schoolId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (settings is null)
        {
            var defaultDto = SettingsHandlerSupport.CreateDefaultBaseline(_timeProvider.GetUtcNow());
            return ApiResponse<SchoolStudentAffairsSettingsDto>.Success(defaultDto);
        }

        return ApiResponse<SchoolStudentAffairsSettingsDto>.Success(settings);
    }
}
