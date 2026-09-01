using System;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.Interfaces;
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

    public GetGuardianStudentNotificationsQueryHandler(
        IStudentWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<PagedResult<GuardianNotificationDto>>> Handle(
        GetGuardianStudentNotificationsQuery query,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<PagedResult<GuardianNotificationDto>>.Fail(StudentHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.NotificationViewOwn)
            && !_currentUser.IsInRole(RoleNames.Guardian))
        {
            return ApiResponse<PagedResult<GuardianNotificationDto>>.Fail(StudentHandlerSupport.PermissionDenied);
        }

        var result = await _repository.GetGuardianStudentNotificationsAsync(
            schoolId.Value,
            userId,
            query.StudentId,
            query.Query,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<PagedResult<GuardianNotificationDto>>.Success(result);
    }
}
