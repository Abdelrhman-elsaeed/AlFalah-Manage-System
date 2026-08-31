using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.DTOs.Behaviors;

public sealed class AcademicConcernListQuery : StudentAffairsPageQuery
{
    public int? AcademicTermId { get; set; }
    public int? ClassroomId { get; set; }
    public int? StudentId { get; set; }
    public string? Category { get; set; }
}

public sealed class BehaviorListQuery : StudentAffairsPageQuery
{
    public int? AcademicTermId { get; set; }
    public int? ClassroomId { get; set; }
    public int? StudentId { get; set; }
    public string? Category { get; set; }
    public BehaviorSeverity? Severity { get; set; }
    public bool? HasReferral { get; set; }
    public string? ThresholdSeverity { get; set; }
}

public sealed record CreateAcademicConcernRequestDto(int StudentId, int SchoolTimetableEntryId, string Category, string Description, DateTimeOffset? OccurredAt);
public sealed record DispatchDecisionRequestDto(GuardianDispatchDecision Decision, string? Reason, string RowVersion);
public sealed record CorrectAcademicConcernRequestDto(string Category, string Description, DateTimeOffset OccurredAt, string CorrectionReason, string RowVersion);

public sealed record CreateBehaviorIncidentRequestDto(
    int StudentId,
    int? SchoolTimetableEntryId,
    string Category,
    BehaviorSeverity Severity,
    string Description,
    DateTimeOffset? OccurredAt,
    string? Location,
    string? ImmediateAction);

public sealed record ClassifyBehaviorRequestDto(string Category, BehaviorSeverity Severity, string Reason, string RowVersion);
public sealed record ReferBehaviorRequestDto(ReferralPriority Priority, string Reason, string RowVersion);
public sealed record CorrectBehaviorRequestDto(string Description, DateTimeOffset OccurredAt, string? Location, string CorrectionReason, string RowVersion);

public sealed record AcademicConcernDto(
    int Id,
    StudentSummaryDto Student,
    string Category,
    string Description,
    DateTimeOffset OccurredAt,
    ActorSummaryDto Reporter,
    GuardianDispatchDecision DispatchDecision,
    MetricBadgeDto Metric,
    int? ReferralId,
    string RowVersion);

public sealed record BehaviorIncidentDto(
    int Id,
    StudentSummaryDto Student,
    string Category,
    BehaviorSeverity Severity,
    string Description,
    DateTimeOffset OccurredAt,
    string? Location,
    string? ImmediateAction,
    ActorSummaryDto Reporter,
    GuardianDispatchDecision DispatchDecision,
    MetricBadgeDto Metric,
    int? ReferralId,
    IReadOnlyList<string> QueuedActions,
    string RowVersion);

public sealed record CreateAcademicConcernCommand(CreateAcademicConcernRequestDto Request) : IRequest<ApiResponse<AcademicConcernDto>>;
public sealed record GetAcademicConcernsQuery(AcademicConcernListQuery Query) : IRequest<ApiResponse<PagedResult<AcademicConcernDto>>>;
public sealed record GetAcademicConcernByIdQuery(int ConcernId) : IRequest<ApiResponse<AcademicConcernDto>>;
public sealed record DecideAcademicConcernDispatchCommand(int ConcernId, DispatchDecisionRequestDto Request) : IRequest<ApiResponse<AcademicConcernDto>>;
public sealed record CorrectAcademicConcernCommand(int ConcernId, CorrectAcademicConcernRequestDto Request) : IRequest<ApiResponse<AcademicConcernDto>>;
public sealed record CreateBehaviorIncidentCommand(CreateBehaviorIncidentRequestDto Request) : IRequest<ApiResponse<BehaviorIncidentDto>>;
public sealed record GetBehaviorIncidentsQuery(BehaviorListQuery Query) : IRequest<ApiResponse<PagedResult<BehaviorIncidentDto>>>;
public sealed record GetBehaviorIncidentByIdQuery(int IncidentId) : IRequest<ApiResponse<BehaviorIncidentDto>>;
public sealed record ClassifyBehaviorIncidentCommand(int IncidentId, ClassifyBehaviorRequestDto Request) : IRequest<ApiResponse<BehaviorIncidentDto>>;
public sealed record DecideBehaviorDispatchCommand(int IncidentId, DispatchDecisionRequestDto Request) : IRequest<ApiResponse<BehaviorIncidentDto>>;
public sealed record ReferBehaviorIncidentCommand(int IncidentId, ReferBehaviorRequestDto Request) : IRequest<ApiResponse<BehaviorIncidentDto>>;
public sealed record CorrectBehaviorIncidentCommand(int IncidentId, CorrectBehaviorRequestDto Request) : IRequest<ApiResponse<BehaviorIncidentDto>>;
