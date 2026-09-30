using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.DTOs.Summons;

public sealed class SummonListQuery : StudentAffairsPageQuery
{
    public GuardianSummonStatus? Status { get; set; }
    public ReferralPriority? Priority { get; set; }
    public DateOnly? AppointmentDate { get; set; }
    public string? AssignedWorkerUserId { get; set; }
    public int? StudentId { get; set; }
    public bool? RequiresOfficerReview { get; set; }
}

public sealed record CreateSummonRequestDto(int StudentId, int? ReferralId, string Reason, ReferralPriority Priority, int GuardianProfileId);
public sealed record ScheduleSummonRequestDto(DateTimeOffset AppointmentAt, string Location, string? Instructions, int GuardianProfileId, string RowVersion);
public sealed record AttendSummonRequestDto(string AttendanceNotes, string RowVersion);
public sealed record MarkSummonNoShowRequestDto(string Notes, string RowVersion);
public sealed record StartSummonObservationRequestDto(
    string Goals,
    DateOnly StartDate,
    DateOnly ReviewDate,
    DateOnly? EndDate,
    string ResponsibleStaffUserId,
    IReadOnlyList<string> MeasurableIndicators,
    string Notes,
    string RowVersion);
public sealed record MarkSummonImprovedRequestDto(string OutcomeEvidence, string VerificationDetails, string RowVersion);
public sealed record ReviewSummonAutomationImpactRequestDto(OfficerReviewDecision Decision, string Rationale, string RowVersion);

public sealed record SummonDto(
    int Id,
    StudentSummaryDto Student,
    int? ReferralId,
    string CreatedReason,
    ReferralPriority Priority,
    int? SourceCountSnapshot,
    int? ThresholdSnapshot,
    GuardianSummonStatus Status,
    DateTimeOffset? ScheduledAt,
    string? Location,
    string? Instructions,
    GuardianSummaryDto Guardian,
    ActorSummaryDto? AssignedSocialWorker,
    bool RequiresOfficerReview,
    string? OfficerReviewReason,
    DateTimeOffset? GuardianNotifiedAt,
    string RowVersion,
    int? CurrentMetricCount = null,
    string? ObservationGoals = null,
    DateOnly? ObservationStartDate = null,
    DateOnly? ObservationReviewDate = null,
    DateOnly? ObservationEndDate = null,
    string? ObservationResponsibleStaffUserId = null,
    IReadOnlyList<string>? ObservationIndicators = null,
    string? ObservationNotes = null,
    string? OutcomeEvidence = null,
    string? OutcomeVerificationDetails = null,
    NotificationDeliveryDto? GuardianDelivery = null);

public sealed record SummonAppointmentDto(
    DateTimeOffset AppointmentAt,
    string Location,
    string? Instructions,
    string Action,
    ActorSummaryDto Actor,
    DateTimeOffset OccurredAt,
    string? Notes);
public sealed record SummonHistoryDto(
    IReadOnlyList<TransitionDto> Transitions,
    IReadOnlyList<SummonAppointmentDto>? Appointments = null);

public sealed record GuardianSummonDto(
    int Id,
    StudentSummaryDto Student,
    string Reason,
    ReferralPriority Priority,
    GuardianSummonStatus Status,
    DateTimeOffset? ScheduledAt,
    string? Location,
    string? Instructions,
    DateTimeOffset? GuardianNotifiedAt);

public sealed record CreateSummonCommand(CreateSummonRequestDto Request, string IdempotencyKey) : IRequest<ApiResponse<SummonDto>>;
public sealed record GetSummonsQuery(SummonListQuery Query) : IRequest<ApiResponse<PagedResult<SummonDto>>>;
public sealed record GetMySummonsQuery(SummonListQuery Query) : IRequest<ApiResponse<PagedResult<GuardianSummonDto>>>;
public sealed record GetMySummonByIdQuery(int SummonId) : IRequest<ApiResponse<GuardianSummonDto>>;
public sealed record GetSummonByIdQuery(int SummonId) : IRequest<ApiResponse<SummonDto>>;
public sealed record ScheduleSummonCommand(int SummonId, ScheduleSummonRequestDto Request) : IRequest<ApiResponse<SummonDto>>;
public sealed record AttendSummonCommand(int SummonId, AttendSummonRequestDto Request) : IRequest<ApiResponse<SummonDto>>;
public sealed record MarkSummonNoShowCommand(int SummonId, MarkSummonNoShowRequestDto Request) : IRequest<ApiResponse<SummonDto>>;
public sealed record StartSummonObservationCommand(int SummonId, StartSummonObservationRequestDto Request) : IRequest<ApiResponse<SummonDto>>;
public sealed record MarkSummonImprovedCommand(int SummonId, MarkSummonImprovedRequestDto Request) : IRequest<ApiResponse<SummonDto>>;
public sealed record ReviewSummonAutomationImpactCommand(int SummonId, ReviewSummonAutomationImpactRequestDto Request) : IRequest<ApiResponse<SummonDto>>;
public sealed record GetSummonHistoryQuery(int SummonId) : IRequest<ApiResponse<SummonHistoryDto>>;
public sealed record GetAutomationImpactReviewsQuery(SummonListQuery Query)
    : IRequest<ApiResponse<PagedResult<SummonDto>>>;
