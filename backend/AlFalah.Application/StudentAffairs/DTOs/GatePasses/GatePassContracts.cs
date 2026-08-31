using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.DTOs.GatePasses;

public sealed class GatePassListQuery : StudentAffairsPageQuery
{
    public GatePassStatus? Status { get; set; }
    public DateOnly? Date { get; set; }
    public int? ClassroomId { get; set; }
}

public sealed record CreateGatePassRequestDto(
    int StudentId,
    DateTimeOffset DesiredExitTime,
    string Reason,
    string PickupPersonName,
    string? PickupRelationship,
    string? PickupIdentityHint);

public sealed record ApproveGatePassRequestDto(DateTimeOffset WindowStartsAt, DateTimeOffset WindowEndsAt, string? ApprovalNote, string RowVersion);
public sealed record RejectGatePassRequestDto(string Reason, string RowVersion);
public sealed record CancelGatePassRequestDto(string Reason, string RowVersion);
public sealed record AcknowledgeGatePassRequestDto(string RowVersion);
public sealed record ExecuteGatePassRequestDto(DateTimeOffset? ExitedAt, PickupVerificationMethod VerificationMethod, string VerificationNote, string? GateNote, string RowVersion);

public sealed record PickupPersonDto(string Name, string? Relationship, string? IdentityHint);

public sealed record GatePassDto(
    int Id,
    StudentSummaryDto Student,
    DateTimeOffset RequestedAt,
    DateTimeOffset RequestedExitAt,
    string Reason,
    PickupPersonDto PickupPerson,
    GatePassStatus Status,
    DateTimeOffset? ApprovedWindowStartsAt,
    DateTimeOffset? ApprovedWindowEndsAt,
    DateTimeOffset? ReviewedAt,
    DateTimeOffset? ExitedAt,
    ClassroomSummaryDto? CurrentClassroom,
    ActorSummaryDto? CurrentTeacher,
    IReadOnlyList<NotificationDeliveryDto> Notifications,
    string RowVersion);

public sealed record SecurityGatePassQueueItemDto(
    int Id,
    StudentSummaryDto Student,
    string ClassLabel,
    DateTimeOffset ApprovedWindowStartsAt,
    DateTimeOffset ApprovedWindowEndsAt,
    PickupPersonDto PickupPerson,
    string OfficerName,
    DateTimeOffset ApprovedAt,
    GatePassStatus Status,
    string RowVersion);

public sealed record GatePassHistoryDto(IReadOnlyList<TransitionDto> Transitions, IReadOnlyList<NotificationDeliveryDto> Deliveries);

public sealed record CreateGatePassCommand(CreateGatePassRequestDto Request, string IdempotencyKey) : IRequest<ApiResponse<GatePassDto>>;
public sealed record GetMyGatePassesQuery(GatePassListQuery Query) : IRequest<ApiResponse<PagedResult<GatePassDto>>>;
public sealed record GetGatePassesQuery(GatePassListQuery Query) : IRequest<ApiResponse<PagedResult<GatePassDto>>>;
public sealed record GetSecurityGatePassQueueQuery(GatePassListQuery Query) : IRequest<ApiResponse<PagedResult<SecurityGatePassQueueItemDto>>>;
public sealed record GetGatePassByIdQuery(int GatePassId) : IRequest<ApiResponse<GatePassDto>>;
public sealed record ApproveGatePassCommand(int GatePassId, ApproveGatePassRequestDto Request) : IRequest<ApiResponse<GatePassDto>>;
public sealed record RejectGatePassCommand(int GatePassId, RejectGatePassRequestDto Request) : IRequest<ApiResponse<GatePassDto>>;
public sealed record CancelGatePassCommand(int GatePassId, CancelGatePassRequestDto Request) : IRequest<ApiResponse<GatePassDto>>;
public sealed record AcknowledgeGatePassByTeacherCommand(int GatePassId, AcknowledgeGatePassRequestDto Request) : IRequest<ApiResponse<GatePassDto>>;
public sealed record AcknowledgeGatePassBySecurityCommand(int GatePassId, AcknowledgeGatePassRequestDto Request) : IRequest<ApiResponse<GatePassDto>>;
public sealed record ExecuteGatePassCommand(int GatePassId, ExecuteGatePassRequestDto Request) : IRequest<ApiResponse<GatePassDto>>;
public sealed record GetGatePassHistoryQuery(int GatePassId) : IRequest<ApiResponse<GatePassHistoryDto>>;
