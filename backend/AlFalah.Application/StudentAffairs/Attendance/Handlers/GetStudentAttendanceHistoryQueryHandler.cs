using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Attendance;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Attendance.Handlers;

public sealed class GetStudentAttendanceHistoryQueryHandler
    : IRequestHandler<GetStudentAttendanceHistoryQuery, ApiResponse<StudentAttendanceHistoryDto>>
{
    private readonly IAttendanceWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetStudentAttendanceHistoryQueryHandler(
        IAttendanceWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<StudentAttendanceHistoryDto>> Handle(
        GetStudentAttendanceHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<StudentAttendanceHistoryDto>.Fail(AttendanceHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.AttendanceViewStudents)
            && !_currentUser.HasPermission(PermissionNames.GuardianViewLinkedStudents))
            return ApiResponse<StudentAttendanceHistoryDto>.Fail(AttendanceHandlerSupport.PermissionDenied);

        if (request.StudentId <= 0)
            return ApiResponse<StudentAttendanceHistoryDto>.Fail("A valid student ID is required");

        var history = await _repository.GetStudentAttendanceHistoryAsync(
            schoolId.Value,
            request.StudentId,
            request.AcademicTermId,
            cancellationToken).ConfigureAwait(false);

        if (history is null)
            return ApiResponse<StudentAttendanceHistoryDto>.Fail("Student attendance history was not found");

        return ApiResponse<StudentAttendanceHistoryDto>.Success(history);
    }
}
