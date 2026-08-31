using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Delays;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Domain.Events;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.TeacherActions.Handlers;

public sealed class CreateSessionDelayCommandHandler
    : IRequestHandler<CreateSessionDelayCommand, ApiResponse<SessionDelayDto>>
{
    private readonly ITeacherActionWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public CreateSessionDelayCommandHandler(
        ITeacherActionWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<SessionDelayDto>> Handle(
        CreateSessionDelayCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<SessionDelayDto>.Fail(TeacherActionHandlerSupport.AuthenticationRequired);
        if (!_currentUser.IsInRole(RoleNames.Instructor)
            || !_currentUser.HasPermission(PermissionNames.SessionDelayCreate))
            return ApiResponse<SessionDelayDto>.Fail(TeacherActionHandlerSupport.PermissionDenied);

        var request = command.Request;
        if (request.StudentId <= 0 || request.SchoolTimetableEntryId <= 0
            || request.DelayMinutes is < 0)
            return ApiResponse<SessionDelayDto>.Fail(
                "Student, timetable entry, and a non-negative delay duration are required");

        var now = _timeProvider.GetUtcNow();
        var occurredAt = request.OccurredAt ?? now;
        if (occurredAt > now.AddMinutes(5))
            return ApiResponse<SessionDelayDto>.Fail("Occurrence time cannot be in the future");
        var timetableDay = TeacherActionHandlerSupport.ToTimetableDay(occurredAt.DayOfWeek);
        if (timetableDay is null)
            return ApiResponse<SessionDelayDto>.Fail(TeacherActionHandlerSupport.ScopeDenied);

        var scope = await _repository.ResolveScopeAsync(
            schoolId.Value,
            userId,
            request.StudentId,
            request.SchoolTimetableEntryId,
            _currentUser.HasPermission(PermissionNames.TeacherQuickActionOverride),
            timetableDay.Value,
            DateOnly.FromDateTime(occurredAt.DateTime),
            cancellationToken).ConfigureAwait(false);
        if (scope is null)
            return ApiResponse<SessionDelayDto>.Fail(TeacherActionHandlerSupport.ScopeDenied);

        var delay = new SessionDelay
        {
            SchoolId = schoolId.Value,
            StudentId = request.StudentId,
            AcademicTermId = scope.AcademicTermId,
            ClassroomId = scope.ClassroomId,
            SchoolTimetableId = scope.SchoolTimetableId,
            SchoolTimetableEntryId = scope.SchoolTimetableEntryId,
            Period = scope.Period,
            OccurredAt = occurredAt,
            DelayMinutes = request.DelayMinutes,
            Reason = request.Reason?.Trim(),
            ReportedByInstructorProfileId = scope.InstructorProfileId,
            ReportedAt = now,
            GuardianNotificationStatus = GuardianNotificationStatus.Pending,
            CreatedAt = now,
            CreatedByUserId = userId,
            UpdatedAt = now,
            UpdatedByUserId = userId
        };
        delay.AppendDomainEvent(new SessionDelayLoggedEvent(
            Guid.NewGuid(),
            delay.Id,
            delay.StudentId,
            delay.SchoolId,
            delay.AcademicTermId,
            delay.ClassroomId,
            scope.SchoolTimetableId,
            scope.SchoolTimetableEntryId,
            delay.Period,
            delay.OccurredAt,
            delay.DelayMinutes,
            delay.ReportedByInstructorProfileId,
            delay.GuardianNotificationStatus,
            now));

        _repository.Add(delay);
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dto = await _repository.GetSessionDelayDtoAsync(
            schoolId.Value,
            delay.Id,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The saved session delay could not be loaded");
        return ApiResponse<SessionDelayDto>.Success(dto, "Session delay recorded successfully");
    }
}
