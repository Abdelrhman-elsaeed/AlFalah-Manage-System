using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Attendance;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Attendance.Handlers;

public sealed class GetStudentAttendanceSheetQueryHandler
    : IRequestHandler<GetStudentAttendanceSheetQuery, ApiResponse<StudentAttendanceSheetDto>>
{
    private readonly IAttendanceWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetStudentAttendanceSheetQueryHandler(
        IAttendanceWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<StudentAttendanceSheetDto>> Handle(
        GetStudentAttendanceSheetQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<StudentAttendanceSheetDto>.Fail(AttendanceHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.AttendanceViewStudents)
            && !_currentUser.HasPermission(PermissionNames.AttendanceManageStudents))
            return ApiResponse<StudentAttendanceSheetDto>.Fail(AttendanceHandlerSupport.PermissionDenied);

        if (request.ClassroomId <= 0)
            return ApiResponse<StudentAttendanceSheetDto>.Fail("A valid classroom ID is required");

        var sheet = await _repository.GetAttendanceSheetDtoAsync(
            schoolId.Value,
            request.ClassroomId,
            request.Date,
            Guid.NewGuid().ToString("N"),
            cancellationToken).ConfigureAwait(false);

        if (sheet is null)
            return ApiResponse<StudentAttendanceSheetDto>.Fail("Classroom was not found");

        return ApiResponse<StudentAttendanceSheetDto>.Success(sheet);
    }
}
