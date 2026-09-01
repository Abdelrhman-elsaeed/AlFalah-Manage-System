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

        if (!_currentUser.HasPermission(PermissionNames.MessagingSend))
            return ApiResponse<SendMessageResultDto>.Fail("You do not have permission to perform this action");

        if (string.IsNullOrWhiteSpace(command.Request.Body))
            return ApiResponse<SendMessageResultDto>.Fail("Message body cannot be empty");

        var result = await _repository.SendMessageAsync(
            schoolId.Value,
            userId,
            command.ConversationId,
            command.Request,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<SendMessageResultDto>.Success(result, "Message sent successfully");
    }
}
