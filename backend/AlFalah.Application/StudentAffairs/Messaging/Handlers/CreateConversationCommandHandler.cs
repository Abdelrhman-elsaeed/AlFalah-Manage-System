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
    private readonly TimeProvider _timeProvider;

    public CreateConversationCommandHandler(
        IMessagingWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<ConversationDto>> Handle(
        CreateConversationCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<ConversationDto>.Fail("An authenticated user and active school are required");

        var isGuardian = _currentUser.IsInRole(RoleNames.Guardian);
        var isAssignedSocialWorkerCase = _currentUser.IsInRole(RoleNames.SocialWorker)
            && command.Request.ThreadType == Domain.Enums.StudentAffairs.ConversationThreadType.GuardianSocialWorker
            && _currentUser.HasPermission(PermissionNames.MessagingSend)
            && _currentUser.HasPermission(PermissionNames.MessagingViewOwn);
        var hasThreadPermission = command.Request.ThreadType == Domain.Enums.StudentAffairs.ConversationThreadType.GuardianTeacher
            ? _currentUser.HasPermission(PermissionNames.MessagingStartGuardianTeacher)
            : command.Request.ThreadType is Domain.Enums.StudentAffairs.ConversationThreadType.GuardianStudentAffairs
                or Domain.Enums.StudentAffairs.ConversationThreadType.GuardianSocialWorker
                && _currentUser.HasPermission(PermissionNames.MessagingStartGuardianAdministration);
        if ((!isGuardian || !hasThreadPermission) && !isAssignedSocialWorkerCase)
            return ApiResponse<ConversationDto>.Fail("You do not have permission to perform this action");

        if (string.IsNullOrWhiteSpace(command.Request.IdempotencyKey))
            return ApiResponse<ConversationDto>.Fail("An idempotency key is required for the initial message");

        if (string.IsNullOrWhiteSpace(command.Request.Subject))
            return ApiResponse<ConversationDto>.Fail("Subject is required");

        if (string.IsNullOrWhiteSpace(command.Request.InitialBody))
            return ApiResponse<ConversationDto>.Fail("Initial message body is required");

        if (!await _repository.IsConversationTargetAllowedAsync(
                schoolId.Value,
                userId,
                command.Request,
                _timeProvider.GetUtcNow(),
                cancellationToken).ConfigureAwait(false))
            return ApiResponse<ConversationDto>.Fail(
                "The student or recipient is outside the caller's authorized messaging scope");

        try
        {
            var conversation = await _repository.CreateConversationAsync(
                schoolId.Value, userId, command.Request, cancellationToken).ConfigureAwait(false);
            return ApiResponse<ConversationDto>.Success(conversation, "Conversation created successfully");
        }
        catch (InvalidOperationException exception)
        {
            return ApiResponse<ConversationDto>.Fail(exception.Message);
        }
    }
}
