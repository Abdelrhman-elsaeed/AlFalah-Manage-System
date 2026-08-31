using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Behaviors;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Domain.Events;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.TeacherActions.Handlers;

public sealed class CreateAcademicConcernCommandHandler
    : IRequestHandler<CreateAcademicConcernCommand, ApiResponse<AcademicConcernDto>>
{
    private readonly ITeacherActionWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public CreateAcademicConcernCommandHandler(
        ITeacherActionWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<AcademicConcernDto>> Handle(
        CreateAcademicConcernCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<AcademicConcernDto>.Fail(TeacherActionHandlerSupport.AuthenticationRequired);
        if (!_currentUser.IsInRole(RoleNames.Instructor)
            || !_currentUser.HasPermission(PermissionNames.AcademicConcernCreate))
            return ApiResponse<AcademicConcernDto>.Fail(TeacherActionHandlerSupport.PermissionDenied);

        var request = command.Request;
        if (request.StudentId <= 0 || request.SchoolTimetableEntryId <= 0
            || string.IsNullOrWhiteSpace(request.Category)
            || string.IsNullOrWhiteSpace(request.Description))
            return ApiResponse<AcademicConcernDto>.Fail(
                "Student, timetable entry, category, and description are required");

        var now = _timeProvider.GetUtcNow();
        var occurredAt = request.OccurredAt ?? now;
        if (occurredAt > now.AddMinutes(5))
            return ApiResponse<AcademicConcernDto>.Fail("Occurrence time cannot be in the future");
        var timetableDay = TeacherActionHandlerSupport.ToTimetableDay(occurredAt.DayOfWeek);
        if (timetableDay is null)
            return ApiResponse<AcademicConcernDto>.Fail(TeacherActionHandlerSupport.ScopeDenied);

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
            return ApiResponse<AcademicConcernDto>.Fail(TeacherActionHandlerSupport.ScopeDenied);

        var concern = new AcademicConcern
        {
            SchoolId = schoolId.Value,
            StudentId = request.StudentId,
            AcademicTermId = scope.AcademicTermId,
            ClassroomId = scope.ClassroomId,
            Category = request.Category.Trim(),
            Description = request.Description.Trim(),
            OccurredAt = occurredAt,
            ReportedByInstructorProfileId = scope.InstructorProfileId,
            SchoolTimetableEntryId = scope.SchoolTimetableEntryId,
            GuardianDispatchDecision = GuardianDispatchDecision.PendingOfficerDecision,
            CreatedAt = now,
            CreatedByUserId = userId,
            UpdatedAt = now,
            UpdatedByUserId = userId
        };
        concern.AppendDomainEvent(new AcademicConcernLoggedEvent(
            Guid.NewGuid(),
            concern.Id,
            concern.StudentId,
            concern.SchoolId,
            concern.AcademicTermId,
            concern.ClassroomId,
            concern.SchoolTimetableEntryId,
            concern.Category,
            concern.OccurredAt,
            concern.ReportedByInstructorProfileId,
            concern.GuardianDispatchDecision,
            now));

        _repository.Add(concern);
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dto = await _repository.GetAcademicConcernDtoAsync(
            schoolId.Value,
            concern.Id,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The saved academic concern could not be loaded");
        return ApiResponse<AcademicConcernDto>.Success(dto, "Academic concern recorded successfully");
    }
}
