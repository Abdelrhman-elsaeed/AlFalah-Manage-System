using AlFalah.Application.Interfaces;
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
        PermissionNames.RecognitionCreate,
        PermissionNames.ReferralCreate
    };

    private readonly ITeacherContextRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TeacherContextSchedule _schedule;
    private readonly TimeProvider _timeProvider;

    public GetTeacherPeriodRosterQueryHandler(
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
        GetTeacherPeriodRosterQuery query,
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
        var schoolLocalTime = _schedule.ToSchoolLocalTime(utcNow);
        var localDate = DateOnly.FromDateTime(schoolLocalTime.DateTime);

        var snapshot = await _repository.GetPeriodRosterAsync(
            schoolId.Value,
            userId,
            query.TimetableEntryId,
            localDate,
            cancellationToken).ConfigureAwait(false);

        if (snapshot is null)
        {
            return ApiResponse<TeacherCurrentContextDto>.Fail("Timetable entry was not found or is not published");
        }

        TeacherPeriodContextDto? periodDto = null;
        if (snapshot.CurrentPeriod is not null)
        {
            var window = _schedule.GetWindow(localDate, snapshot.CurrentPeriod.Period);
            periodDto = new TeacherPeriodContextDto(
                snapshot.CurrentPeriod.TimetableEntryId,
                snapshot.CurrentPeriod.Period,
                window.StartsAt,
                window.EndsAt,
                snapshot.CurrentPeriod.Subject,
                new ClassroomSummaryDto(
                    snapshot.CurrentPeriod.Classroom.Id,
                    snapshot.CurrentPeriod.Classroom.Label,
                    snapshot.CurrentPeriod.Classroom.Stage.ToString(),
                    snapshot.CurrentPeriod.Classroom.GradeLevel,
                    snapshot.CurrentPeriod.Classroom.Section));
        }

        var context = new TeacherCurrentContextDto(
            new ActorSummaryDto(snapshot.Teacher.UserId, snapshot.Teacher.DisplayName, RoleNames.Instructor),
            schoolLocalTime,
            _schedule.TimeZoneId,
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
                .Where(p => _currentUser.HasPermission(p) || _currentUser.IsInRole(RoleNames.Instructor))
                .ToArray());

        return ApiResponse<TeacherCurrentContextDto>.Success(context);
    }
}
