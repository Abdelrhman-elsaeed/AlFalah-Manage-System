using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.DTOs.Referrals;

public sealed class ReferralListQuery : StudentAffairsPageQuery
{
    public StudentReferralStatus? Status { get; set; }
    public ReferralPriority? Priority { get; set; }
    public int? StudentId { get; set; }
    public string? AssignedWorkerUserId { get; set; }
    public bool? IsAssigned { get; set; }
}

public sealed record CreateReferralRequestDto(int StudentId, string Reason, ReferralSourceType Source, ReferralPriority Priority);
public sealed record AssignReferralRequestDto(string SocialWorkerUserId, string? Reason, string RowVersion);
public sealed record AcceptReferralRequestDto(string RowVersion);
public sealed record AddReferralActionRequestDto(StudentCaseActionType ActionType, string Description, DateTimeOffset? ActionAt, string? Result, string RowVersion);
public sealed record ResolveReferralRequestDto(string ResolutionNote, string RowVersion);
public sealed record ReopenReferralRequestDto(string Reason, string RowVersion);

public sealed record ReferralSourceSnapshotDto(ReferralSourceType SourceType, int? SourceEntityId, int? CountSnapshot, int? ThresholdSnapshot);
public sealed record StudentCaseActionDto(int Id, StudentCaseActionType ActionType, string Description, ActorSummaryDto Actor, DateTimeOffset ActionAt, string? Result);

public sealed record ReferralDto(
    int Id,
    StudentSummaryDto Student,
    ReferralSourceSnapshotDto SourceSnapshot,
    MetricBadgeDto? CurrentMetric,
    ReferralPriority Priority,
    StudentReferralStatus Status,
    ActorSummaryDto? AssignedSocialWorker,
    IReadOnlyList<StudentCaseActionDto> Actions,
    string? ResolutionNotes,
    DateTimeOffset CreatedAt,
    string RowVersion);

public sealed record CreateReferralCommand(CreateReferralRequestDto Request, string IdempotencyKey) : IRequest<ApiResponse<ReferralDto>>;
public sealed record GetReferralsQuery(ReferralListQuery Query) : IRequest<ApiResponse<PagedResult<ReferralDto>>>;
public sealed record GetReferralByIdQuery(int ReferralId) : IRequest<ApiResponse<ReferralDto>>;
public sealed record AssignReferralCommand(int ReferralId, AssignReferralRequestDto Request) : IRequest<ApiResponse<ReferralDto>>;
public sealed record AcceptReferralCommand(int ReferralId, AcceptReferralRequestDto Request) : IRequest<ApiResponse<ReferralDto>>;
public sealed record AddReferralActionCommand(int ReferralId, AddReferralActionRequestDto Request) : IRequest<ApiResponse<ReferralDto>>;
public sealed record ResolveReferralCommand(int ReferralId, ResolveReferralRequestDto Request) : IRequest<ApiResponse<ReferralDto>>;
public sealed record ReopenReferralCommand(int ReferralId, ReopenReferralRequestDto Request) : IRequest<ApiResponse<ReferralDto>>;
