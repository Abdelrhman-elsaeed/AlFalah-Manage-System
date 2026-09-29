using AlFalah.Application.Interfaces;
using AlFalah.Application.IntelligentTimetable;
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
    private readonly ISchoolLocalDateResolver _localDateResolver;
    private readonly TimeProvider _timeProvider;

    public GetStudentAttendanceHistoryQueryHandler(
        IAttendanceWorkflowRepository repository,
        ICurrentUserService currentUser,
        ISchoolLocalDateResolver localDateResolver,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _localDateResolver = localDateResolver;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<StudentAttendanceHistoryDto>> Handle(
        GetStudentAttendanceHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<StudentAttendanceHistoryDto>.Fail(AttendanceHandlerSupport.AuthenticationRequired);

        var isGuardian = _currentUser.IsInRole(RoleNames.Guardian);
        if (isGuardian
            ? !_currentUser.HasPermission(PermissionNames.GuardianViewLinkedStudents)
            : !_currentUser.HasPermission(PermissionNames.AttendanceViewStudents))
            return ApiResponse<StudentAttendanceHistoryDto>.Fail(AttendanceHandlerSupport.PermissionDenied);

        if (request.StudentId <= 0)
            return ApiResponse<StudentAttendanceHistoryDto>.Fail("A valid student ID is required");

        if (isGuardian)
        {
            var localDate = await _localDateResolver.ResolveAsync(
                schoolId.Value,
                _timeProvider.GetUtcNow(),
                cancellationToken).ConfigureAwait(false);
            if (localDate is null)
                return ApiResponse<StudentAttendanceHistoryDto>.Fail("School local date could not be resolved");
            var link = await _repository.GetGuardianExcuseLinkAsync(
                schoolId.Value,
                userId,
                request.StudentId,
                localDate.Value,
                cancellationToken).ConfigureAwait(false);
            if (link is null || !link.GuardianIsActive || !link.StudentIsActive)
                return ApiResponse<StudentAttendanceHistoryDto>.Fail("Student attendance history was not found");
        }

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
