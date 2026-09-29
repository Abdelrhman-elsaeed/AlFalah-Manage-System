using System;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.Interfaces;
using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.StudentAffairs.DTOs.Guardian;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Application.StudentAffairs.Students;
using AlFalah.Application.StudentAffairs.Students.Handlers;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Guardians.Handlers;

public sealed class GetGuardianStudentNotificationsQueryHandler
    : IRequestHandler<GetGuardianStudentNotificationsQuery, ApiResponse<PagedResult<GuardianNotificationDto>>>
{
    private readonly IStudentWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly ISchoolLocalDateResolver _localDateResolver;
    private readonly TimeProvider _timeProvider;

    public GetGuardianStudentNotificationsQueryHandler(
        IStudentWorkflowRepository repository,
        ICurrentUserService currentUser,
        ISchoolLocalDateResolver localDateResolver,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _localDateResolver = localDateResolver;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<PagedResult<GuardianNotificationDto>>> Handle(
        GetGuardianStudentNotificationsQuery query,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<PagedResult<GuardianNotificationDto>>.Fail(StudentHandlerSupport.AuthenticationRequired);

        if (!_currentUser.IsInRole(RoleNames.Guardian)
            || !_currentUser.HasPermission(PermissionNames.NotificationViewOwn))
        {
            return ApiResponse<PagedResult<GuardianNotificationDto>>.Fail(StudentHandlerSupport.PermissionDenied);
        }

        if (!await _repository.IsActiveGuardianProfileAsync(schoolId.Value, userId, cancellationToken)
                .ConfigureAwait(false))
            return ApiResponse<PagedResult<GuardianNotificationDto>>.Fail(StudentHandlerSupport.PermissionDenied);

        var localDate = await _localDateResolver.ResolveAsync(
            schoolId.Value,
            _timeProvider.GetUtcNow(),
            cancellationToken).ConfigureAwait(false);
        if (localDate is null)
            return ApiResponse<PagedResult<GuardianNotificationDto>>.Fail("School local date could not be resolved");

        var result = await _repository.GetGuardianStudentNotificationsAsync(
            schoolId.Value,
            userId,
            query.StudentId,
            localDate.Value,
            query.Query,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<PagedResult<GuardianNotificationDto>>.Success(result);
    }
}
