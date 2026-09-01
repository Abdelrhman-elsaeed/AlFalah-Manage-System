using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Messaging;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Messaging.Handlers;

public sealed class CreateConversationCommandHandler
    : IRequestHandler<CreateConversationCommand, ApiResponse<ConversationDto>>
{
    private readonly IMessagingWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public CreateConversationCommandHandler(
        IMessagingWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<ConversationDto>> Handle(
        CreateConversationCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<ConversationDto>.Fail("An authenticated user and active school are required");

        if (!_currentUser.HasPermission(PermissionNames.MessagingStartGuardianTeacher)
            && !_currentUser.HasPermission(PermissionNames.MessagingStartGuardianAdministration))
            return ApiResponse<ConversationDto>.Fail("You do not have permission to perform this action");

        if (string.IsNullOrWhiteSpace(command.Request.Subject))
            return ApiResponse<ConversationDto>.Fail("Subject is required");

        var conversation = await _repository.CreateConversationAsync(
            schoolId.Value,
            userId,
            command.Request,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<ConversationDto>.Success(conversation, "Conversation created successfully");
    }
}
