using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Messaging;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Messaging.Handlers;

public sealed class GetMessagingAuditQueryHandler
    : IRequestHandler<GetMessagingAuditQuery, ApiResponse<PagedResult<MessagingAuditThreadDto>>>
{
    private readonly IMessagingWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetMessagingAuditQueryHandler(
        IMessagingWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<PagedResult<MessagingAuditThreadDto>>> Handle(
        GetMessagingAuditQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.ActiveSchoolId is not int schoolId || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return ApiResponse<PagedResult<MessagingAuditThreadDto>>.Fail("An authenticated user and active school are required");
        if (!_currentUser.IsInRole(RoleNames.SchoolManager)
            || !_currentUser.HasPermission(PermissionNames.MessagingViewAudit))
            return ApiResponse<PagedResult<MessagingAuditThreadDto>>.Fail("You do not have permission to perform this action");

        var result = await _repository.GetMessagingAuditAsync(schoolId, request.Query, cancellationToken).ConfigureAwait(false);
        return ApiResponse<PagedResult<MessagingAuditThreadDto>>.Success(result);
    }
}
