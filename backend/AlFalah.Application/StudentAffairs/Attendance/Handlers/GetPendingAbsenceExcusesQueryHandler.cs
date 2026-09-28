using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Attendance;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Attendance.Handlers;

public sealed class GetPendingAbsenceExcusesQueryHandler
    : IRequestHandler<GetPendingAbsenceExcusesQuery, ApiResponse<PagedResult<OfficerAbsenceExcuseQueueItemDto>>>
{
    private readonly IAttendanceWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetPendingAbsenceExcusesQueryHandler(
        IAttendanceWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<PagedResult<OfficerAbsenceExcuseQueueItemDto>>> Handle(
        GetPendingAbsenceExcusesQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        if (!_currentUser.IsAuthenticated || schoolId is null || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return ApiResponse<PagedResult<OfficerAbsenceExcuseQueueItemDto>>.Fail(AttendanceHandlerSupport.AuthenticationRequired);

        if (!_currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            || !_currentUser.HasPermission(PermissionNames.AttendanceViewStudents)
            || !_currentUser.HasPermission(PermissionNames.AttendanceReviewExcuse))
        {
            return ApiResponse<PagedResult<OfficerAbsenceExcuseQueueItemDto>>.Fail(AttendanceHandlerSupport.PermissionDenied);
        }

        var result = await _repository.GetPendingExcusesAsync(
            schoolId.Value,
            request.Query,
            cancellationToken).ConfigureAwait(false);
        return ApiResponse<PagedResult<OfficerAbsenceExcuseQueueItemDto>>.Success(result);
    }
}
