using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Attendance;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Domain.Events;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Attendance.Handlers;

public sealed class ReviewAbsenceExcuseCommandHandler
    : IRequestHandler<AcceptAbsenceExcuseCommand, ApiResponse<AbsenceExcuseDto>>,
      IRequestHandler<RejectAbsenceExcuseCommand, ApiResponse<AbsenceExcuseDto>>
{
    private readonly IAttendanceWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public ReviewAbsenceExcuseCommandHandler(
        IAttendanceWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public Task<ApiResponse<AbsenceExcuseDto>> Handle(
        AcceptAbsenceExcuseCommand command,
        CancellationToken cancellationToken) =>
        ReviewAsync(
            command.ExcuseId,
            AbsenceExcuseStatus.Accepted,
            command.Request.ReviewNote,
            command.Request.RowVersion,
            cancellationToken);

    public Task<ApiResponse<AbsenceExcuseDto>> Handle(
        RejectAbsenceExcuseCommand command,
        CancellationToken cancellationToken) =>
        ReviewAsync(
            command.ExcuseId,
            AbsenceExcuseStatus.Rejected,
            command.Request.RejectionReason,
            command.Request.RowVersion,
            cancellationToken);

    private async Task<ApiResponse<AbsenceExcuseDto>> ReviewAsync(
        int excuseId,
        AbsenceExcuseStatus decision,
        string? reviewReason,
        string rowVersion,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<AbsenceExcuseDto>.Fail(AttendanceHandlerSupport.AuthenticationRequired);
        if (!_currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            || !_currentUser.HasPermission(PermissionNames.AttendanceReviewExcuse))
            return ApiResponse<AbsenceExcuseDto>.Fail(AttendanceHandlerSupport.PermissionDenied);
        if (decision == AbsenceExcuseStatus.Rejected && string.IsNullOrWhiteSpace(reviewReason))
            return ApiResponse<AbsenceExcuseDto>.Fail("A rejection reason is required");

        var excuse = await _repository.GetExcuseForUpdateAsync(
            schoolId.Value,
            excuseId,
            cancellationToken).ConfigureAwait(false);
        if (excuse is null)
            return ApiResponse<AbsenceExcuseDto>.Fail("Absence excuse was not found");
        if (excuse.Status != AbsenceExcuseStatus.Pending)
            return ApiResponse<AbsenceExcuseDto>.Fail("Only a pending absence excuse can be reviewed");
        if (excuse.DailyStudentAttendance.Status != StudentAttendanceStatus.Absent)
            return ApiResponse<AbsenceExcuseDto>.Fail("The linked attendance record is no longer absent");
        if (!AttendanceHandlerSupport.TryDecodeExpectedRowVersion(
                rowVersion,
                excuse.RowVersion,
                out var expectedRowVersion))
            return ApiResponse<AbsenceExcuseDto>.Fail(AttendanceHandlerSupport.ConcurrencyConflict);

        _repository.SetExpectedRowVersion(excuse, expectedRowVersion);
        var now = _timeProvider.GetUtcNow();
        excuse.Status = decision;
        excuse.ReviewedByUserId = userId;
        excuse.ReviewedAt = now;
        excuse.ReviewReason = reviewReason?.Trim();
        excuse.UpdatedAt = now;
        excuse.UpdatedByUserId = userId;
        excuse.DailyStudentAttendance.ExcuseStatus = decision;
        excuse.DailyStudentAttendance.UpdatedAt = now;
        excuse.DailyStudentAttendance.UpdatedByUserId = userId;

        if (decision == AbsenceExcuseStatus.Accepted)
        {
            excuse.AppendDomainEvent(new AbsenceExcuseAcceptedEvent(
                Guid.NewGuid(),
                excuse.Id,
                excuse.DailyStudentAttendanceId,
                excuse.DailyStudentAttendance.StudentId,
                excuse.SchoolId,
                excuse.DailyStudentAttendance.AcademicTermId,
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
            return ApiResponse<AbsenceExcuseDto>.Fail(AttendanceHandlerSupport.ConcurrencyConflict);
        }

        var dto = await _repository.GetExcuseDtoAsync(schoolId.Value, excuse.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The reviewed absence excuse could not be loaded");
        return ApiResponse<AbsenceExcuseDto>.Success(
            dto,
            decision == AbsenceExcuseStatus.Accepted
                ? "Absence excuse accepted successfully"
                : "Absence excuse rejected successfully");
    }
}
