using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Attendance;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Attendance.Handlers;

public sealed class GetStudentAttendanceRecordsQueryHandler
    : IRequestHandler<GetStudentAttendanceRecordsQuery, ApiResponse<PagedResult<StudentAttendanceRecordDto>>>
{
    private readonly IAttendanceWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetStudentAttendanceRecordsQueryHandler(
        IAttendanceWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<PagedResult<StudentAttendanceRecordDto>>> Handle(
        GetStudentAttendanceRecordsQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<PagedResult<StudentAttendanceRecordDto>>.Fail(AttendanceHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.AttendanceViewStudents))
            return ApiResponse<PagedResult<StudentAttendanceRecordDto>>.Fail(AttendanceHandlerSupport.PermissionDenied);

        var result = await _repository.GetAttendanceRecordsAsync(
            schoolId.Value,
            request.Query,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<PagedResult<StudentAttendanceRecordDto>>.Success(result);
    }
}
