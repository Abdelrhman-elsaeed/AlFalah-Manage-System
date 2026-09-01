using AlFalah.Application.StudentAffairs.DTOs.Messaging;
using AlFalah.Shared.Models;

namespace AlFalah.Application.StudentAffairs.Messaging;

public interface IMessagingWorkflowRepository
{
    Task<PagedResult<ConversationDto>> GetConversationsAsync(
        int schoolId,
        string userId,
        ConversationListQuery query,
        CancellationToken cancellationToken);

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

    Task<IReadOnlyList<OfficeHourSlotDto>> GetEligibleOfficeHoursAsync(
        int schoolId,
        string userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OfficeHourSlotDto>> GetMyOfficeHoursAsync(
        int schoolId,
        string userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OfficeHourSlotDto>> UpdateMyOfficeHoursAsync(
        int schoolId,
        string userId,
        UpdateMyOfficeHoursRequestDto request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OfficeHourSlotDto>> GetTeacherOfficeHoursAsync(
        int schoolId,
        int instructorId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OfficeHourSlotDto>> OverrideTeacherOfficeHoursAsync(
        int schoolId,
        string adminUserId,
        int instructorId,
        OverrideTeacherOfficeHoursRequestDto request,
        CancellationToken cancellationToken);
}
