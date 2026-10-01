using AlFalah.Application.StudentAffairs.DTOs.Messaging;
using AlFalah.Shared.Models;

namespace AlFalah.Application.StudentAffairs.Messaging;

public interface IMessagingWorkflowRepository
{
    Task<MessageReleaseResult> ReleaseDueMessageAsync(int messageId, CancellationToken cancellationToken);
    Task ReconcileOfficeHoursAsync(int schoolId, IReadOnlyCollection<string>? teacherUserIds, CancellationToken cancellationToken);
    Task<bool> IsParticipantAsync(
        int schoolId,
        string userId,
        int conversationId,
        CancellationToken cancellationToken);

    Task<bool> IsConversationTargetAllowedAsync(
        int schoolId,
        string creatorUserId,
        CreateConversationRequestDto request,
        DateTimeOffset instant,
        CancellationToken cancellationToken);

    Task<PagedResult<ConversationDto>> GetConversationsAsync(
        int schoolId,
        string userId,
        ConversationListQuery query,
        CancellationToken cancellationToken);

    Task<PagedResult<MessagingAuditThreadDto>> GetMessagingAuditAsync(
        int schoolId,
        MessagingAuditQuery query,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GuardianTeacherOptionDto>> GetGuardianTeacherOptionsAsync(
        int schoolId, string guardianUserId, int studentId, DateTimeOffset instant, CancellationToken cancellationToken);

    Task<IReadOnlyList<GuardianStaffOptionDto>> GetGuardianStaffOptionsAsync(
        int schoolId, string guardianUserId, int studentId, DateTimeOffset instant, CancellationToken cancellationToken);

    Task<IReadOnlyList<StudentGuardianOptionDto>> GetStudentGuardianOptionsAsync(
        int schoolId, int studentId, DateTimeOffset instant, CancellationToken cancellationToken);

    Task<ConversationDto?> GetConversationByIdAsync(
        int schoolId,
        string userId,
        int conversationId,
        CancellationToken cancellationToken);

    Task<PagedResult<ConversationMessageDto>> GetConversationMessagesAsync(
        int schoolId,
        string userId,
        int conversationId,
        ConversationMessageQuery query,
        CancellationToken cancellationToken);

    Task<ConversationDto> CreateConversationAsync(
        int schoolId,
        string creatorUserId,
        CreateConversationRequestDto request,
        CancellationToken cancellationToken);

    Task<SendMessageResultDto> SendMessageAsync(
        int schoolId,
        string senderUserId,
        int conversationId,
        SendMessageRequestDto request,
        CancellationToken cancellationToken);

    Task<bool> MarkConversationReadAsync(
        int schoolId,
        string userId,
        int conversationId,
        long throughMessageId,
        CancellationToken cancellationToken);

    Task<ConversationDto?> CloseConversationAsync(
        int schoolId,
        string userId,
        int conversationId,
        CloseConversationRequestDto request,
        CancellationToken cancellationToken);

    Task<OfficeHoursAggregateDto> GetEligibleOfficeHoursAsync(
        int schoolId,
        string userId,
        CancellationToken cancellationToken);

    Task<OfficeHoursAggregateDto> GetMyOfficeHoursAsync(
        int schoolId,
        string userId,
        CancellationToken cancellationToken);

    Task<OfficeHoursAggregateDto> UpdateMyOfficeHoursAsync(
        int schoolId,
        string userId,
        UpdateMyOfficeHoursRequestDto request,
        CancellationToken cancellationToken);

    Task<OfficeHoursAggregateDto> GetTeacherOfficeHoursAsync(
        int schoolId,
        string requesterUserId,
        int instructorId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SchoolInstructorOptionDto>> GetSchoolInstructorOptionsAsync(
        int schoolId,
        string? search,
        CancellationToken cancellationToken);

    Task<OfficeHoursAggregateDto> OverrideTeacherOfficeHoursAsync(
        int schoolId,
        string adminUserId,
        int instructorId,
        OverrideTeacherOfficeHoursRequestDto request,
        CancellationToken cancellationToken);
}

public sealed record MessageReleaseResult(bool Completed, DateTimeOffset? NextAttemptAt);
