using AlFalah.Application.Interfaces;
using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Application.StudentAffairs.DTOs.Teacher;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.TeacherContext.Handlers;

public sealed class GetTeacherPeriodRosterQueryHandler
    : IRequestHandler<GetTeacherPeriodRosterQuery, ApiResponse<TeacherCurrentContextDto>>
{
    private const string AuthenticationRequired =
        "An authenticated teacher and active school are required";
    private const string PermissionDenied =
        "You do not have permission to view teacher quick actions";

    private static readonly string[] QuickActionPermissions =
    {
        PermissionNames.BehaviorCreate,
        PermissionNames.AcademicConcernCreate,
        PermissionNames.SessionDelayCreate,
        PermissionNames.RecognitionCreate
    };

    private readonly ITeacherContextRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly ICurrentLessonResolver _currentLessonResolver;
    private readonly TimeProvider _timeProvider;

    public GetTeacherPeriodRosterQueryHandler(
        ITeacherContextRepository repository,
        ICurrentUserService currentUser,
        ICurrentLessonResolver currentLessonResolver,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _currentLessonResolver = currentLessonResolver;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<TeacherCurrentContextDto>> Handle(
        GetTeacherPeriodRosterQuery query,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        var schoolId = _currentUser.ActiveSchoolId;
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(userId) || schoolId is null)
        {
            return ApiResponse<TeacherCurrentContextDto>.Fail(AuthenticationRequired);
        }

        if (!_currentUser.IsInRole(RoleNames.Instructor)
            || !_currentUser.HasPermission(PermissionNames.TeacherQuickActionView))
        {
            return ApiResponse<TeacherCurrentContextDto>.Fail(PermissionDenied);
        }

        var utcNow = _timeProvider.GetUtcNow();
        var lesson = await _currentLessonResolver.ResolveForInstructorAsync(
            schoolId.Value, utcNow, userId, cancellationToken).ConfigureAwait(false);
        if (lesson.Kind != CurrentLessonResolutionKind.ActiveLesson
            || lesson.SchoolTimetableEntryId != query.TimetableEntryId)
        {
            return ApiResponse<TeacherCurrentContextDto>.Fail(
                "Timetable entry was not found in the teacher's active current lesson");
        }

        var schoolLocalTime = lesson.SchoolLocalTime!.Value;
        var lookup = new TeacherContextLookup(
            schoolId.Value,
            userId,
            lesson.SchoolLocalDate!.Value,
            BellScheduleResolver.ToDay(schoolLocalTime.DayOfWeek),
            lesson.PeriodSequence,
            0,
            false,
            utcNow,
            lesson.BellScheduleRevisionId,
            lesson.SchoolTimetableId,
            lesson.SchoolTimetableEntryId);
        var snapshot = await _repository.GetTopPriorityAsync(lookup, cancellationToken).ConfigureAwait(false);
        if (snapshot?.CurrentPeriod is null)
        {
            return ApiResponse<TeacherCurrentContextDto>.Fail(
                "Timetable entry was not found in the teacher's active current lesson");
        }

        var period = snapshot.CurrentPeriod;
        var periodDto = new TeacherPeriodContextDto(
            lesson.SchoolTimetableId!.Value,
            lesson.BellScheduleRevisionId!.Value,
            period.TimetableEntryId,
            period.Period,
            lesson.PeriodStartsAt!.Value,
            lesson.PeriodEndsAt!.Value,
            period.Subject,
            new ClassroomSummaryDto(
                period.Classroom.Id,
                period.Classroom.Label,
                period.Classroom.Stage.ToString(),
                period.Classroom.GradeLevel,
                period.Classroom.Section),
            new ActorSummaryDto(lesson.OriginalInstructor!.UserId, lesson.OriginalInstructor.DisplayName, RoleNames.Instructor),
            new ActorSummaryDto(lesson.EffectiveInstructor!.UserId, lesson.EffectiveInstructor.DisplayName, RoleNames.Instructor),
            lesson.ActiveSubstitutionId);

        var context = new TeacherCurrentContextDto(
            new ActorSummaryDto(snapshot.Teacher.UserId, snapshot.Teacher.DisplayName, RoleNames.Instructor),
            lesson.Kind.ToString(),
            lesson.ResolutionReason,
            schoolLocalTime,
            lesson.SchoolTimeZoneId!,
            snapshot.TimetableRevision,
            periodDto,
            snapshot.Roster
                .Select(student => new StudentSummaryDto(
                    student.Id,
                    student.StudentNumber,
                    student.DisplayName,
                    student.ClassroomId,
                    student.ClassLabel,
                    student.IsActive,
                    student.PhotoUrl))
                .ToArray(),
            QuickActionPermissions
                .Where(_currentUser.HasPermission)
                .ToArray());

        return ApiResponse<TeacherCurrentContextDto>.Success(context);
    }
}
