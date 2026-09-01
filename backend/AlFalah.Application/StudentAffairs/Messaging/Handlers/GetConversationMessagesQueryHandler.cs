using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Messaging;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Messaging.Handlers;

public sealed class GetConversationMessagesQueryHandler
    : IRequestHandler<GetConversationMessagesQuery, ApiResponse<PagedResult<ConversationMessageDto>>>
{
    private readonly IMessagingWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetConversationMessagesQueryHandler(
        IMessagingWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<PagedResult<ConversationMessageDto>>> Handle(
        GetConversationMessagesQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<PagedResult<ConversationMessageDto>>.Fail("An authenticated user and active school are required");

        if (!_currentUser.HasPermission(PermissionNames.MessagingViewOwn))
            return ApiResponse<PagedResult<ConversationMessageDto>>.Fail("You do not have permission to perform this action");

        var result = await _repository.GetConversationMessagesAsync(
            schoolId.Value,
            userId,
            request.ConversationId,
            request.Query,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<PagedResult<ConversationMessageDto>>.Success(result);
    }
}
