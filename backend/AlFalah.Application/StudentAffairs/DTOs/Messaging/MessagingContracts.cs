using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.DTOs.Messaging;

public sealed class ConversationListQuery : StudentAffairsPageQuery
{
    public int? StudentId { get; set; }
    public bool? IsUnread { get; set; }
}

public sealed class ConversationMessageQuery : StudentAffairsPageQuery
{
    public long? BeforeMessageId { get; set; }
}

public sealed record CreateConversationRequestDto(
    int StudentId,
    ConversationThreadType ThreadType,
    int? TargetInstructorProfileId,
    string? TargetStaffRole,
    string? TargetStaffUserId,
    string Subject,
    string InitialBody);

public sealed record SendMessageRequestDto(string Body, long? ReplyToMessageId, string IdempotencyKey);
public sealed record MarkConversationReadRequestDto(long ThroughMessageId);
public sealed record CloseConversationRequestDto(string Reason, string RowVersion);

public sealed record ConversationParticipantDto(string UserId, string DisplayName, string Role);
public sealed record ConversationDto(int Id, StudentSummaryDto Student, string Subject, ConversationThreadType ThreadType, ConversationThreadStatus Status, IReadOnlyList<ConversationParticipantDto> Participants, int UnreadCount, DateTimeOffset UpdatedAt, string RowVersion);
public sealed record ConversationMessageDto(long Id, int ConversationId, ActorSummaryDto Sender, string Body, long? ReplyToMessageId, DateTimeOffset CreatedAt, MessageDeliveryState DeliveryState, IReadOnlyList<NotificationDeliveryDto> Receipts);
public sealed record SendMessageResultDto(ConversationMessageDto Message, OfficeHoursDisposition Disposition, DateTimeOffset? NextEligibleSendAt);

public sealed record OfficeHourSlotDto(int Id, DayOfWeek DayOfWeek, TimeOnly StartsAt, TimeOnly EndsAt, DateOnly EffectiveFrom, DateOnly? EffectiveTo, TeacherOfficeHourSource Source, bool IsEligible, string RowVersion);
public sealed record UpdateMyOfficeHoursRequestDto(IReadOnlyList<int> EligibleSlotIds, DateOnly EffectiveFrom, string RowVersion);
public sealed record OverrideTeacherOfficeHoursRequestDto(IReadOnlyList<int> EligibleSlotIds, DateOnly EffectiveFrom, string Reason, string RowVersion);

public sealed record GetConversationsQuery(ConversationListQuery Query) : IRequest<ApiResponse<PagedResult<ConversationDto>>>;
public sealed record CreateConversationCommand(CreateConversationRequestDto Request) : IRequest<ApiResponse<ConversationDto>>;
public sealed record GetConversationByIdQuery(int ConversationId) : IRequest<ApiResponse<ConversationDto>>;
public sealed record GetConversationMessagesQuery(int ConversationId, ConversationMessageQuery Query) : IRequest<ApiResponse<PagedResult<ConversationMessageDto>>>;
public sealed record SendConversationMessageCommand(int ConversationId, SendMessageRequestDto Request) : IRequest<ApiResponse<SendMessageResultDto>>;
public sealed record MarkConversationReadCommand(int ConversationId, MarkConversationReadRequestDto Request) : IRequest<ApiResponse<bool>>;
public sealed record CloseConversationCommand(int ConversationId, CloseConversationRequestDto Request) : IRequest<ApiResponse<ConversationDto>>;
public sealed record GetEligibleOfficeHoursQuery : IRequest<ApiResponse<IReadOnlyList<OfficeHourSlotDto>>>;
public sealed record GetMyOfficeHoursQuery : IRequest<ApiResponse<IReadOnlyList<OfficeHourSlotDto>>>;
public sealed record UpdateMyOfficeHoursCommand(UpdateMyOfficeHoursRequestDto Request) : IRequest<ApiResponse<IReadOnlyList<OfficeHourSlotDto>>>;
public sealed record GetTeacherOfficeHoursQuery(int InstructorId) : IRequest<ApiResponse<IReadOnlyList<OfficeHourSlotDto>>>;
public sealed record OverrideTeacherOfficeHoursCommand(int InstructorId, OverrideTeacherOfficeHoursRequestDto Request) : IRequest<ApiResponse<IReadOnlyList<OfficeHourSlotDto>>>;
