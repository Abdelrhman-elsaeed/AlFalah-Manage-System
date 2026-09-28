using AlFalah.Application.Interfaces;
using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.StudentAffairs.DTOs.Recognitions;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.TeacherActions.Handlers;

public sealed class CreateRecognitionCommandHandler
    : IRequestHandler<CreateRecognitionCommand, ApiResponse<RecognitionDto>>
{
    private readonly ITeacherActionWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly ICurrentLessonResolver _currentLessonResolver;

    public CreateRecognitionCommandHandler(
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

    public async Task<ApiResponse<RecognitionDto>> Handle(
        CreateRecognitionCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (!_currentUser.IsAuthenticated || schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<RecognitionDto>.Fail(TeacherActionHandlerSupport.AuthenticationRequired);

        if (!_currentUser.IsInRole(RoleNames.Instructor)
            || !_currentUser.HasPermission(PermissionNames.RecognitionCreate))
            return ApiResponse<RecognitionDto>.Fail(TeacherActionHandlerSupport.PermissionDenied);

        var request = command.Request;
        if (request.StudentId <= 0
            || string.IsNullOrWhiteSpace(request.RecognitionType)
            || string.IsNullOrWhiteSpace(request.Title)
            || string.IsNullOrWhiteSpace(request.Description))
        {
            return ApiResponse<RecognitionDto>.Fail(
                "Student, recognition type, title, and description are required");
        }

        var now = _timeProvider.GetUtcNow();
        var recognizedAt = request.RecognizedAt ?? now;
        if (recognizedAt > now.AddMinutes(5))
            return ApiResponse<RecognitionDto>.Fail("Recognition time cannot be in the future");

        var lesson = await _currentLessonResolver.ResolveForInstructorAsync(
            schoolId.Value, now, userId, cancellationToken).ConfigureAwait(false);
        if (lesson.Kind != CurrentLessonResolutionKind.ActiveLesson
            || lesson.PeriodStartsAt is null
            || lesson.PeriodEndsAt is null
            || recognizedAt < lesson.PeriodStartsAt.Value
            || recognizedAt >= lesson.PeriodEndsAt.Value)
            return ApiResponse<RecognitionDto>.Fail(TeacherActionHandlerSupport.ScopeDenied);

        var scope = await _repository.ResolveCurrentRosterScopeAsync(
            schoolId.Value,
            userId,
            request.StudentId,
            lesson.EffectiveInstructor!.Id,
            lesson.AcademicYearId!.Value,
            lesson.Semester!.Value,
            lesson.Classroom!.Id,
            lesson.SchoolTimetableId!.Value,
            lesson.SchoolTimetableEntryId!.Value,
            lesson.PeriodSequence!.Value,
            lesson.SchoolLocalDate!.Value,
            cancellationToken).ConfigureAwait(false);

        if (scope is null)
            return ApiResponse<RecognitionDto>.Fail(TeacherActionHandlerSupport.ScopeDenied);

        var recognition = new StudentRecognition
        {
            SchoolId = schoolId.Value,
            StudentId = request.StudentId,
            AcademicTermId = scope.AcademicTermId,
            ClassroomId = scope.ClassroomId > 0 ? scope.ClassroomId : null,
            RecognitionType = request.RecognitionType.Trim(),
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            RecognizedAt = recognizedAt,
            ReportedByInstructorProfileId = scope.InstructorProfileId,
            GuardianNotificationStatus = GuardianNotificationStatus.Pending,
            CreatedAt = now,
            CreatedByUserId = userId,
            UpdatedAt = now,
            UpdatedByUserId = userId
        };

        _repository.Add(recognition);
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dto = await _repository.GetRecognitionDtoAsync(
            schoolId.Value,
            recognition.Id,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The saved recognition could not be loaded");

        return ApiResponse<RecognitionDto>.Success(dto, "Student recognition recorded successfully");
    }
}
