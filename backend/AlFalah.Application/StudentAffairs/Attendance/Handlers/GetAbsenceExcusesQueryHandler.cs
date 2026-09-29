using AlFalah.Application.Interfaces;
using AlFalah.Application.IntelligentTimetable;
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
    private readonly ISchoolLocalDateResolver _localDateResolver;
    private readonly TimeProvider _timeProvider;

    public GetAbsenceExcusesQueryHandler(
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

    public async Task<ApiResponse<IReadOnlyList<AbsenceExcuseDto>>> Handle(
        GetAbsenceExcusesQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<IReadOnlyList<AbsenceExcuseDto>>.Fail(AttendanceHandlerSupport.AuthenticationRequired);

        var isGuardian = _currentUser.IsInRole(RoleNames.Guardian);
        if (isGuardian
            ? !_currentUser.HasPermission(PermissionNames.AttendanceSubmitExcuse)
            : !_currentUser.HasPermission(PermissionNames.AttendanceViewStudents))
            return ApiResponse<IReadOnlyList<AbsenceExcuseDto>>.Fail(AttendanceHandlerSupport.PermissionDenied);

        if (request.AttendanceId <= 0)
            return ApiResponse<IReadOnlyList<AbsenceExcuseDto>>.Fail("A valid attendance ID is required");

        GuardianExcuseLinkSnapshot? guardianLink = null;
        if (isGuardian)
        {
            var attendance = await _repository.GetAttendanceForUpdateAsync(
                schoolId.Value,
                request.AttendanceId,
                cancellationToken).ConfigureAwait(false);
            if (attendance is null)
                return ApiResponse<IReadOnlyList<AbsenceExcuseDto>>.Fail("Attendance record was not found");
            var localDate = await _localDateResolver.ResolveAsync(
                schoolId.Value,
                _timeProvider.GetUtcNow(),
                cancellationToken).ConfigureAwait(false);
            if (localDate is null)
                return ApiResponse<IReadOnlyList<AbsenceExcuseDto>>.Fail("School local date could not be resolved");
            guardianLink = await _repository.GetGuardianExcuseLinkAsync(
                schoolId.Value,
                userId,
                attendance.StudentId,
                localDate.Value,
                cancellationToken).ConfigureAwait(false);
            if (guardianLink is null || !guardianLink.GuardianIsActive || !guardianLink.StudentIsActive)
                return ApiResponse<IReadOnlyList<AbsenceExcuseDto>>.Fail("Attendance record was not found");
        }

        var excuses = await _repository.GetExcusesByAttendanceIdAsync(
            schoolId.Value,
            request.AttendanceId,
            cancellationToken).ConfigureAwait(false);

        var visibleExcuses = guardianLink is null
            ? excuses
            : excuses.Where(excuse => excuse.Guardian.Id == guardianLink.GuardianProfileId).ToArray();
        return ApiResponse<IReadOnlyList<AbsenceExcuseDto>>.Success(visibleExcuses);
    }
}
