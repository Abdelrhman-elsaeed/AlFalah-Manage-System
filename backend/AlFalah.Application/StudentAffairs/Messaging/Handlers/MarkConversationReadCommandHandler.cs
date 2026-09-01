using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Messaging;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Messaging.Handlers;

public sealed class MarkConversationReadCommandHandler
    : IRequestHandler<MarkConversationReadCommand, ApiResponse<bool>>
{
    private readonly IMessagingWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public MarkConversationReadCommandHandler(
        IMessagingWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<bool>> Handle(
        MarkConversationReadCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<bool>.Fail("An authenticated user and active school are required");

        if (!_currentUser.HasPermission(PermissionNames.MessagingViewOwn))
            return ApiResponse<bool>.Fail("You do not have permission to perform this action");

        var result = await _repository.MarkConversationReadAsync(
            schoolId.Value,
            userId,
            command.ConversationId,
            command.Request.ThroughMessageId,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<bool>.Success(result);
    }
}
