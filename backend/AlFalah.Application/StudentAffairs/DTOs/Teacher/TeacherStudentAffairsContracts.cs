using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.DTOs.Teacher;

public sealed record TeacherPeriodContextDto(
    int TimetableId,
    int BellScheduleRevisionId,
    int TimetableEntryId,
    int Period,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Subject,
    ClassroomSummaryDto Classroom,
    ActorSummaryDto OriginalInstructor,
    ActorSummaryDto EffectiveInstructor,
    int? SubstitutionId);
public sealed record TeacherCurrentContextDto(
    ActorSummaryDto Teacher,
    string ResolutionKind,
    string ResolutionReason,
    DateTimeOffset SchoolLocalTime,
    string SchoolTimeZone,
    int TimetableRevision,
    TeacherPeriodContextDto? CurrentPeriod,
    IReadOnlyList<StudentSummaryDto> Roster,
    IReadOnlyList<string> PermittedQuickActions);

public sealed record TeacherGatePassAcknowledgementDto(
    int Id,
    StudentSummaryDto Student,
    ClassroomSummaryDto Classroom,
    DateTimeOffset WindowStartsAt,
    DateTimeOffset WindowEndsAt,
    string Reason,
    string Status,
    string RowVersion);

public sealed record TeacherEntryPermitAcknowledgementDto(
    int Id,
    StudentSummaryDto Student,
    ClassroomSummaryDto Classroom,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidUntil,
    string Reason,
    string Status,
    string RowVersion);

public sealed record TeacherTopPriorityDto(
    TeacherCurrentContextDto Context,
    int PendingGatePassAcknowledgements,
    int PendingEntryPermitAcknowledgements,
    IReadOnlyList<TeacherGatePassAcknowledgementDto> GatePassAcknowledgements,
    IReadOnlyList<TeacherEntryPermitAcknowledgementDto> EntryPermitAcknowledgements,
    IReadOnlyList<string> Alerts);

public sealed record GetTeacherCurrentContextQuery : IRequest<ApiResponse<TeacherCurrentContextDto>>;
public sealed record GetTeacherPeriodRosterQuery(int TimetableEntryId) : IRequest<ApiResponse<TeacherCurrentContextDto>>;
public sealed record GetTeacherTopPriorityQuery : IRequest<ApiResponse<TeacherTopPriorityDto>>;
