using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.DTOs.Teacher;

public sealed record TeacherPeriodContextDto(
    int TimetableEntryId,
    byte Period,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Subject,
    ClassroomSummaryDto Classroom);
public sealed record TeacherCurrentContextDto(
    ActorSummaryDto Teacher,
    DateTimeOffset SchoolLocalTime,
    string SchoolTimeZone,
    int TimetableRevision,
    TeacherPeriodContextDto? CurrentPeriod,
    IReadOnlyList<StudentSummaryDto> Roster,
    IReadOnlyList<string> PermittedQuickActions);

public sealed record TeacherTopPriorityDto(
    TeacherCurrentContextDto Context,
    int PendingGatePassAcknowledgements,
    int PendingEntryPermitAcknowledgements,
    IReadOnlyList<string> Alerts);

public sealed record GetTeacherCurrentContextQuery : IRequest<ApiResponse<TeacherCurrentContextDto>>;
public sealed record GetTeacherPeriodRosterQuery(int TimetableEntryId) : IRequest<ApiResponse<TeacherCurrentContextDto>>;
public sealed record GetTeacherTopPriorityQuery : IRequest<ApiResponse<TeacherTopPriorityDto>>;
