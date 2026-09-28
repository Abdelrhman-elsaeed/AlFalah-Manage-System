using AlFalah.Application.Interfaces;
using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.StudentAffairs.DTOs.Behaviors;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Domain.Events;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.TeacherActions.Handlers;

public sealed class CreateBehaviorIncidentCommandHandler
    : IRequestHandler<CreateBehaviorIncidentCommand, ApiResponse<BehaviorIncidentDto>>
{
    private readonly ITeacherActionWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly ICurrentLessonResolver _currentLessonResolver;

    public CreateBehaviorIncidentCommandHandler(
        ITeacherActionWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider,
        ICurrentLessonResolver currentLessonResolver)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
        _currentLessonResolver = currentLessonResolver;
    }

    public async Task<ApiResponse<BehaviorIncidentDto>> Handle(
        CreateBehaviorIncidentCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (!_currentUser.IsAuthenticated || schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<BehaviorIncidentDto>.Fail(TeacherActionHandlerSupport.AuthenticationRequired);
        if (!_currentUser.IsInRole(RoleNames.Instructor)
            || !_currentUser.HasPermission(PermissionNames.BehaviorCreate))
            return ApiResponse<BehaviorIncidentDto>.Fail(TeacherActionHandlerSupport.PermissionDenied);

        var request = command.Request;
        if (request.StudentId <= 0 || request.SchoolTimetableEntryId is null
            || request.SchoolTimetableEntryId <= 0
            || string.IsNullOrWhiteSpace(request.Category)
            || string.IsNullOrWhiteSpace(request.Description))
            return ApiResponse<BehaviorIncidentDto>.Fail(
                "Student, timetable entry, category, and description are required");

        var now = _timeProvider.GetUtcNow();
        var occurredAt = request.OccurredAt ?? now;
        if (occurredAt > now.AddMinutes(5))
            return ApiResponse<BehaviorIncidentDto>.Fail("Occurrence time cannot be in the future");
        var lesson = await _currentLessonResolver.ResolveForInstructorAsync(
            schoolId.Value, now, userId, cancellationToken).ConfigureAwait(false);
        if (lesson.Kind != CurrentLessonResolutionKind.ActiveLesson
            || lesson.SchoolTimetableEntryId != request.SchoolTimetableEntryId
            || lesson.PeriodStartsAt is null
            || lesson.PeriodEndsAt is null
            || occurredAt < lesson.PeriodStartsAt.Value
            || occurredAt >= lesson.PeriodEndsAt.Value)
            return ApiResponse<BehaviorIncidentDto>.Fail(TeacherActionHandlerSupport.ScopeDenied);

        var scope = await _repository.ResolveCurrentRosterScopeAsync(
            schoolId.Value,
            userId,
            request.StudentId,
            lesson.EffectiveInstructor!.Id,
            lesson.AcademicYearId!.Value,
            lesson.Semester!.Value,
            lesson.Classroom!.Id,
            lesson.SchoolTimetableId!.Value,
            request.SchoolTimetableEntryId.Value,
            lesson.PeriodSequence!.Value,
            lesson.SchoolLocalDate!.Value,
            cancellationToken).ConfigureAwait(false);
        if (scope is null)
            return ApiResponse<BehaviorIncidentDto>.Fail(TeacherActionHandlerSupport.ScopeDenied);

        var incident = new BehaviorIncident
        {
            SchoolId = schoolId.Value,
            StudentId = request.StudentId,
            AcademicTermId = scope.AcademicTermId,
            ClassroomId = scope.ClassroomId,
            CategoryCode = request.Category.Trim(),
            Severity = request.Severity,
            Description = request.Description.Trim(),
            OccurredAt = occurredAt,
            Location = request.Location?.Trim(),
            ReportedByInstructorProfileId = scope.InstructorProfileId,
            ImmediateActionTaken = request.ImmediateAction?.Trim(),
            GuardianDispatchDecision = GuardianDispatchDecision.PendingOfficerDecision,
            CreatedAt = now,
            CreatedByUserId = userId,
            UpdatedAt = now,
            UpdatedByUserId = userId
        };
        incident.AppendDomainEvent(new BehaviorIncidentLoggedEvent(
            Guid.NewGuid(),
            incident.Id,
            incident.StudentId,
            incident.SchoolId,
            incident.AcademicTermId,
            incident.ClassroomId,
            scope.SchoolTimetableId,
            scope.SchoolTimetableEntryId,
            scope.Period,
            incident.CategoryCode,
            incident.Severity,
            incident.OccurredAt,
            incident.ReportedByInstructorProfileId,
            null,
            incident.GuardianDispatchDecision,
            now));

        _repository.Add(incident);
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dto = await _repository.GetBehaviorDtoAsync(
            schoolId.Value,
            incident.Id,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The saved behavior incident could not be loaded");
        return ApiResponse<BehaviorIncidentDto>.Success(dto, "Behavior incident recorded successfully");
    }
}
