using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Attendance;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Domain.Events;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Attendance.Handlers;

public sealed class CorrectStudentAttendanceCommandHandler
    : IRequestHandler<CorrectStudentAttendanceCommand, ApiResponse<StudentAttendanceRecordDto>>
{
    private readonly IAttendanceWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public CorrectStudentAttendanceCommandHandler(
        IAttendanceWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<StudentAttendanceRecordDto>> Handle(
        CorrectStudentAttendanceCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<StudentAttendanceRecordDto>.Fail(AttendanceHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.AttendanceOverrideCorrection))
            return ApiResponse<StudentAttendanceRecordDto>.Fail(AttendanceHandlerSupport.PermissionDenied);

        var reason = command.Request.CorrectionReason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            return ApiResponse<StudentAttendanceRecordDto>.Fail("A correction reason is required");

        var attendance = await _repository.GetAttendanceForUpdateAsync(
            schoolId.Value,
            command.AttendanceId,
            cancellationToken).ConfigureAwait(false);

        if (attendance is null)
            return ApiResponse<StudentAttendanceRecordDto>.Fail("Attendance record was not found");

        if (!AttendanceHandlerSupport.TryDecodeExpectedRowVersion(
                command.Request.RowVersion,
                attendance.RowVersion,
                out var expectedRowVersion))
            return ApiResponse<StudentAttendanceRecordDto>.Fail("The attendance record was modified by another user");

        var now = _timeProvider.GetUtcNow();
        var wasAbsent = attendance.Status == StudentAttendanceStatus.Absent;
        var willBeAbsent = command.Request.Status == StudentAttendanceStatus.Absent;

        attendance.Status = command.Request.Status;
        if (command.Request.Status != StudentAttendanceStatus.Absent)
        {
            attendance.ExcuseStatus = null;
        }

        attendance.CorrectionReason = reason;
        attendance.CorrectedByUserId = userId;
        attendance.CorrectedAt = now;
        attendance.UpdatedAt = now;
        attendance.UpdatedByUserId = userId;

        if (!wasAbsent && willBeAbsent)
        {
            attendance.AppendDomainEvent(new StudentAbsentRecordedEvent(
                Guid.NewGuid(),
                attendance.Id,
                attendance.StudentId,
                attendance.SchoolId,
                attendance.AcademicTermId,
                attendance.ClassroomId,
                attendance.AttendanceDate,
                userId,
                now,
                now));
        }

        try
        {
            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (AttendanceConcurrencyException)
        {
            return ApiResponse<StudentAttendanceRecordDto>.Fail("The attendance record was modified by another user");
        }

        var dto = await _repository.GetAttendanceRecordDtoAsync(
            schoolId.Value,
            attendance.Id,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The corrected attendance record could not be loaded");

        return ApiResponse<StudentAttendanceRecordDto>.Success(dto, "Attendance record corrected successfully");
    }
}
