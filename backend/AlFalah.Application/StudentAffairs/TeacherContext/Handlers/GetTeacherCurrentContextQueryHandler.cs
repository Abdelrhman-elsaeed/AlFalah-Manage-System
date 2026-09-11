using AlFalah.Application.Interfaces;
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
    private readonly TeacherContextSchedule _schedule;
    private readonly TimeProvider _timeProvider;

    public GetTeacherCurrentContextQueryHandler(
        ITeacherContextRepository repository,
        ICurrentUserService currentUser,
        TeacherContextSchedule schedule,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _schedule = schedule;
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
        await _schedule.LoadAsync(schoolId.Value, utcNow, cancellationToken);
        var schoolLocalTime = _schedule.ToSchoolLocalTime(utcNow);
        var localDate = DateOnly.FromDateTime(schoolLocalTime.DateTime);
        var localClock = TimeOnly.FromDateTime(schoolLocalTime.DateTime);
        var lookup = new TeacherContextLookup(
            schoolId.Value,
            userId,
            localDate,
            TeacherContextSchedule.ToTimetableDay(schoolLocalTime.DayOfWeek),
            _schedule.GetCurrentPeriod(localClock),
            _schedule.GetFallbackPeriod(localClock),
            _schedule.AllowOffHoursFallback,
            utcNow, _schedule.RevisionId);

        var snapshot = await _repository
            .GetTopPriorityAsync(lookup, cancellationToken)
            .ConfigureAwait(false);
        if (snapshot is null)
        {
            return ApiResponse<TeacherCurrentContextDto>.Fail(InstructorProfileNotFound);
        }

        var currentPeriod = MapPeriod(snapshot.CurrentPeriod, localDate);
        var context = new TeacherCurrentContextDto(
            new ActorSummaryDto(
                snapshot.Teacher.UserId,
                snapshot.Teacher.DisplayName,
                RoleNames.Instructor),
            schoolLocalTime,
            _schedule.TimeZoneId,
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

    private TeacherPeriodContextDto? MapPeriod(
        TeacherTimetablePeriodSnapshot? period,
        DateOnly localDate)
    {
        if (period is null || !_schedule.HasPeriod(localDate, period.Period))
        {
            return null;
        }

        var window = _schedule.GetWindow(localDate, period.Period);
        return new TeacherPeriodContextDto(
            period.TimetableEntryId,
            period.Period,
            window.StartsAt,
            window.EndsAt,
            period.Subject,
            new ClassroomSummaryDto(
                period.Classroom.Id,
                period.Classroom.Label,
                period.Classroom.Stage.ToString(),
                period.Classroom.GradeLevel,
                period.Classroom.Section));
    }
}
