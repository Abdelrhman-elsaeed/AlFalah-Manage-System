using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Messaging;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Messaging.Handlers;

public sealed class GetConversationByIdQueryHandler
    : IRequestHandler<GetConversationByIdQuery, ApiResponse<ConversationDto>>
{
    private readonly IMessagingWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetConversationByIdQueryHandler(
        IMessagingWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<ConversationDto>> Handle(
        GetConversationByIdQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<ConversationDto>.Fail("An authenticated user and active school are required");

        if (!_currentUser.HasPermission(PermissionNames.MessagingViewOwn))
            return ApiResponse<ConversationDto>.Fail("You do not have permission to perform this action");

        var conversation = await _repository.GetConversationByIdAsync(
            schoolId.Value,
            userId,
            request.ConversationId,
            cancellationToken).ConfigureAwait(false);

        if (conversation is null)
            return ApiResponse<ConversationDto>.Fail("Conversation was not found");

        return ApiResponse<ConversationDto>.Success(conversation);
    }
}
