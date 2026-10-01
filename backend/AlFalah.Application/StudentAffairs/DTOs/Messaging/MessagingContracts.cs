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

public sealed class MessagingAuditQuery : StudentAffairsPageQuery
{
    public ConversationThreadType? ThreadType { get; set; }
    public ConversationThreadStatus? Status { get; set; }
}

public sealed record MessagingAuditThreadDto(
    int ThreadId,
    ConversationThreadType ThreadType,
    ConversationThreadStatus Status,
    IReadOnlyList<string> ParticipantRoles,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt,
    int MessageCount,
    int PendingDeliveryCount,
    int DeliveredCount,
    int FailedCount);

public sealed record CreateConversationRequestDto(
    int StudentId,
    ConversationThreadType ThreadType,
    int? TargetInstructorProfileId,
    string? TargetStaffRole,
    string? TargetStaffUserId,
    string Subject,
    string InitialBody,
    string IdempotencyKey = "",
    int? ReferralId = null,
    int? TargetGuardianProfileId = null);

public sealed record SendMessageRequestDto(string Body, long? ReplyToMessageId, string IdempotencyKey);
public sealed record MarkConversationReadRequestDto(long ThroughMessageId);
public sealed record CloseConversationRequestDto(string Reason, string RowVersion);

public sealed record ConversationParticipantDto(string UserId, string DisplayName, string Role);
public sealed record ConversationDto(int Id, StudentSummaryDto Student, string Subject, ConversationThreadType ThreadType, ConversationThreadStatus Status, IReadOnlyList<ConversationParticipantDto> Participants, int UnreadCount, DateTimeOffset UpdatedAt, string RowVersion, int? ReferralId = null);
public sealed record ConversationMessageDto(long Id, int ConversationId, ActorSummaryDto Sender, string Body, long? ReplyToMessageId, DateTimeOffset CreatedAt, MessageDeliveryState DeliveryState, OfficeHoursDisposition Disposition, DateTimeOffset? NextEligibleSendAt, IReadOnlyList<NotificationDeliveryDto> Receipts);
public sealed record SendMessageResultDto(ConversationMessageDto Message, OfficeHoursDisposition Disposition, DateTimeOffset? NextEligibleSendAt);
public sealed record GuardianTeacherOptionDto(int InstructorProfileId, string DisplayName, string Subject);
public sealed record SchoolInstructorOptionDto(int InstructorProfileId, string DisplayName, string Subject);
public sealed record GuardianStaffOptionDto(
    string UserId,
    string DisplayName,
    string Role,
    ConversationThreadType ThreadType);
public sealed record StudentGuardianOptionDto(
    int GuardianProfileId,
    string DisplayName,
    string Relationship,
    bool IsPrimary);

public sealed record OfficeHourSlotDto(
    string StableKey,
    DayOfWeek DayOfWeek,
    int PeriodSequence,
    TimeOnly StartsAt,
    TimeOnly EndsAt,
    bool IsEligible,
    bool IsSelected,
    bool IsConflicted,
    string? ConflictReason,
    TeacherOfficeHourSource Source,
    int SchoolTimetableId,
    int TimetableRevision,
    int BellScheduleRevisionId);

public sealed record OfficeHoursAggregateDto(
    int? ConfigurationId,
    int InstructorId,
    int AcademicTermId,
    int SchoolTimetableId,
    int TimetableRevision,
    int BellScheduleRevisionId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string RowVersion,
    TeacherOfficeHourSource Source,
    string UpdatedByUserId,
    DateTimeOffset UpdatedAt,
    string? StatusReason,
    IReadOnlyList<OfficeHourSlotDto> Slots);

public sealed record UpdateMyOfficeHoursRequestDto(IReadOnlyList<string> SelectedSlotKeys, DateOnly EffectiveFrom, string RowVersion);
public sealed record OverrideTeacherOfficeHoursRequestDto(IReadOnlyList<string> SelectedSlotKeys, DateOnly EffectiveFrom, string Reason, string RowVersion);

public sealed record GetConversationsQuery(ConversationListQuery Query) : IRequest<ApiResponse<PagedResult<ConversationDto>>>;
public sealed record GetMessagingAuditQuery(MessagingAuditQuery Query) : IRequest<ApiResponse<PagedResult<MessagingAuditThreadDto>>>;
public sealed record CreateConversationCommand(CreateConversationRequestDto Request) : IRequest<ApiResponse<ConversationDto>>;
public sealed record GetConversationByIdQuery(int ConversationId) : IRequest<ApiResponse<ConversationDto>>;
public sealed record GetConversationMessagesQuery(int ConversationId, ConversationMessageQuery Query) : IRequest<ApiResponse<PagedResult<ConversationMessageDto>>>;
public sealed record SendConversationMessageCommand(int ConversationId, SendMessageRequestDto Request) : IRequest<ApiResponse<SendMessageResultDto>>;
public sealed record MarkConversationReadCommand(int ConversationId, MarkConversationReadRequestDto Request) : IRequest<ApiResponse<bool>>;
public sealed record CloseConversationCommand(int ConversationId, CloseConversationRequestDto Request) : IRequest<ApiResponse<ConversationDto>>;
public sealed record GetGuardianTeacherOptionsQuery(int StudentId) : IRequest<ApiResponse<IReadOnlyList<GuardianTeacherOptionDto>>>;
public sealed record GetGuardianStaffOptionsQuery(int StudentId) : IRequest<ApiResponse<IReadOnlyList<GuardianStaffOptionDto>>>;
public sealed record GetStudentGuardianOptionsQuery(int StudentId) : IRequest<ApiResponse<IReadOnlyList<StudentGuardianOptionDto>>>;
public sealed record GetEligibleOfficeHoursQuery : IRequest<ApiResponse<OfficeHoursAggregateDto>>;
public sealed record GetMyOfficeHoursQuery : IRequest<ApiResponse<OfficeHoursAggregateDto>>;
public sealed record UpdateMyOfficeHoursCommand(UpdateMyOfficeHoursRequestDto Request) : IRequest<ApiResponse<OfficeHoursAggregateDto>>;
public sealed record GetTeacherOfficeHoursQuery(int InstructorId) : IRequest<ApiResponse<OfficeHoursAggregateDto>>;
public sealed record GetSchoolInstructorOptionsQuery(string? Search) : IRequest<ApiResponse<IReadOnlyList<SchoolInstructorOptionDto>>>;
public sealed record OverrideTeacherOfficeHoursCommand(int InstructorId, OverrideTeacherOfficeHoursRequestDto Request) : IRequest<ApiResponse<OfficeHoursAggregateDto>>;
