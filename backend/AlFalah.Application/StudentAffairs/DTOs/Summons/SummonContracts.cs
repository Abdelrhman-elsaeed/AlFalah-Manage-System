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
}

public sealed record CreateSummonRequestDto(int StudentId, int? ReferralId, string Reason, ReferralPriority Priority, int GuardianProfileId);
public sealed record ScheduleSummonRequestDto(DateTimeOffset AppointmentAt, string Location, string? Instructions, int GuardianProfileId, string RowVersion);
public sealed record AttendSummonRequestDto(string AttendanceNotes, string RowVersion);
public sealed record StartSummonObservationRequestDto(string ObservationPlan, string RowVersion);
public sealed record MarkSummonImprovedRequestDto(string OutcomeEvidence, string RowVersion);
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
    string RowVersion);

public sealed record SummonHistoryDto(IReadOnlyList<TransitionDto> Transitions);

public sealed record CreateSummonCommand(CreateSummonRequestDto Request, string IdempotencyKey) : IRequest<ApiResponse<SummonDto>>;
public sealed record GetSummonsQuery(SummonListQuery Query) : IRequest<ApiResponse<PagedResult<SummonDto>>>;
public sealed record GetMySummonsQuery(SummonListQuery Query) : IRequest<ApiResponse<PagedResult<SummonDto>>>;
public sealed record GetSummonByIdQuery(int SummonId) : IRequest<ApiResponse<SummonDto>>;
public sealed record ScheduleSummonCommand(int SummonId, ScheduleSummonRequestDto Request) : IRequest<ApiResponse<SummonDto>>;
public sealed record AttendSummonCommand(int SummonId, AttendSummonRequestDto Request) : IRequest<ApiResponse<SummonDto>>;
public sealed record StartSummonObservationCommand(int SummonId, StartSummonObservationRequestDto Request) : IRequest<ApiResponse<SummonDto>>;
public sealed record MarkSummonImprovedCommand(int SummonId, MarkSummonImprovedRequestDto Request) : IRequest<ApiResponse<SummonDto>>;
public sealed record ReviewSummonAutomationImpactCommand(int SummonId, ReviewSummonAutomationImpactRequestDto Request) : IRequest<ApiResponse<SummonDto>>;
public sealed record GetSummonHistoryQuery(int SummonId) : IRequest<ApiResponse<SummonHistoryDto>>;
