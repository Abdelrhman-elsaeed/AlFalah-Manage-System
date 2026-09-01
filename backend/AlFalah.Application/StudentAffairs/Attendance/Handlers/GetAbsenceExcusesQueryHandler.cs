using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Attendance;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Attendance.Handlers;

public sealed class GetAbsenceExcusesQueryHandler
    : IRequestHandler<GetAbsenceExcusesQuery, ApiResponse<IReadOnlyList<AbsenceExcuseDto>>>
{
    private readonly IAttendanceWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetAbsenceExcusesQueryHandler(
        IAttendanceWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<IReadOnlyList<AbsenceExcuseDto>>> Handle(
        GetAbsenceExcusesQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<IReadOnlyList<AbsenceExcuseDto>>.Fail(AttendanceHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.AttendanceViewStudents)
            && !_currentUser.HasPermission(PermissionNames.AttendanceSubmitExcuse))
            return ApiResponse<IReadOnlyList<AbsenceExcuseDto>>.Fail(AttendanceHandlerSupport.PermissionDenied);

        var excuses = await _repository.GetExcusesByAttendanceIdAsync(
            schoolId.Value,
            request.AttendanceId,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<IReadOnlyList<AbsenceExcuseDto>>.Success(excuses);
    }
}
