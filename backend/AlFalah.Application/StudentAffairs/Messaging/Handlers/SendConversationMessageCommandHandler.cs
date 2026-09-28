using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Messaging;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Messaging.Handlers;

public sealed class SendConversationMessageCommandHandler
    : IRequestHandler<SendConversationMessageCommand, ApiResponse<SendMessageResultDto>>
{
    private readonly IMessagingWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public SendConversationMessageCommandHandler(
        IMessagingWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<SendMessageResultDto>> Handle(
        SendConversationMessageCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<SendMessageResultDto>.Fail("An authenticated user and active school are required");

        if (!IsMessagingRole(_currentUser) || !_currentUser.HasPermission(PermissionNames.MessagingSend))
            return ApiResponse<SendMessageResultDto>.Fail("You do not have permission to perform this action");

        if (string.IsNullOrWhiteSpace(command.Request.Body))
            return ApiResponse<SendMessageResultDto>.Fail("Message body cannot be empty");

        if (string.IsNullOrWhiteSpace(command.Request.IdempotencyKey))
            return ApiResponse<SendMessageResultDto>.Fail("An idempotency key is required");

        if (!await _repository.IsParticipantAsync(
                schoolId.Value,
                userId,
                command.ConversationId,
                cancellationToken).ConfigureAwait(false))
            return ApiResponse<SendMessageResultDto>.Fail("Conversation was not found");

        try
        {
            var result = await _repository.SendMessageAsync(
                schoolId.Value, userId, command.ConversationId, command.Request, cancellationToken).ConfigureAwait(false);
            return ApiResponse<SendMessageResultDto>.Success(result, "Message sent successfully");
        }
        catch (InvalidOperationException exception)
        {
            return ApiResponse<SendMessageResultDto>.Fail(exception.Message);
        }
    }

    private static bool IsMessagingRole(ICurrentUserService currentUser) =>
        currentUser.IsInRole(RoleNames.Guardian)
        || currentUser.IsInRole(RoleNames.Instructor)
        || currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
        || currentUser.IsInRole(RoleNames.SocialWorker);
}
