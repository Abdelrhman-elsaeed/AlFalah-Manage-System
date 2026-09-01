using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Application.StudentAffairs.DTOs.Teacher;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.TeacherContext.Handlers;

public sealed class GetTeacherTopPriorityQueryHandler
    : IRequestHandler<GetTeacherTopPriorityQuery, ApiResponse<TeacherTopPriorityDto>>
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
        PermissionNames.RecognitionCreate
    };

    private readonly ITeacherContextRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TeacherContextSchedule _schedule;
    private readonly TimeProvider _timeProvider;

    public GetTeacherTopPriorityQueryHandler(
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

    public async Task<ApiResponse<TeacherTopPriorityDto>> Handle(
        GetTeacherTopPriorityQuery query,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        var schoolId = _currentUser.ActiveSchoolId;
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(userId) || schoolId is null)
        {
            return ApiResponse<TeacherTopPriorityDto>.Fail(AuthenticationRequired);
        }

        if (!_currentUser.HasPermission(PermissionNames.TeacherQuickActionView))
        {
            return ApiResponse<TeacherTopPriorityDto>.Fail(PermissionDenied);
        }

        var utcNow = _timeProvider.GetUtcNow();
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
            utcNow);

        var snapshot = await _repository
            .GetTopPriorityAsync(lookup, cancellationToken)
            .ConfigureAwait(false);
        if (snapshot is null)
        {
            return ApiResponse<TeacherTopPriorityDto>.Fail(InstructorProfileNotFound);
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
                .Where(_currentUser.HasPermission)
                .ToArray());

        var result = new TeacherTopPriorityDto(
            context,
            snapshot.PendingGatePassAcknowledgements,
            snapshot.PendingEntryPermitAcknowledgements,
            BuildAlerts(snapshot));

        return ApiResponse<TeacherTopPriorityDto>.Success(result);
    }

    private TeacherPeriodContextDto? MapPeriod(
        TeacherTimetablePeriodSnapshot? period,
        DateOnly localDate)
    {
        if (period is null)
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

    private static IReadOnlyList<string> BuildAlerts(TeacherContextSnapshot snapshot)
    {
        var alerts = new List<string>(3);
        if (snapshot.CurrentPeriod is null)
        {
            alerts.Add("No current lesson was found in the published timetable");
        }

        if (snapshot.PendingGatePassAcknowledgements > 0)
        {
            alerts.Add($"{snapshot.PendingGatePassAcknowledgements} gate pass acknowledgement(s) pending");
        }

        if (snapshot.PendingEntryPermitAcknowledgements > 0)
        {
            alerts.Add($"{snapshot.PendingEntryPermitAcknowledgements} classroom entry permit acknowledgement(s) pending");
        }

        return alerts;
    }
}
