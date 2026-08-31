using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.DTOs.Delays;

public sealed class MorningDelayListQuery : StudentAffairsPageQuery
{
    public DateOnly? Date { get; set; }
    public int? AcademicTermId { get; set; }
    public int? ClassroomId { get; set; }
    public int? StudentId { get; set; }
    public string? Severity { get; set; }
}

public sealed class SessionDelayListQuery : StudentAffairsPageQuery
{
    public int? AcademicTermId { get; set; }
    public int? ClassroomId { get; set; }
    public int? StudentId { get; set; }
    public DateOnly? Date { get; set; }
}

public sealed record ProvideMorningDelayReasonRequestDto(string Reason);
public sealed record CorrectMorningDelayRequestDto(DateTimeOffset ArrivalAt, int DelayMinutes, string Reason, string RowVersion);
public sealed record CreateSessionDelayRequestDto(int StudentId, int SchoolTimetableEntryId, DateTimeOffset? OccurredAt, int? DelayMinutes, string? Reason);
public sealed record CorrectSessionDelayRequestDto(DateTimeOffset OccurredAt, int? DelayMinutes, string? DelayReason, string CorrectionReason, string RowVersion);

public sealed record MorningDelayDto(
    int Id,
    StudentSummaryDto Student,
    DateTimeOffset ArrivalAt,
    string SchoolLocalArrivalTime,
    string SchoolTimeZone,
    TimeOnly CutoffSnapshot,
    int DelayMinutes,
    string? Reason,
    MetricBadgeDto Metric,
    NotificationDeliveryDto? GuardianNotification,
    string RowVersion);

public sealed record SessionDelayDto(
    int Id,
    StudentSummaryDto Student,
    int TimetableEntryId,
    byte Period,
    DateTimeOffset OccurredAt,
    int? DelayMinutes,
    string? Reason,
    ActorSummaryDto Reporter,
    MetricBadgeDto Metric,
    NotificationDeliveryDto? GuardianNotification,
    string RowVersion);

public sealed record GetMorningDelaysQuery(MorningDelayListQuery Query) : IRequest<ApiResponse<PagedResult<MorningDelayDto>>>;
public sealed record GetMorningDelayByIdQuery(int DelayId) : IRequest<ApiResponse<MorningDelayDto>>;
public sealed record ProvideMorningDelayReasonCommand(int DelayId, ProvideMorningDelayReasonRequestDto Request) : IRequest<ApiResponse<MorningDelayDto>>;
public sealed record CorrectMorningDelayCommand(int DelayId, CorrectMorningDelayRequestDto Request) : IRequest<ApiResponse<MorningDelayDto>>;
public sealed record CreateSessionDelayCommand(CreateSessionDelayRequestDto Request) : IRequest<ApiResponse<SessionDelayDto>>;
public sealed record GetSessionDelaysQuery(SessionDelayListQuery Query) : IRequest<ApiResponse<PagedResult<SessionDelayDto>>>;
public sealed record GetSessionDelayByIdQuery(int DelayId) : IRequest<ApiResponse<SessionDelayDto>>;
public sealed record CorrectSessionDelayCommand(int DelayId, CorrectSessionDelayRequestDto Request) : IRequest<ApiResponse<SessionDelayDto>>;
public sealed record RecordBiometricMorningArrivalDelayCommand(
    int StudentId,
    DateTimeOffset ArrivalAt,
    DateOnly SchoolLocalDate,
    TimeOnly CutoffTimeSnapshot,
    int DelayMinutes,
    string? Reason) : IRequest<ApiResponse<MorningDelayDto>>;
