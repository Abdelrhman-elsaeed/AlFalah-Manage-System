using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.GatePasses;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.GatePasses.Handlers;

public sealed class GetSecurityGatePassQueueQueryHandler
    : IRequestHandler<GetSecurityGatePassQueueQuery, ApiResponse<PagedResult<SecurityGatePassQueueItemDto>>>
{
    private readonly IGatePassWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetSecurityGatePassQueueQueryHandler(
        IGatePassWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<PagedResult<SecurityGatePassQueueItemDto>>> Handle(
        GetSecurityGatePassQueueQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        if (schoolId is null || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return ApiResponse<PagedResult<SecurityGatePassQueueItemDto>>.Fail(GatePassHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.GatePassAcknowledgeSecurity)
            && !_currentUser.HasPermission(PermissionNames.GatePassExecute))
            return ApiResponse<PagedResult<SecurityGatePassQueueItemDto>>.Fail(GatePassHandlerSupport.PermissionDenied);

        var result = await _repository.GetSecurityGatePassQueueAsync(
            schoolId.Value,
            request.Query,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<PagedResult<SecurityGatePassQueueItemDto>>.Success(result);
    }
}
