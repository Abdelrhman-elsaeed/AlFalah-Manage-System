using AlFalah.Application.Interfaces;
using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Application.StudentAffairs.DTOs.Teacher;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.TeacherContext.Handlers;

public sealed class GetTeacherCurrentContextQueryHandler
    : IRequestHandler<GetTeacherCurrentContextQuery, ApiResponse<TeacherCurrentContextDto>>
{
    private const string AuthenticationRequired =
        "An authenticated teacher and active school are required";
    private const string PermissionDenied =
        "You do not have permission to view teacher quick actions";
    private const string InstructorProfileNotFound =
        "An active instructor profile was not found for the current user";

    private static readonly string[] QuickActionPermissions =
    {
        PermissionNames.BehaviorCreate,
        PermissionNames.AcademicConcernCreate,
        PermissionNames.SessionDelayCreate,
        PermissionNames.RecognitionCreate,
        PermissionNames.ReferralCreate
    };

    private readonly ITeacherContextRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly ICurrentLessonResolver _currentLessonResolver;
    private readonly TimeProvider _timeProvider;

    public GetTeacherCurrentContextQueryHandler(
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
        GetTeacherCurrentContextQuery query,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        var schoolId = _currentUser.ActiveSchoolId;
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(userId) || schoolId is null)
        {
            return ApiResponse<TeacherCurrentContextDto>.Fail(AuthenticationRequired);
        }

        if (!_currentUser.HasPermission(PermissionNames.TeacherQuickActionView)
            && !_currentUser.IsInRole(RoleNames.Instructor))
        {
            return ApiResponse<TeacherCurrentContextDto>.Fail(PermissionDenied);
        }

        var utcNow = _timeProvider.GetUtcNow();
        var lesson = await _currentLessonResolver.ResolveForInstructorAsync(
            schoolId.Value,
            utcNow,
            userId,
            cancellationToken).ConfigureAwait(false);
        var schoolLocalTime = lesson.SchoolLocalTime ?? utcNow;
        var localDate = lesson.SchoolLocalDate ?? DateOnly.FromDateTime(schoolLocalTime.DateTime);
        var lookup = new TeacherContextLookup(
            schoolId.Value,
            userId,
            localDate,
            lesson.Kind == CurrentLessonResolutionKind.ActiveLesson
                ? BellScheduleResolver.ToDay(schoolLocalTime.DayOfWeek)
                : null,
            lesson.PeriodSequence,
            0,
            false,
            utcNow,
            lesson.BellScheduleRevisionId,
            lesson.SchoolTimetableId,
            lesson.SchoolTimetableEntryId);

        var snapshot = await _repository
            .GetTopPriorityAsync(lookup, cancellationToken)
            .ConfigureAwait(false);
        if (snapshot is null)
        {
            return ApiResponse<TeacherCurrentContextDto>.Fail(InstructorProfileNotFound);
        }

        var currentPeriod = MapPeriod(snapshot.CurrentPeriod, lesson);
        var context = new TeacherCurrentContextDto(
            new ActorSummaryDto(
                snapshot.Teacher.UserId,
                snapshot.Teacher.DisplayName,
                RoleNames.Instructor),
            schoolLocalTime,
            lesson.SchoolTimeZoneId ?? "UTC",
            snapshot.TimetableRevision,
            currentPeriod,
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
                .Where(p => _currentUser.HasPermission(p) || _currentUser.IsInRole(RoleNames.Instructor))
                .ToArray());

        return ApiResponse<TeacherCurrentContextDto>.Success(context);
    }

    private static TeacherPeriodContextDto? MapPeriod(
        TeacherTimetablePeriodSnapshot? period,
        CurrentLessonResolution lesson)
    {
        if (period is null
            || lesson.Kind != CurrentLessonResolutionKind.ActiveLesson
            || lesson.PeriodStartsAt is null
            || lesson.PeriodEndsAt is null)
        {
            return null;
        }

        return new TeacherPeriodContextDto(
            period.TimetableEntryId,
            period.Period,
            lesson.PeriodStartsAt.Value,
            lesson.PeriodEndsAt.Value,
            period.Subject,
            new ClassroomSummaryDto(
                period.Classroom.Id,
                period.Classroom.Label,
                period.Classroom.Stage.ToString(),
                period.Classroom.GradeLevel,
                period.Classroom.Section));
    }
}
